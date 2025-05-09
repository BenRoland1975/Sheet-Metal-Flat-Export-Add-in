using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SwView = SolidWorks.Interop.sldworks.View;
using AppSettings = RoesleinAddIn.Settings;
using SysEnv = System.Environment;
using System.Linq;
using System.Diagnostics;

namespace RoesleinAddIn
{
    /// <summary>
    /// Processor for sheet metal parts
    /// </summary>
    public class SheetMetalProcessor
    {
        #region Private Members
        private readonly ISldWorks _swApp;
        private AppSettings settings;
        private string outDir;
        private bool loggingEnabled;
        private List<string> thicknessFailures = new List<string>();
        private List<string> exportErrors = new List<string>();
        private Dictionary<string, List<string>> unmappedMaterialParts = new Dictionary<string, List<string>>();
        private List<string> ignoredParts = new List<string>();
        private Dictionary<string, int> materialThicknessCounts = new Dictionary<string, int>();
        private HashSet<string> _processedPartPaths;
        #endregion

        #region Constructor
        /// <summary>
        /// Constructor
        /// </summary>
        public SheetMetalProcessor(ISldWorks swApp)
        {
            Debug.WriteLine("[DEBUG] SheetMetalProcessor Constructor START"); 
            _swApp = swApp; 
            
            try 
            {
                Debug.WriteLine("[DEBUG] Constructor: Loading settings..."); 
                LoadSettings();
                EnsureDirectoriesExist(); 
                Debug.WriteLine("[DEBUG] Constructor: Settings loaded and directories verified"); 
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] Error in constructor: {ex.Message}"); 
                Logger.Error($"Error in constructor: {ex.Message}", ex);
            }
            Debug.WriteLine("[DEBUG] SheetMetalProcessor Constructor END"); 
        }

        private void LoadSettings()
        {
            settings = AppSettings.LoadSettings();
            
            if (settings == null)
            {
                Debug.WriteLine("Settings loaded as null, creating new default settings object.");
                settings = new AppSettings();
            }

            // Get and validate export directory
            outDir = settings.LastExportFolder;
            if (string.IsNullOrEmpty(outDir))
            {
                outDir = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments),
                    "Roeslein", "DXF Exports");
                settings.LastExportFolder = outDir;
                settings.SaveSettings();
            }

            // Ensure path ends with backslash
            if (!outDir.EndsWith("\\"))
                outDir += "\\";

            loggingEnabled = settings.LoggingEnabled;
            Debug.WriteLine($"Settings loaded - Export Dir: {outDir}, Logging Enabled: {loggingEnabled}");
        }

        private void EnsureDirectoriesExist()
        {
            try
            {
                // Ensure export directory exists
                if (!Directory.Exists(outDir))
                {
                    Directory.CreateDirectory(outDir);
                    Debug.WriteLine($"Created export directory: {outDir}");
                }

                // Ensure log directory exists
                if (!string.IsNullOrEmpty(settings.LogFilePath))
                {
                    string logDir = Path.GetDirectoryName(settings.LogFilePath);
                    if (!Directory.Exists(logDir))
                    {
                        Directory.CreateDirectory(logDir);
                        Debug.WriteLine($"Created log directory: {logDir}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating directories: {ex.Message}");
                Logger.Error("Error creating required directories", ex);
            }
        }

        private void LogToFile(string message)
        {
            if (settings?.LoggingEnabled == true && !string.IsNullOrEmpty(settings.LogFilePath))
            {
                try
                {
                    File.AppendAllText(settings.LogFilePath, $"\n[{DateTime.Now}] {message}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error writing to log file: {ex.Message}");
                }
            }
        }

        private string MapMaterial(string swMaterial, string partTitle = null)
        {
            if (string.IsNullOrWhiteSpace(swMaterial)) return null;
            string normalized = swMaterial.Trim();
            var mappings = Settings.LoadMaterialMappings();
            var match = mappings.FirstOrDefault(m => string.Equals(m.SwMaterial.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (match != null && !string.IsNullOrWhiteSpace(match.DxfMaterial))
            {
                return match.DxfMaterial.Trim();
            }
            
            // Track unmapped material
            if (!unmappedMaterialParts.ContainsKey(normalized))
                unmappedMaterialParts[normalized] = new List<string>();
            
            // Add part title if provided and not already in the list
            if (!string.IsNullOrEmpty(partTitle) && 
                !unmappedMaterialParts[normalized].Contains(partTitle))
            {
                unmappedMaterialParts[normalized].Add(partTitle);
                Logger.Info($"Added part '{partTitle}' to unmapped material '{normalized}'");
            }
        
            return null;
        }

        private void AddNotesToDrawing(DrawingDoc swDrawingDoc, ModelDoc2 swPartModel, string configName, string thickness, string flatViewName)
        {
            ModelDoc2 swDraw = (ModelDoc2)swDrawingDoc;
            if (swDraw == null)
            {
                Logger.Error("ERROR: Could not get ModelDoc2 from DrawingDoc in AddNotesToDrawing");
                return;
            }

            try
            {
                // --- 1 Create / activate Notes layer --------------------------------
                LayerMgr lyrMgr = (LayerMgr)swDraw.GetLayerManager();
                if (lyrMgr == null)
                {
                    Logger.Error("ERROR: Could not get Layer Manager");
                    return;
                }

                string layerName = string.IsNullOrWhiteSpace(settings.TextLayerName)
                                  ? "Notes" : settings.TextLayerName;
                Logger.Info($"Using text layer: '{layerName}' from settings");

                Layer notesLayer = (Layer)lyrMgr.GetLayer(layerName);
                if (notesLayer == null)
                {
                    bool result = swDrawingDoc.CreateLayer2(
                        layerName, "Property Notes", ColorTranslator.ToWin32(Color.Magenta),
                        (int)swLineStyles_e.swLineCONTINUOUS,
                        (int)swLineWeights_e.swLW_NORMAL, true, true);
                    
                    if (!result)
                    {
                        Logger.Error($"ERROR: Failed to create '{layerName}' layer");
                        return;
                    }
                    notesLayer = (Layer)lyrMgr.GetLayer(layerName);
                }
                
                notesLayer.Color = ColorTranslator.ToWin32(Color.Magenta);
                notesLayer.Visible = true;
                notesLayer.Printable = true;
                lyrMgr.SetCurrentLayer(layerName);

                // --- 2 Collect custom property values -----------------------------------
                CustomPropertyManager propMgr = swPartModel.Extension.CustomPropertyManager[configName];
                if (propMgr == null)
                {
                    Logger.Error("ERROR: Could not get CustomPropertyManager");
                    return;
                }

                // Load property mappings in correct order
                string[] notes;
                var propertyMappings = Settings.LoadPropertyMappings()?.OrderBy(m => m.RowNumber).ToList();
                if (propertyMappings == null || propertyMappings.Count == 0)
                {
                    Logger.Warning("WARNING: No property mappings found, using default order");
                    notes = new string[]
                    {
                        $"Shop Route: {GetCustomPropertyValue(propMgr, "Shop Route")}",
                        $"Thickness: {thickness}",
                        $"Material: {MapMaterial(GetCustomPropertyValue(propMgr, "Material"))}",
                        $"Revision: {GetCustomPropertyValue(propMgr, "Revision")}",
                        $"Description: {GetCustomPropertyValue(propMgr, "Description")}",
                        $"Part Number: {GetCustomPropertyValue(propMgr, "Part Number")}"
                    };
                }
                else
                {
                    // Create notes array based on property mapping order
                    var orderedNotes = new List<string>();
                    foreach (var mapping in propertyMappings)
                    {
                        string value = mapping.SwCustomProperty == "Sheet Metal Thickness" ? 
                                     thickness : 
                                     GetCustomPropertyValue(propMgr, mapping.SwCustomProperty);
                        
                        if (mapping.SwCustomProperty == "Material")
                        {
                            string partTitle = swPartModel.GetTitle();
                            value = MapMaterial(value, partTitle) ?? value;
                        }
                            
                        orderedNotes.Add($"{mapping.DxfPropertyName}: {value}");
                    }
                    notes = orderedNotes.ToArray();
                }

                Logger.Info("Adding notes to drawing with values:");
                foreach (string note in notes)
                {
                    Logger.Info($"  - {note}");
                }

                // --- 3 Insert each note – anchor to SHEET not VIEW -------------------
                // Clear DXF mapping file before creating notes
                _swApp.SetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDxfMappingFile, "");
                
                // Get the flat pattern view bounds
                SwView flatView = (SwView)swDrawingDoc.GetFirstView();
                while (flatView != null && flatView.GetName2() != flatViewName)
                {
                    flatView = (SwView)flatView.GetNextView();
                }

                if (flatView == null)
                {
                    Logger.Error($"ERROR: Could not find view '{flatViewName}' for text placement");
                    return;
                }

                // Get view outline - returns array of 4 doubles: [xmin, ymin, xmax, ymax]
                object outlineObj = flatView.GetOutline();
                if (outlineObj == null)
                {
                    Logger.Error("ERROR: Failed to get view outline");
                    return;
                }
                
                double[] viewBounds = (double[])outlineObj;
                
                // Calculate center coordinates and adjust for text block placement
                double centerX = (viewBounds[0] + viewBounds[2]) / 2.0;
                double spacing = 0.0075; // Reduced spacing between lines
                
                // Calculate total height of text block
                int numNotes = notes.Count(t => !string.IsNullOrWhiteSpace(t));
                double textBlockHeight = numNotes * spacing;
                
                // Position text block just above the part's center
                double startY = viewBounds[1] + ((viewBounds[3] - viewBounds[1]) / 2.0) + (textBlockHeight / 2.0);
                
                Logger.Info($"View bounds: Left={viewBounds[0]}, Bottom={viewBounds[1]}, Right={viewBounds[2]}, Top={viewBounds[3]}");
                Logger.Info($"Center point: X={centerX}, Y={startY}");
                
                // Select the sheet for note anchoring
                swDraw.Extension.SelectByID2("", "SHEET", 0, 0, 0, false, 0, null, 0);

                int noteIndex = 0;
                foreach (string noteText in notes.Where(t => !string.IsNullOrWhiteSpace(t)))
                {
                    Note swNote = (Note)swDraw.InsertNote(noteText);
                    if (swNote == null)
                    {
                        Logger.Error($"ERROR: Failed to insert note: {noteText}");
                        continue;
                    }

                    Annotation ann = (Annotation)swNote.GetAnnotation();
                    if (ann != null)
                    {
                        ann.Layer = layerName;
                        // Place notes starting from top, working down
                        double y = startY - (noteIndex * spacing);
                        ann.SetPosition2(centerX, y, 0);
                        notesLayer.Color = ColorTranslator.ToWin32(Color.Magenta);
                        Logger.Info($"Placed note at X={centerX}, Y={y}: {noteText}");
                        noteIndex++;
                    }
                    else
                    {
                        Logger.Error($"ERROR: Failed to get annotation for note: {noteText}");
                    }
                }

                swDraw.ForceRebuild3(true);
                swDraw.GraphicsRedraw2();
                Logger.Info("Successfully added all notes to drawing");
            }
            catch (Exception ex)
            {
                Logger.Error($"ERROR in AddNotesToDrawing: {ex.Message}\n{ex.StackTrace}");
            }
        }
        #endregion

        #region Public Methods
        public void ProcessAssemblySheetMetal()
        {
            Debug.WriteLine("[DEBUG] ProcessAssemblySheetMetal START"); // DEBUG
            Debug.WriteLine("[DEBUG] Attempting Logger.Info: Starting assembly processing"); // DEBUG
            Logger.Info("Starting assembly processing");
            ModelDoc2 swModel = _swApp.ActiveDoc as ModelDoc2;
            
            // Clear processed part paths to allow re-processing in new runs
            _processedPartPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            if (swModel == null)
            {
                MessageBox.Show("No active document.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (swModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                MessageBox.Show("Active document is not an assembly.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            LoadSettings();

            if (string.IsNullOrEmpty(settings.LastExportFolder) || !Directory.Exists(settings.LastExportFolder))
            {
                 MessageBox.Show("Output folder is not set or does not exist. Please configure it in the settings.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Error);
                 Logger.Error("Output folder not configured or invalid.");
                 return;
            }
            string outputFolder = settings.LastExportFolder;
            if (!outputFolder.EndsWith("\\")) outputFolder += "\\";

            // Get the assembly name for later use with export
            string assemblyName = Path.GetFileNameWithoutExtension(swModel.GetTitle());

            AssemblyDoc swAssy = swModel as AssemblyDoc;
            object[] components = swAssy.GetComponents(true) as object[];
            
            if (components == null || components.Length == 0)
            {
                MessageBox.Show("No components found in the assembly.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Warning("No components found in assembly.");
                return;
            }

            int totalProcessed = 0;
            int sheetMetalPartsFound = 0;
            thicknessFailures.Clear();
            exportErrors.Clear();
            ignoredParts.Clear();
            unmappedMaterialParts.Clear();
            materialThicknessCounts.Clear();
            List<string> processedFiles = new List<string>();

            Logger.Info($"Processing {components.Length} components in assembly {swModel.GetPathName()}");
            Logger.Info($"Output Directory: {outputFolder}");

            // Use the root component for recursion
            ConfigurationManager swConfMgr = swModel.ConfigurationManager;
            Configuration swConf = swConfMgr.ActiveConfiguration;
            Component2 swRootComp = swConf.GetRootComponent3(true);
            if (swRootComp == null)
            {
                MessageBox.Show("Failed to get root component.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Warning("Failed to get root component.");
                return;
            }
            ProcessAssemblyComponentsRecursively(swRootComp, outputFolder, ref sheetMetalPartsFound, ref totalProcessed, processedFiles);

            // Use StringBuilder for robust string construction
            var summaryBuilder = new System.Text.StringBuilder();

            if (totalProcessed > 0)
            {
                summaryBuilder.AppendLine($"Successfully processed {totalProcessed} out of {sheetMetalPartsFound} sheet metal parts found.");
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine($"Files saved to: {outputFolder}");
                Debug.WriteLine("[DEBUG] Attempting Logger.Info: Successfully processed {totalProcessed} sheet metal parts."); // DEBUG
                Logger.Info($"Successfully processed {totalProcessed} sheet metal parts.");

                // Add material-thickness breakdown section
                if (materialThicknessCounts.Count > 0)
                {
                    summaryBuilder.AppendLine();
                    summaryBuilder.AppendLine("DXFs created by Material and Thickness:");
                    foreach (var entry in materialThicknessCounts.OrderByDescending(e => e.Value))
                    {
                        summaryBuilder.AppendLine($"- {entry.Value} DXFs: {entry.Key}");
                    }
                }
            }
            else if (sheetMetalPartsFound > 0)
            {
                 summaryBuilder.AppendLine($"Found {sheetMetalPartsFound} sheet metal parts, but none were processed successfully.");
                 summaryBuilder.AppendLine($"Check log file for details: {settings.LogFilePath}");
                 Debug.WriteLine("[DEBUG] Attempting Logger.Warning: Found {sheetMetalPartsFound} sheet metal parts, but none were processed."); // DEBUG
                 Logger.Warning($"Found {sheetMetalPartsFound} sheet metal parts, but none were processed.");
            }
            else
            {
                summaryBuilder.AppendLine("No sheet metal parts found or processed in the assembly.");
                Debug.WriteLine("[DEBUG] Attempting Logger.Info: No sheet metal parts found or processed."); // DEBUG
                Logger.Info("No sheet metal parts found or processed.");
            }

             if (thicknessFailures.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine($"WARNING: Could not determine sheet metal thickness for {thicknessFailures.Count} parts:");
                foreach (string failure in thicknessFailures)
                {
                    summaryBuilder.AppendLine($"- {failure}");
                }
                Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Thickness failures: {string.Join(", ", thicknessFailures)}"); // DEBUG
                Logger.Warning($"Thickness failures: {string.Join(", ", thicknessFailures)}");
            }

             if (exportErrors.Count > 0)
            {
                 summaryBuilder.AppendLine();
                 summaryBuilder.AppendLine($"ERROR: Encountered {exportErrors.Count} errors during processing:");
                 foreach(string error in exportErrors)
                 {
                     summaryBuilder.AppendLine($"- {error}");
                 }
                 Debug.WriteLine($"[DEBUG] Attempting Logger.Error: Processing errors encountered: {exportErrors.Count}"); // DEBUG
                 Logger.Error($"Processing errors encountered: {exportErrors.Count}");
            }

            if (unmappedMaterialParts.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine("The following materials were not found in the Material Mapping settings:");
                
                foreach (var materialEntry in unmappedMaterialParts.OrderBy(m => m.Key))
                {
                    string material = materialEntry.Key;
                    List<string> parts = materialEntry.Value;
                    
                    summaryBuilder.AppendLine($"- {material}");
                    
                    // List part numbers that use this unmapped material (up to 5)
                    if (parts.Count > 0)
                    {
                        int displayCount = Math.Min(parts.Count, 5); // Display up to 5 parts per material
                        string partsList = string.Join(", ", parts.Take(displayCount));
                        
                        if (parts.Count > displayCount)
                        {
                            summaryBuilder.AppendLine($"   Used in: {partsList}, and {parts.Count - displayCount} more part(s)");
                        }
                        else
                        {
                            summaryBuilder.AppendLine($"   Used in: {partsList}");
                        }
                    }
                }
                
                summaryBuilder.AppendLine("\nPlease add these materials to the Material Mapping tab in Settings and run the process again.");
            }

            // Consolidate ignored parts list by counting occurrences of each part
            if (ignoredParts.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine($"Parts ignored (not marked for laser cutting): {ignoredParts.Count}");
                
                // Group parts by name and count occurrences
                var consolidatedParts = ignoredParts
                    .GroupBy(part => part)
                    .Select(group => new { Name = group.Key, Count = group.Count() })
                    .OrderBy(item => item.Name);
                
                foreach (var item in consolidatedParts)
                {
                    if (item.Count > 1)
                        summaryBuilder.AppendLine($"- {item.Name} (not processed {item.Count} times)");
                    else
                        summaryBuilder.AppendLine($"- {item.Name}");
                }
            }

            string summaryMessage = summaryBuilder.ToString(); // Get the final string
            
            // Show custom dialog with scrollable text and export button, passing assembly name
            ShowResultDialog("Roeslein Add-in - Processing Complete", summaryMessage, outputFolder, assemblyName);

            Debug.WriteLine("[DEBUG] Attempting Logger.Info: Completed assembly processing. Total processed: {totalProcessed}, Found SM: {sheetMetalPartsFound}"); // DEBUG
            Logger.Info($"Completed assembly processing. Total processed: {totalProcessed}, Found SM: {sheetMetalPartsFound}");
            Debug.WriteLine("[DEBUG] ProcessAssemblySheetMetal END"); // DEBUG
        }

        // Create a custom dialog with scrollable text area and export button
        private void ShowResultDialog(string title, string message, string defaultExportFolder, string assemblyName = "")
        {
            try
            {
                using (var resultForm = new Form())
                {
                    resultForm.Text = title;
                    resultForm.Width = 600;
                    resultForm.Height = 500;
                    resultForm.StartPosition = FormStartPosition.CenterScreen;
                    resultForm.MinimizeBox = false;
                    resultForm.MaximizeBox = false;
                    resultForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    
                    // Create a scrollable text box to display the summary
                    TextBox summaryTextBox = new TextBox();
                    summaryTextBox.Multiline = true;
                    summaryTextBox.ReadOnly = true;
                    summaryTextBox.ScrollBars = ScrollBars.Vertical;
                    summaryTextBox.Font = new Font("Consolas", 9F);
                    summaryTextBox.Dock = DockStyle.Fill;
                    summaryTextBox.Text = message;
                    
                    // Create a panel for buttons at the bottom
                    Panel buttonPanel = new Panel();
                    buttonPanel.Dock = DockStyle.Bottom;
                    buttonPanel.Height = 50;
                    
                    // Create OK button
                    Button okButton = new Button();
                    okButton.Text = "OK";
                    okButton.DialogResult = DialogResult.OK;
                    okButton.Location = new Point(490, 15);
                    okButton.Size = new Size(80, 25);
                    buttonPanel.Controls.Add(okButton);
                    
                    // Create Export button
                    Button exportButton = new Button();
                    exportButton.Text = "Export Report";
                    exportButton.Location = new Point(370, 15);
                    exportButton.Size = new Size(100, 25);
                    exportButton.Click += (sender, e) => 
                    {
                        try 
                        {
                            // Show save file dialog
                            using (SaveFileDialog saveDialog = new SaveFileDialog())
                            {
                                saveDialog.Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv|All Files (*.*)|*.*";
                                saveDialog.Title = "Export Processing Results";
                                saveDialog.InitialDirectory = defaultExportFolder;
                                
                                // Create default filename with assembly name and date/time
                                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                                string defaultFilename = string.IsNullOrEmpty(assemblyName) 
                                    ? $"Roeslein_Processing_Results_{timestamp}.txt"
                                    : $"{assemblyName}_Processing_Results_{timestamp}.txt";
                                    
                                saveDialog.FileName = defaultFilename;
                                
                                if (saveDialog.ShowDialog() == DialogResult.OK)
                                {
                                    File.WriteAllText(saveDialog.FileName, message);
                                    MessageBox.Show($"Results exported to {saveDialog.FileName}", "Export Complete", 
                                                   MessageBoxButtons.OK, MessageBoxIcon.Information);
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Error($"Error exporting results: {ex.Message}");
                            MessageBox.Show($"Failed to export results: {ex.Message}", "Export Error", 
                                           MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    };
                    buttonPanel.Controls.Add(exportButton);
                    
                    // Add controls to form
                    resultForm.Controls.Add(summaryTextBox);
                    resultForm.Controls.Add(buttonPanel);
                    
                    // Show dialog
                    resultForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error showing result dialog: {ex.Message}");
                MessageBox.Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        public void ProcessSinglePartSheetMetal()
        {
             Debug.WriteLine("[DEBUG] ProcessSinglePartSheetMetal START"); // DEBUG
             ModelDoc2 swModel = _swApp.ActiveDoc as ModelDoc2;

             if (swModel == null)
             {
                 MessageBox.Show("No active document.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
             }

             if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
             {
                 MessageBox.Show("Active document is not a part.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
             }

             PartDoc swPart = swModel as PartDoc;

             if (!IsSheetMetalPart(swPart))
             {
                  Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Single part processing skipped: {swModel.GetPathName()} is not a sheet metal part."); // DEBUG
                  Logger.Info($"Single part processing skipped: {swModel.GetPathName()} is not a sheet metal part.");
                  return;
             }

             // Add laser cutting check for single parts
             if (!IsLaserCuttingPart(swModel))
             {
                 Logger.Warning($"Single part {swModel.GetTitle()} skipped - not marked for laser cutting");
                 MessageBox.Show($"Part {swModel.GetTitle()} is not marked for laser cutting in its Shop Route property.", 
                              "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                 return;
             }

             Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Starting single part processing for: {swModel.GetPathName()}"); // DEBUG
             Logger.Info($"Starting single part processing for: {swModel.GetPathName()}");
             LoadSettings();

             // Determine output folder - Use the DXF Output Folder from settings (this.outDir)
             string outputFolder = this.outDir; // this.outDir is initialized by LoadSettings from settings.LastExportFolder

             // Sanity check for the output folder
             if (string.IsNullOrEmpty(outputFolder) || !Directory.Exists(outputFolder))
             {
                 Logger.Error($"Single Part Export: Output folder from settings ('{outputFolder ?? "NULL"}') is invalid or inaccessible. Please check settings.");
                 MessageBox.Show($"The DXF Output Folder specified in settings ('{outputFolder ?? "Not Set"}') is invalid or does not exist.\n\nPlease check the path in the settings form.", 
                                 "Roeslein Add-in - Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                 return;
             }

             // Compose output file path
             string fileName = Path.GetFileNameWithoutExtension(swModel.GetPathName()) + ".dxf";
             string outputPath = Path.Combine(outputFolder, fileName);

             string result = ProcessSheetMetalPart(swPart, outputFolder, Path.GetFileNameWithoutExtension(swModel.GetPathName()));

             if (!string.IsNullOrEmpty(result))
             {
                  Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Single part DXF exported successfully to: {result}"); // DEBUG
                  Logger.Info($"Single part DXF exported successfully to: {result}");
                  MessageBox.Show($"DXF file exported successfully to:\n{result}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
             }
             else
             {
                  Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Single part DXF export failed for: {swModel.GetPathName()}"); // DEBUG
                  Logger.Warning($"Single part DXF export failed for: {swModel.GetPathName()}");
                  MessageBox.Show($"Failed to export DXF file for {fileName}.\n\nCheck log for details: {settings.LogFilePath}", "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
             }

             Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Completed single part processing"); // DEBUG
             Logger.Info("Completed single part processing");
             Debug.WriteLine("[DEBUG] ProcessSinglePartSheetMetal END"); // DEBUG
        }
        #endregion

        #region Private Methods
        private string ProcessSheetMetalComponent(ModelDoc2 compDoc, string outputFolder)
        {
            if (compDoc == null || compDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
            {
                return null;
            }
            
            PartDoc swPart = compDoc as PartDoc;
            string fileName = Path.GetFileNameWithoutExtension(compDoc.GetPathName());
            string outputPath = Path.Combine(outputFolder, fileName + ".dxf");
            
            // Get the base file name without extension
            string baseFileName = Path.GetFileNameWithoutExtension(fileName);
            
            // Call ProcessSheetMetalPart with the base file name
            return ProcessSheetMetalPart(swPart, outputFolder, baseFileName);
        }

        /// <summary>
        /// Process sheet metal part and export to DXF
        /// </summary>
        public string ProcessSheetMetalPart(PartDoc swPart, string outputFolder, string baseFileName = null)
        {
            if (swPart == null) return null;
            
            // Declare variables outside try block for accessibility in catch block
            string partTitle = "Unknown"; 
            string fileName = "Unknown";
            ModelDoc2 swModel = null;
            string originalConfig = null;
            ModelDoc2 swDraw = null;

            try
            {
                swModel = swPart as ModelDoc2;
                partTitle = swModel.GetTitle();
                // LASER CHECK: Check before any property updates
                if (!IsLaserCuttingPart(swModel))
                {
                    Logger.Warning($"Part {partTitle} skipped - not marked for laser cutting (Shop Route: '{GetCustomProperty(swModel, "Shop Route")}')");
                    ignoredParts.Add(partTitle);
                    return null;
                }
                string partPath = swModel.GetPathName();
                fileName = baseFileName ?? Path.GetFileNameWithoutExtension(partTitle);
                string outputPath = Path.Combine(outputFolder, fileName + ".dxf");

                Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Processing part: {partTitle} for DXF export to {outputPath}"); // DEBUG
                Logger.Info($"Processing part: {partTitle} for DXF export to {outputPath}");

                originalConfig = swModel.ConfigurationManager.ActiveConfiguration.Name;
                string flatConfig = FindFlatPatternConfig(swModel);

                if (string.IsNullOrEmpty(flatConfig))
                {
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Error: No Flat-Pattern configuration found for {partTitle}. Cannot export DXF."); // DEBUG
                    Logger.Error($"No Flat-Pattern configuration found for {partTitle}. Cannot export DXF.");
                    exportErrors.Add($"No Flat-Pattern configuration found for {fileName}");
                    swModel.ShowConfiguration2(originalConfig);
                    return null;
                }

                Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Using configuration: {flatConfig}"); // DEBUG
                Logger.Info($"Using configuration: {flatConfig}");
                swModel.ShowConfiguration2(flatConfig);

                Feature flatPatternFeature = FindFeatureByType(swPart, "FlatPattern");
                if (flatPatternFeature == null)
                {
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Error: No 'FlatPattern' feature found in configuration '{flatConfig}' for {partTitle}."); // DEBUG
                    Logger.Error($"No 'FlatPattern' feature found in configuration '{flatConfig}' for {partTitle}.");
                    exportErrors.Add($"No 'FlatPattern' feature found for {fileName}");
                    swModel.ShowConfiguration2(originalConfig);
                    return null;
                }
                flatPatternFeature.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);

                // Hide all visible sketches in the flat pattern config
                HideAllVisibleSketchesInConfig(swModel);
                Debug.WriteLine("[DEBUG] Hid visible sketches in flat pattern config.");

                string thickness = GetSheetMetalThickness(swPart, originalConfig);
                if (string.IsNullOrEmpty(thickness))
                {
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Could not determine sheet metal thickness for {partTitle}. Skipping DXF export."); // DEBUG
                    Logger.Warning($"Could not determine sheet metal thickness for {partTitle}. Skipping DXF export.");
                    thicknessFailures.Add(fileName);
                    swModel.ShowConfiguration2(originalConfig);
                    return null;
                }
                Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Determined thickness: {thickness}"); // DEBUG
                Logger.Info($"Determined thickness: {thickness}");

                // Track DXF count by material and thickness
                string mat = GetCustomProperty(swModel, "Material");
                string mappedMaterial = MapMaterial(mat, partTitle);
                
                string key = $"{(mappedMaterial ?? mat)} | {thickness}";
                if (!materialThicknessCounts.ContainsKey(key))
                    materialThicknessCounts[key] = 0;
                materialThicknessCounts[key]++;

                string tempDrawingPath = Path.Combine(Path.GetTempPath(), fileName + "_tempDXF.SLDDRW");
                try
                {
                    string drwTemplate = settings.DrawingTemplatePath;
                    if (string.IsNullOrEmpty(drwTemplate) || !File.Exists(drwTemplate))
                    {
                        string msg = $"Drawing template not found or invalid: '{drwTemplate}'. Please check the path in settings.";
                        Debug.WriteLine($"[DEBUG] {msg}");
                        Logger.Error(msg);
                        throw new Exception(msg);
                    }

                    object newDoc = _swApp.NewDocument(drwTemplate, (int)swDwgPaperSizes_e.swDwgPaperAsize, 0, 0);
                    if (newDoc == null) throw new Exception("Failed to create new drawing document.");
                    swDraw = (ModelDoc2)newDoc;

                    DrawingDoc swDrawingDoc = swDraw as DrawingDoc;
                    if (swDrawingDoc == null) throw new Exception("Failed to get DrawingDoc from new document.");

                    // Set up the bend line layer first
                    string bendLayerName = !string.IsNullOrEmpty(settings.BendLineLayerName) 
                        ? settings.BendLineLayerName 
                        : "Bend";

                    // Create the layer with specified properties
                    bool layerResult = swDrawingDoc.CreateLayer2(
                        bendLayerName,
                        "Sheet‑metal bend lines",
                        ColorTranslator.ToWin32(Color.Blue),
                        (int)swLineStyles_e.swLineCENTER,
                        (int)swLineWeights_e.swLW_THIN,
                        true,   // visible
                        true    // printable
                    );

                    if (!layerResult)  // Check boolean result
                    {
                        Logger.Warning($"Failed to create bend line layer: {bendLayerName}");
                        throw new Exception($"Failed to create bend line layer: {bendLayerName}");
                    }

                    // Set as current layer
                    LayerMgr lyrMgr = (LayerMgr)swDraw.GetLayerManager();
                    if (lyrMgr != null)
                    {
                        lyrMgr.SetCurrentLayer(bendLayerName);
                    }

                    // Create flat pattern view with bend lines visible
                    SwView flatView = swDrawingDoc.CreateFlatPatternViewFromModelView3(partPath, flatConfig, 0.05, 0.05, 0, false, false);
                    if (flatView == null) throw new Exception("Failed to create flat pattern view in drawing.");
                    Logger.Info($"Created flat pattern view named: {flatView.Name}");

                    // Add zoom to fit after creating the view
                    if (!swDrawingDoc.ActivateView(flatView.Name)) Logger.Warning("Failed to activate view before zoom.");
                    swDraw.ViewZoomtofit2();
                    swDraw.GraphicsRedraw2();

                    // Ensure bend lines are on the correct layer
                    AssignBendLinesToLayer(swDrawingDoc, flatView);
                    swDraw.ForceRebuild3(true);
                    swDraw.GraphicsRedraw2();

                    // Set DXF export options before adding notes
                    SetDxfExportOptions(swDraw);

                    // Pass the flat view name to AddNotesToDrawing
                    AddNotesToDrawing(swDrawingDoc, swModel, originalConfig, thickness, flatView.Name);

                    int errors = 0;
                    int warnings = 0;
                    bool success = swDraw.Extension.SaveAs(outputPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                                                         (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);

                    if (!success)
                    {
                        Debug.WriteLine($"[DEBUG] Attempting Logger.Error: Failed to save DXF for {partTitle}. Errors: {errors}, Warnings: {warnings}"); // DEBUG
                        Logger.Error($"Failed to save DXF for {partTitle}. Errors: {errors}, Warnings: {warnings}");
                        exportErrors.Add($"Failed to save DXF for {fileName} (Error code: {errors})");
                        return null;
                    }
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Info: DXF successfully saved to: {outputPath}"); // DEBUG
                    Logger.Info($"DXF successfully saved to: {outputPath}");

                    return outputPath;
                }
                finally
                {
                    if (swDraw != null)
                    {
                        _swApp.CloseDoc(swDraw.GetTitle());
                    }
                    if (swModel != null && originalConfig != null)
                    {
                        swModel.ShowConfiguration2(originalConfig);
                    }
                }
            }
            catch (Exception ex)
            {
                // Use the variables declared outside the try block
                Debug.WriteLine($"[DEBUG] Attempting Logger.Error: Error processing part {partTitle}: {ex.Message}"); // DEBUG
                Logger.Error($"Error processing part {partTitle}: {ex.Message}", ex);
                exportErrors.Add($"Error processing {fileName}: {ex.Message}");
                return null;
            }
        }
        
        private string FindFlatPatternConfig(ModelDoc2 swModel)
        {
             if (swModel == null) return null;
             object configsObj = swModel.GetConfigurationNames();
             string[] configNames = configsObj as string[];
             if (configNames == null) return null;

             // Strategy: Prioritize known default name, then fallback to name convention, skip suppression check.
             string defaultConfigName = "DefaultSM-FLAT-PATTERN"; // Common default name
             string fallbackConfigName = null;

             foreach (string configName in configNames)
             {
                  // Check for exact default name first
                  if (configName.Equals(defaultConfigName, StringComparison.OrdinalIgnoreCase))
                  {
                       Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Found default flat pattern configuration: {configName}"); // DEBUG
                       Logger.Info($"Found default flat pattern configuration: {configName}");
                       return configName;
                  }
                  
                  // Check for fallback names containing FLAT or PATTERN
                  if (fallbackConfigName == null && (configName.ToUpper().Contains("FLAT") || configName.ToUpper().Contains("PATTERN")))
                  {
                       fallbackConfigName = configName;
                       Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Found potential flat pattern configuration by name convention: {configName}"); // DEBUG
                       // Don't return immediately, keep looking for the default name
                  }
             }

             // If default wasn't found, return the fallback if one was found
             if (fallbackConfigName != null)
             {
                  Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Using fallback configuration: {fallbackConfigName}"); // DEBUG
                  Logger.Info($"Using fallback configuration: {fallbackConfigName}");
                  return fallbackConfigName;
             }

             // If no flat pattern config found, use the active configuration as a temporary fallback
             string activeConfig = swModel.ConfigurationManager?.ActiveConfiguration?.Name;
             if (!string.IsNullOrEmpty(activeConfig))
             {
                  Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: No flat pattern config found, using active config as fallback: {activeConfig}"); // DEBUG
                  Logger.Warning($"No flat pattern config found, using active config as fallback: {activeConfig}");
                  return activeConfig;
             }

             Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Could not find a suitable flat pattern configuration (checked default and name convention) for {swModel.GetTitle()}."); // DEBUG
             Logger.Warning($"Could not find a suitable flat pattern configuration (checked default and name convention) for {swModel.GetTitle()}.");
             return null;
        }

        private Feature FindFeatureByType(PartDoc swPart, string typeName)
        {
             ModelDoc2 model = swPart as ModelDoc2;
             Feature feat = model.FirstFeature() as Feature;
             while (feat != null)
             {
                 if (feat.GetTypeName2() == typeName)
                 {
                     return feat;
                 }
                 feat = feat.GetNextFeature() as Feature;
             }
             return null;
        }

        private string GetSheetMetalThickness(PartDoc swPart, string configName)
        {
            ModelDoc2 swModel = swPart as ModelDoc2;
            string partPath = swModel.GetPathName();
            string partTitle = swModel.GetTitle();
            
            // Log start of thickness check
            Logger.Info($"Checking sheet metal thickness for {partTitle} ({partPath}) in config {configName}");

            CustomPropertyManager propMgr = swModel.Extension.CustomPropertyManager[configName];
            if (propMgr == null)
            {
                Logger.Error($"ERROR: Failed to get CustomPropertyManager for {partTitle} config {configName}");
                return null;
            }

            string val = "";
            string resVal = "";
            bool wasRes = false;
            bool wasLinked = false;

            // Check custom property first
            Logger.Info($"Checking 'Sheet Metal Thickness' custom property for {partTitle}");
            
            int ret = propMgr.Get6("Sheet Metal Thickness", true, out val, out resVal, out wasRes, out wasLinked);
            
            Logger.Info($"Property check results for {partTitle}: Return code: {ret}, Raw value: '{val}', Resolved value: '{resVal}', Was resolved: {wasRes}, Is linked: {wasLinked}");

            // If we have a valid resolved value that's not an equation
            if (ret == 1 && wasRes && !string.IsNullOrEmpty(resVal) && !resVal.Contains("Thickness@"))
            {
                Logger.Info($"Using existing thickness value for {partTitle}: {resVal}");
                return resVal;
            }

            // Get thickness from feature
            Logger.Info($"Checking SheetMetal feature for {partTitle}");

            Feature swSMFeat = FindFeatureByType(swPart, "SheetMetal");
            if (swSMFeat != null)
            {
                SheetMetalFeatureData swSMData = swSMFeat.GetDefinition() as SheetMetalFeatureData;
                if (swSMData != null)
                {
                    double thicknessMeters = swSMData.Thickness;
                    double thicknessInches = Math.Round(thicknessMeters * 39.3701, 4);
                    string formattedThickness = thicknessInches.ToString("0.0000");

                    Logger.Info($"Found thickness in feature for {partTitle}: Meters: {thicknessMeters}, Inches: {thicknessInches}, Formatted: {formattedThickness}");

                    // Update the custom property
                    Logger.Info($"Updating thickness custom property for {partTitle}");

                    propMgr.Add3("Sheet Metal Thickness", 
                               (int)swCustomInfoType_e.swCustomInfoText, 
                               formattedThickness, 
                               (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

                    // Also update these properties to match Ben's macro
                    // BEN ROLAND - 2024-07-26: Commented out next line to use existing Part Number property instead of filename.
                    // Original behavior (to match Ben's macro) was: UpdateCustomProperty(propMgr, "Part Number", partTitle.Replace(".SLDPRT", ""));
                    UpdateCustomProperty(propMgr, "Description", GetCustomPropertyValue(propMgr, "Description"));
                    UpdateCustomProperty(propMgr, "Revision", GetCustomPropertyValue(propMgr, "Revision"));
                    UpdateCustomProperty(propMgr, "Material", GetCustomPropertyValue(propMgr, "Material"));
                    UpdateCustomProperty(propMgr, "Shop Route", GetCustomPropertyValue(propMgr, "Shop Route"));

                    Logger.Info($"Successfully updated all properties for {partTitle}");

                    return formattedThickness;
                }
                else
                {
                    Logger.Error($"ERROR: Could not get SheetMetalFeatureData for {partTitle}");
                }
            }
            else
            {
                Logger.Warning($"ERROR: No SheetMetal feature found in {partTitle}");
            }

            Logger.Warning($"WARNING: Could not determine thickness for {partTitle}");
            return null;
        }

        private string GetCustomPropertyValue(CustomPropertyManager propMgr, string propName)
        {
            string val = "", resVal = "";
            bool wasRes = false, wasLinked = false;
            int ret = propMgr.Get6(propName, true, out val, out resVal, out wasRes, out wasLinked);
            return wasRes ? resVal : val;
        }

        private void UpdateCustomProperty(CustomPropertyManager propMgr, string propName, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                propMgr.Add3(propName, (int)swCustomInfoType_e.swCustomInfoText, value,
                            (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                
                Logger.Info($"Updated {propName}: {value}");
            }
        }

        /// <summary>
        /// Checks if a part is a sheet metal part by looking for the "SheetMetal" feature.
        /// </summary>
        public static bool IsSheetMetalPart(PartDoc part)
        {
            if (part == null) return false;

            ModelDoc2 model = part as ModelDoc2;
            Feature feat = model.FirstFeature() as Feature;

            while (feat != null)
            {
                string typeName = feat.GetTypeName2();
                if (typeName == "SheetMetal" || typeName == "FlatPattern")
                {
                     Debug.WriteLine($"[DEBUG] Part {model.GetTitle()} identified as sheet metal (Feature: {typeName})"); // DEBUG
                     Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Part {model.GetTitle()} identified as sheet metal (Feature: {typeName})"); // DEBUG
                     Logger.Info($"Part {model.GetTitle()} identified as sheet metal (Feature: {typeName})");
                    return true;
                }
                feat = feat.GetNextFeature() as Feature;
            }
             Debug.WriteLine($"[DEBUG] Part {model.GetTitle()} NOT identified as sheet metal."); // DEBUG
             Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Part {model.GetTitle()} NOT identified as sheet metal."); // DEBUG
             Logger.Info($"Part {model.GetTitle()} NOT identified as sheet metal.");
            return false;
        }

        private void HideAllVisibleSketchesInConfig(ModelDoc2 swModel)
        {
            if (swModel == null) return;
            Debug.WriteLine("[DEBUG] Hiding user sketches (not bend lines or flat pattern)...");
            bool changed = false;
            Feature feat = swModel.FirstFeature() as Feature;
            while (feat != null)
            {
                if (feat.GetTypeName2() == "ProfileFeature")
                {
                    string name = feat.Name ?? string.Empty;
                    // Do NOT hide if name contains 'Bend' or 'Flat' (case-insensitive)
                    if (name.IndexOf("bend", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("flat", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        feat = feat.GetNextFeature() as Feature;
                        continue;
                    }
                    // Hide user sketch
                    if (feat.Select2(false, -1))
                    {
                        swModel.BlankSketch();
                        swModel.ClearSelection2(true);
                        changed = true;
                    }
                }
                feat = feat.GetNextFeature() as Feature;
            }
            if (changed) swModel.GraphicsRedraw2();
            Debug.WriteLine("[DEBUG] Finished hiding sketches.");
        }

        private bool IsLaserCuttingPart(ModelDoc2 doc)
        {
            if (!settings.CheckForLaser) 
            {
                Logger.Info($"Part {doc.GetTitle()} automatically passed laser check (CheckForLaser setting is off)");
                return true; // If checkbox is not checked, always return true
            }
            
            string shopRoute = GetCustomProperty(doc, "Shop Route");
            if (string.IsNullOrEmpty(shopRoute)) 
            {
                Logger.Warning($"Part {doc.GetTitle()} failed laser check - Shop Route is empty");
                return false;
            }
            
            bool hasLaser = shopRoute.IndexOf("Laser Cutting", StringComparison.OrdinalIgnoreCase) >= 0;
            if (hasLaser)
            {
                Logger.Info($"Part {doc.GetTitle()} passed laser check - Shop Route contains 'Laser Cutting': {shopRoute}");
            }
            else
            {
                Logger.Warning($"Part {doc.GetTitle()} failed laser check - Shop Route does not contain 'Laser Cutting': {shopRoute}");
            }
            return hasLaser;
        }

        private void ProcessAssemblyComponentsRecursively(Component2 swComp, string outputFolder, ref int sheetMetalPartsFound, ref int totalProcessed, List<string> processedFiles)
        {
            // Add a static or class-level HashSet to track processed part paths
            if (_processedPartPaths == null)
                _processedPartPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            object[] children = swComp.GetChildren() as object[];
            if (children != null)
            {
                foreach (Component2 child in children)
                {
                    // Check if the component path has already been processed
                    string componentPath = child.GetPathName();
                    
                    // Skip if we've already processed this file path
                    if (!string.IsNullOrEmpty(componentPath) && _processedPartPaths.Contains(componentPath))
                    {
                        Logger.DebugLog($"Skipping already processed part: {componentPath}");
                        continue;
                    }

                    // Get component load state
                    int loadState = -1;
                    bool wasLightweight = false;
                    
                    try
                    {
                        // Check if component is in lightweight state
                        loadState = child.GetSuppression();
                        wasLightweight = (loadState == (int)swComponentSuppressionState_e.swComponentLightweight);
                        
                        if (wasLightweight)
                        {
                            Logger.Info($"Found lightweight component: {componentPath ?? child.Name2}");
                            
                            // Try to fully load the component
                            int loadResult = child.SetSuppression2((int)swComponentSuppressionState_e.swComponentResolved);
                            if (loadResult != (int)swComponentSuppressionState_e.swComponentResolved)
                            {
                                Logger.Warning($"Failed to fully load lightweight component: {componentPath ?? child.Name2}, result code: {loadResult}");
                            }
                            else
                            {
                                Logger.Info($"Successfully loaded lightweight component: {componentPath ?? child.Name2}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error checking/changing component state: {ex.Message}");
                    }
                    
                    // Get the model document (now hopefully fully loaded)
                    ModelDoc2 childDoc = null;
                    try
                    {
                        childDoc = child.GetModelDoc2() as ModelDoc2;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error getting ModelDoc2 for component: {ex.Message}");
                    }
                    
                    if (childDoc != null && childDoc.GetType() == (int)swDocumentTypes_e.swDocPART)
                    {
                        string partPath = childDoc.GetPathName();
                        if (!string.IsNullOrEmpty(partPath))
                        {
                            // Track this file path as processed
                            _processedPartPaths.Add(partPath);
                            
                            try
                            {
                                if (IsSheetMetalPart(childDoc as PartDoc))
                                {
                                    if (IsLaserCuttingPart(childDoc))
                                    {
                                        sheetMetalPartsFound++;
                                        string resultPath = ProcessSheetMetalPart(childDoc as PartDoc, outputFolder);
                                        if (!string.IsNullOrEmpty(resultPath))
                                        {
                                            totalProcessed++;
                                            processedFiles.Add(resultPath);
                                        }
                                    }
                                    else
                                    {
                                        ignoredParts.Add(childDoc.GetTitle());
                                        Logger.Info($"Part {childDoc.GetTitle()} ignored - not marked for laser cutting");
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Error processing potential sheet metal part: {ex.Message}");
                            }
                            finally
                            {
                                // Return to lightweight state if it was originally lightweight
                                if (wasLightweight)
                                {
                                    try
                                    {
                                        int restoreResult = child.SetSuppression2((int)swComponentSuppressionState_e.swComponentLightweight);
                                        Logger.DebugLog($"Returned component to lightweight state: {restoreResult == (int)swComponentSuppressionState_e.swComponentLightweight}");
                                    }
                                    catch (Exception ex)
                                    {
                                        Logger.Error($"Error returning component to lightweight state: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                    
                    // Recursively process children
                    ProcessAssemblyComponentsRecursively(child, outputFolder, ref sheetMetalPartsFound, ref totalProcessed, processedFiles);
                }
            }
        }

        private string GetCustomProperty(ModelDoc2 doc, string propName)
        {
            if (doc == null) return "";
            string configName = doc.ConfigurationManager?.ActiveConfiguration?.Name ?? "";
            CustomPropertyManager custPropMgr = doc.Extension?.CustomPropertyManager[configName];
            return GetCustomPropertyValue(custPropMgr, propName);
        }

        private void SetDxfExportOptions(ModelDoc2 swDraw)
        {
            Logger.DebugLog("SetDxfExportOptions START");

            if (_swApp == null ||
                swDraw == null ||
                swDraw.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                Logger.Error("Invalid input for SetDxfExportOptions.");
                return;
            }

            try
            {
                // 1 – Force classic R12 format (shop floor friendly)
                _swApp.SetUserPreferenceIntegerValue(
                    (int)swUserPreferenceIntegerValue_e.swDxfOutputFormat,
                    (int)swDxfFormat_e.swDxfFormat_R12);

                // 2 – ***CRITICAL*** Disable custom mapping so SW keeps native layers
                _swApp.SetUserPreferenceToggle(
                    (int)swUserPreferenceToggle_e.swDxfMapping,
                    false);

                // 3 – Export hidden layers (bend‑line layer is hidden if view suppressed)
                _swApp.SetUserPreferenceToggle(
                    (int)swUserPreferenceToggle_e.swDXFExportHiddenLayersOn,
                    true);

                Logger.Info("DXF export options configured (mapping OFF – preserve drawing layers).");
            }
            catch (Exception ex)
            {
                Logger.Error("Error setting DXF export options", ex);
            }
            Logger.DebugLog("SetDxfExportOptions END");
        }

/// <summary>
/// Moves every bend-line sketch segment in the supplied flat-pattern drawing view onto the designated layer.
/// </summary>
private void AssignBendLinesToLayer(DrawingDoc swDraw, SwView flatView)
{
    if (swDraw == null || flatView == null || !flatView.IsFlatPatternView())
    {
        Logger.Warning("Invalid parameters for AssignBendLinesToLayer");
        return;
    }

    try
    {
        string bendLayerName = !string.IsNullOrEmpty(settings.BendLineLayerName) 
            ? settings.BendLineLayerName 
            : "Bend";

        // Get the layer manager
        ModelDoc2 swModel = swDraw as ModelDoc2;
        if (swModel == null) return;
        LayerMgr layerMgr = (LayerMgr)swModel.GetLayerManager();
        if (layerMgr == null) return;

        // Ensure the bend layer exists
        Layer bendLayer = (Layer)layerMgr.GetLayer(bendLayerName);
        if (bendLayer == null)
        {
            int layerResult = layerMgr.AddLayer(
                bendLayerName, 
                "Bend Lines Layer", 
                255, // Blue
                (int)swLineStyles_e.swLineCENTER,
                (int)swLineWeights_e.swLW_THIN);
            if (layerResult != 1) return;
            bendLayer = (Layer)layerMgr.GetLayer(bendLayerName);
        }

        // Get the sketch from the flat pattern view
        Sketch sketch = flatView.GetSketch() as Sketch;
        if (sketch == null) return;
        object[] segments = sketch.GetSketchSegments() as object[];
        if (segments == null || segments.Length == 0) return;

        int processedCount = 0;
        foreach (object segObj in segments)
        {
            SketchSegment seg = segObj as SketchSegment;
            if (seg == null) continue;

            // Just set the layer for each segment (API-compliant)
            seg.Layer = bendLayerName;
            processedCount++;
        }
        Logger.Info($"Successfully assigned {processedCount} bend lines to layer: {bendLayerName}");
    }
    catch (Exception ex)
    {
        Logger.Error($"Error in AssignBendLinesToLayer: {ex.Message}");
    }
}
        #endregion
    }
} 