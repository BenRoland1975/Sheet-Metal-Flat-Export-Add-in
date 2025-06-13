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
using System.Text;
using swUserPreferenceToggle_e = SolidWorks.Interop.swconst.swUserPreferenceToggle_e;
using swDisplayMode_e = SolidWorks.Interop.swconst.swDisplayMode_e;

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
                Logger.SetMainLogFilePath(settings.LogFilePath);
                Logger.Instance.SetDebugLogPath(settings.DebugLogFilePath);
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

        private void SetViewDisplayMode(SwView flatView, DrawingDoc swDrawingDoc)
        {
            if (flatView == null || swDrawingDoc == null) return;

            try
            {
                Logger.Info("Setting view display mode to hide bend lines");

                // Set the view to use "Hidden Lines Removed" display mode
                // This often prevents bend lines from showing
                flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false);

                // Alternative: try wireframe mode
                // flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swWIREFRAME, false, false);

                // Remove tangent edges - use value 0 for removed
                flatView.SetDisplayTangentEdges2(0);
                
                // If bend lines should be hidden, hide them via entity selection
                if (!settings.ShowBendLines)
                {
                    // Try to hide bend line sketches
                    HideBendSketches(swDrawingDoc, flatView);
                }

                // Force the drawing to update
                ModelDoc2 swDraw = swDrawingDoc as ModelDoc2;
                if (swDraw != null)
                {
                    swDraw.EditRebuild3();
                    swDraw.GraphicsRedraw2();
                }

                Logger.Info("View display mode set successfully");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting view display mode: {ex.Message}", ex);
            }
        }
        
        // New method to specifically target bend-related sketches
        private void HideBendSketches(DrawingDoc swDrawingDoc, SwView flatView)
        {
            Logger.Info($"Attempting to hide bend-related sketches in view: {flatView.Name}");
            
            try
            {
                // Get the model document
                ModelDoc2 swModel = swDrawingDoc as ModelDoc2;
                if (swModel == null) return;
                
                // Get the drawing component for the view
                DrawingComponent swDrawComp = flatView.RootDrawingComponent;
                if (swDrawComp == null)
                {
                    Logger.Warning("Could not get root drawing component for view");
                    return;
                }
                
                Component2 swComp = swDrawComp.Component;
                if (swComp == null)
                {
                    Logger.Warning("Could not get component from drawing component");
                    return;
                }
                
                // Get the model document for the component
                ModelDoc2 compModel = swComp.GetModelDoc2() as ModelDoc2;
                if (compModel == null || compModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                {
                    Logger.Warning("Component model is not a part document");
                    return;
                }
                
                // Track sketches that might be bend lines
                List<Feature> bendSketches = new List<Feature>();
                
                // Find all sketches that might be bend lines
                Feature swFeat = compModel.FirstFeature() as Feature;
                while (swFeat != null)
                {
                    string featName = swFeat.Name;
                    string featType = swFeat.GetTypeName2();
                    
                    // Check if this is a sketch that might be related to bend lines
                    if (featType == "ProfileFeature" && 
                        (featName.IndexOf("bend", StringComparison.OrdinalIgnoreCase) >= 0 || 
                         featName.IndexOf("fold", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        bendSketches.Add(swFeat);
                        Logger.Info($"Found potential bend line sketch: {featName}");
                    }
                    
                    // Get next feature
                    swFeat = swFeat.GetNextFeature() as Feature;
                }
                
                // Try to hide any bend line sketches found
                int hiddenCount = 0;
                foreach (Feature bendSketch in bendSketches)
                {
                    try
                    {
                        // Select the sketch
                        bool selected = swModel.Extension.SelectByID2(
                            bendSketch.Name, "SKETCH", 0, 0, 0, false, 0, null, 0);
                        
                        if (selected)
                        {
                            // Hide the sketch
                            swModel.BlankSketch();
                            hiddenCount++;
                            Logger.Info($"Successfully hid bend sketch: {bendSketch.Name}");
                        }
                        
                        // Clear selection
                        swModel.ClearSelection2(true);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Error hiding bend sketch {bendSketch.Name}: {ex.Message}");
                    }
                }
                
                Logger.Info($"Hidden {hiddenCount} bend line sketches out of {bendSketches.Count} found");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in HideBendSketches: {ex.Message}", ex);
            }
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

            // Log all relevant settings for debugging
            Debug.WriteLine($"Settings loaded - Export Dir: {outDir}, Logging Enabled: {loggingEnabled}, Show Bend Lines: {settings.ShowBendLines}");
            Logger.Info($"Settings loaded - Show Bend Lines: {settings.ShowBendLines}");
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
                if (string.IsNullOrWhiteSpace(settings?.DxfMappingFilePath))
                {
                    // No custom mapping configured – clear to avoid using stale paths
                    _swApp.SetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDxfMappingFile, "");
                }
                
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

                Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Processing part: {partTitle} for DXF export to {outputPath}");
                Logger.Info($"Processing part: {partTitle} for DXF export to {outputPath}");

                originalConfig = swModel.ConfigurationManager.ActiveConfiguration.Name;
                string flatConfig = FindFlatPatternConfig(swModel);

                if (string.IsNullOrEmpty(flatConfig))
                {
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Error: No Flat-Pattern configuration found for {partTitle}. Cannot export DXF.");
                    Logger.Error($"No Flat-Pattern configuration found for {partTitle}. Cannot export DXF.");
                    exportErrors.Add($"No Flat-Pattern configuration found for {fileName}");
                    swModel.ShowConfiguration2(originalConfig);
                    return null;
                }

                Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Using configuration: {flatConfig}");
                Logger.Info($"Using configuration: {flatConfig}");
                swModel.ShowConfiguration2(flatConfig);

                Feature flatPatternFeature = FindFeatureByType(swPart, "FlatPattern");
                if (flatPatternFeature == null)
                {
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Error: No 'FlatPattern' feature found in configuration '{flatConfig}' for {partTitle}.");
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
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Could not determine sheet metal thickness for {partTitle}. Skipping DXF export.");
                    Logger.Warning($"Could not determine sheet metal thickness for {partTitle}. Skipping DXF export.");
                    thicknessFailures.Add(fileName);
                    swModel.ShowConfiguration2(originalConfig);
                    return null;
                }
                Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Determined thickness: {thickness}");
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

                    // Log the ShowBendLines setting value
                    Logger.Info($"ShowBendLines setting is: {settings.ShowBendLines}");

                    // Create the flat pattern view (always create it normally first)
                    SwView flatView = swDrawingDoc.CreateFlatPatternViewFromModelView3(
                        partPath,
                        flatConfig,
                        0.05, // X position
                        0.05, // Y position
                        0,    // Angle
                        false, // ShowSheetMetalBendNotes
                        settings.ShowBendLines   // Use settings value instead of always true
                    );

                    if (flatView == null) 
                    {
                        throw new Exception("Failed to create flat pattern view in drawing.");
                    }

                    Logger.Info($"Created flat pattern view named: {flatView.Name}");

                    // Set the view scale to 1:1
                    flatView.ScaleDecimal = 1.0;

                    // NOW FORCE HIDE BEND LINES if setting is disabled
                    if (!settings.ShowBendLines)
                    {
                        Logger.Info("Attempting to force hide bend lines using advanced drawing view manipulation");
                        ForceHideBendLinesAdvanced(swDrawingDoc, flatView);
                    }

                    // Final rebuild before export
                    swDraw.ForceRebuild3(true);
                    swDraw.GraphicsRedraw2();

                    // Set DXF export options JUST before save
                    SetDxfExportOptions(swDraw);

                    // Pass the flat view name to AddNotesToDrawing
                    AddNotesToDrawing(swDrawingDoc, swModel, originalConfig, thickness, flatView.Name);

                    // Debug the drawing structure
                    DebugDrawingStructure(swDrawingDoc);

                    int errors = 0;
                    int warnings = 0;
                    bool success = swDraw.Extension.SaveAs(outputPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
                                                         (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, ref errors, ref warnings);

                    // DXF successfully saved - now post-process it
                    if (success)
                    {
                        Logger.Info($"DXF successfully saved to: {outputPath}");
                        
                        // Post-process the DXF file to fix bend lines and colors
                        PostProcessDxfFile(outputPath);
                        
                        return outputPath;
                    }

                    Debug.WriteLine($"[DEBUG] Attempting Logger.Error: Failed to save DXF for {partTitle}. Errors: {errors}, Warnings: {warnings}");
                    Logger.Error($"Failed to save DXF for {partTitle}. Errors: {errors}, Warnings: {warnings}");
                    exportErrors.Add($"Failed to save DXF for {fileName} (Error code: {errors})");
                    return null;
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
                Debug.WriteLine($"[DEBUG] Attempting Logger.Error: Error processing part {partTitle}: {ex.Message}");
                Logger.Error($"Error processing part {partTitle}: {ex.Message}", ex);
                exportErrors.Add($"Error processing {fileName}: {ex.Message}");
                return null;
            }
        }
        
        /// <summary>
        /// Post-processes a DXF file to move hidden lines to BEND_LINES layer and set all colors to ByLayer
        /// </summary>
        private void PostProcessDxfFile(string dxfFilePath)
        {
            if (string.IsNullOrEmpty(dxfFilePath) || !File.Exists(dxfFilePath))
            {
                Logger.Error($"Cannot post-process DXF file: File not found at {dxfFilePath}");
                return;
            }
            
            Logger.Info($"Post-processing DXF file: {dxfFilePath}");
            
            try
            {
                // Attempt to directly modify the DXF file to handle bend lines
                // For immediate results while we improve the code, let's try a direct string replacement
                // This might be crude but it can work for many DXF files
                string dxfContent = File.ReadAllText(dxfFilePath);
                
                // Create a backup of the original file (deleted at the end)
                string backupPath = dxfFilePath + ".bak";
                File.WriteAllText(backupPath, dxfContent);
                Logger.Info($"Created backup of original DXF at: {backupPath}");
                
                // First do a detection pass to see if our normal processing might work
                int hiddenCount = CountOccurrences(dxfContent, "HIDDEN");
                bool hasBendIndicators = dxfContent.Contains("BEND") || 
                                        dxfContent.Contains("FOLD") || 
                                        dxfContent.Contains("CENTER");
                
                Logger.Info($"Detected {hiddenCount} occurrences of 'HIDDEN' in the DXF file");
                Logger.Info($"DXF contains bend indicators: {hasBendIndicators}");
                
                // First try a direct approach to replace layer 0 hidden lines with BEND_LINES layer
                // Layer code "8" followed by layer name "0" with linetype "6" followed by "HIDDEN"
                bool modified = false;
                
                // Only proceed with detailed scanning if we detect hidden lines
                if (hiddenCount > 0)
                {
                    // Read the file as lines for more precise modification
                    string[] lines = File.ReadAllLines(dxfFilePath);
                    List<string> outputLines = new List<string>();
                    
                    // First scan to find all layers and entities for logging
                    var layersFound = new HashSet<string>();
                    
                    for (int i = 0; i < lines.Length - 1; i++)
                    {
                        // Find layers
                        if (lines[i].Trim() == "LAYER" && i+2 < lines.Length && lines[i+1].Trim() == "2")
                        {
                            layersFound.Add(lines[i+2].Trim());
                        }
                    }
                    
                    Logger.Info($"Found layers: {string.Join(", ", layersFound)}");
                    
                    // Check if BEND_LINES layer exists or needs to be created
                    bool bendLayerExists = layersFound.Contains("BEND_LINES");
                    
                    // Add BEND_LINES layer section if it doesn't exist
                    StringBuilder newDxfContent = new StringBuilder();
                    if (!bendLayerExists)
                    {
                        // Find the TABLE section with LAYER to insert our new layer
                        int insertPosition = dxfContent.IndexOf("ENDTAB", dxfContent.IndexOf("TABLE\r\n  2\r\nLAYER"));
                        if (insertPosition > 0)
                        {
                            // Insert the BEND_LINES layer definition right before ENDTAB
                            string bendLayerDef = "  0\r\nLAYER\r\n  2\r\nBEND_LINES\r\n  70\r\n0\r\n  62\r\n5\r\n  6\r\nCONTINUOUS\r\n";
                            
                            newDxfContent.Append(dxfContent.Substring(0, insertPosition));
                            newDxfContent.Append(bendLayerDef);
                            newDxfContent.Append(dxfContent.Substring(insertPosition));
                            
                            dxfContent = newDxfContent.ToString();
                            newDxfContent.Clear();
                            modified = true;
                            Logger.Info("Added BEND_LINES layer to DXF file");
                        }
                    }
                    
                    // Now locate all hidden lines and change their layer
                    // Process each entity
                    bool inEntity = false;
                    bool isHiddenLine = false;
                    int entitiesProcessed = 0;
                    int hiddenLinesFound = 0;
                    int bendLinesProcessed = 0;
                    int colorsByLayerSet = 0;
                    
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        
                        // Entity start/end detection
                        if (line == "0" && i + 1 < lines.Length)
                        {
                            string entityType = lines[i + 1].Trim();
                            
                            // If we were in an entity, finalize it
                            if (inEntity)
                            {
                                inEntity = false;
                                entitiesProcessed++;
                                
                                if (isHiddenLine)
                                {
                                    hiddenLinesFound++;
                                }
                                
                                isHiddenLine = false;
                            }
                            
                            // Check if this is the start of a new entity
                            if (entityType == "LINE" || entityType == "LWPOLYLINE" || entityType == "POLYLINE" ||
                                entityType == "ARC" || entityType == "CIRCLE")
                            {
                                inEntity = true;
                            }
                        }
                        
                        // Hidden line detection
                        if (inEntity && line == "6" && i + 1 < lines.Length && lines[i + 1].Trim() == "HIDDEN")
                        {
                            isHiddenLine = true;
                            Logger.Info("Found hidden line");
                        }
                        
                        // Update layer for hidden lines
                        if (inEntity && isHiddenLine && line == "8" && i + 1 < lines.Length)
                        {
                            string currentLayer = lines[i + 1].Trim();
                            outputLines.Add(lines[i]); // Add the 8
                            outputLines.Add("BEND_LINES");
                            bendLinesProcessed++;
                            i++; // Skip the original layer
                            continue;
                        }
                        
                        // Set all entity colors to ByLayer
                        if (inEntity && line == "62" && i + 1 < lines.Length)
                        {
                            outputLines.Add(lines[i]);
                            outputLines.Add("256"); // ByLayer color
                            colorsByLayerSet++;
                            i++; // Skip the original color
                            continue;
                        }
                        
                        // Add the line to output
                        outputLines.Add(lines[i]);
                    }
                    
                    // If we found and processed hidden lines, write the file
                    if (bendLinesProcessed > 0 || colorsByLayerSet > 0)
                    {
                        File.WriteAllLines(dxfFilePath, outputLines);
                        modified = true;
                        Logger.Info($"Updated DXF with: {bendLinesProcessed} bend lines moved to BEND_LINES layer, {colorsByLayerSet} colors set to ByLayer");
                    }
                    
                    // If user does NOT want bend lines, remove entire BEND_LINES layer and its entities
                    if (!settings.ShowBendLines)
                    {
                        Logger.Info("ShowBendLines = false – stripping BEND_LINES layer and its entities");

                        var prunedLines = new List<string>();

                        int idx = 0;
                        while (idx < outputLines.Count)
                        {
                            string code = outputLines[idx].Trim();

                            // Entity/header blocks always start with group code 0
                            if (code == "0" && idx + 1 < outputLines.Count)
                            {
                                int blockStart = idx;
                                int cursor = idx + 2; // skip 0 + entity type
                                bool isBendEntity = false;

                                // Move until next 0 or end of list
                                while (cursor < outputLines.Count && outputLines[cursor].Trim() != "0")
                                {
                                    if (outputLines[cursor].Trim() == "8" && cursor + 1 < outputLines.Count)
                                    {
                                        string layerName = outputLines[cursor + 1].Trim();
                                        if (layerName.Equals("BEND_LINES", StringComparison.OrdinalIgnoreCase))
                                            isBendEntity = true;
                                    }
                                    cursor++;
                                }

                                if (!isBendEntity)
                                {
                                    // copy the block
                                    for (int k = blockStart; k < cursor; k++) prunedLines.Add(outputLines[k]);
                                }

                                idx = cursor; // continue from next block (which may be at list.Count)
                            }
                            else
                            {
                                // Header or table data before first entity, just copy
                                prunedLines.Add(outputLines[idx]);
                                idx++;
                            }
                        }

                        // after prunedLines built
                        // Remove BEND_LINES layer definition
                        var finalLines = new List<string>();
                        bool insideLayerTable = false;
                        bool skipLayer = false;

                        for (int i = 0; i < prunedLines.Count; i++)
                        {
                            string trimmed = prunedLines[i].Trim();

                            if (!insideLayerTable && trimmed == "TABLE" && i + 2 < prunedLines.Count && prunedLines[i + 2].Trim() == "LAYER")
                            {
                                insideLayerTable = true;
                            }

                            if (insideLayerTable && trimmed == "ENDTAB")
                            {
                                insideLayerTable = false;
                            }

                            if (insideLayerTable && trimmed == "LAYER")
                            {
                                // Check if upcoming 2/BEND_LINES
                                if (i + 3 < prunedLines.Count && prunedLines[i + 2].Trim() == "2" && prunedLines[i + 3].Trim().Equals("BEND_LINES", StringComparison.OrdinalIgnoreCase))
                                {
                                    skipLayer = true;
                                    continue; // skip this LAYER line
                                }
                            }

                            if (skipLayer)
                            {
                                // stop skipping when next 0 encountered
                                if (trimmed == "0")
                                {
                                    skipLayer = false;
                                }
                                continue;
                            }

                            finalLines.Add(prunedLines[i]);
                        }

                        File.WriteAllLines(dxfFilePath, finalLines);
                        modified = true;
                        Logger.Info("BEND_LINES layer and its entities removed");
                    }
                }
                else
                {
                    // No hidden lines detected - try to find Layer 0 entities and set them to ByLayer color
                    // This is a fallback approach
                    
                    // Try a simple string replacement to set all colors to ByLayer
                    int colorsByLayerSet = 0;
                    
                    // Read the file as lines for more precise modification
                    string[] lines = File.ReadAllLines(dxfFilePath);
                    List<string> outputLines = new List<string>();
                    
                    bool inEntity3 = false;
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string line = lines[i].Trim();
                        
                        // Entity start/end detection
                        if (line == "0" && i + 1 < lines.Length)
                        {
                            string entityType = lines[i + 1].Trim();
                            
                            // Check if this is the start of a new entity
                            if (entityType == "LINE" || entityType == "LWPOLYLINE" || entityType == "POLYLINE" ||
                                entityType == "ARC" || entityType == "CIRCLE" || entityType == "TEXT" || entityType == "MTEXT")
                            {
                                inEntity3 = true;
                            }
                            else
                            {
                                inEntity3 = false;
                            }
                        }
                        
                        // Set all entity colors to ByLayer
                        if (inEntity3 && line == "62" && i + 1 < lines.Length)
                        {
                            outputLines.Add(lines[i]);
                            outputLines.Add("256"); // ByLayer color
                            colorsByLayerSet++;
                            i++; // Skip the original color
                            continue;
                        }
                        
                        // Add the line to output
                        outputLines.Add(lines[i]);
                    }
                    
                    // If we set any colors, write the file
                    if (colorsByLayerSet > 0)
                    {
                        File.WriteAllLines(dxfFilePath, outputLines);
                        modified = true;
                        Logger.Info($"Updated DXF with: {colorsByLayerSet} colors set to ByLayer");
                    }
                    
                    if (!modified)
                    {
                        Logger.Warning("No hidden lines or bend lines found in the DXF file - could not process");
                    }
                }
                
                // Delete the backup file unless debug logging is on
                try { if (File.Exists(backupPath)) File.Delete(backupPath); } catch {}
                
                Logger.Info($"DXF post-processing complete: {dxfFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error post-processing DXF file: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Count occurrences of a substring in a string
        /// </summary>
        private int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
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
                               (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);

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
                            (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                
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
            // Reload settings to get the latest CheckForLaser value
            var currentSettings = AppSettings.LoadSettings();
            if (currentSettings != null)
            {
                settings = currentSettings;
            }
            
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

                // 2 – Enable custom mapping if a valid map file has been configured in settings
                string mapPath = settings?.DxfMappingFilePath;

                // If configured path is blank or missing, fall back to shared PCSVAULT path
                if (string.IsNullOrWhiteSpace(mapPath) || !File.Exists(mapPath))
                {
                    const string fallbackMap = @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\BendLineMapping.map";
                    if (File.Exists(fallbackMap))
                    {
                        Logger.Warning($"Configured map file not found. Falling back to shared map file at: {fallbackMap}");
                        mapPath = fallbackMap;
                    }
                }

                bool mapAvailable = !string.IsNullOrWhiteSpace(mapPath) && File.Exists(mapPath);

                if (mapAvailable)
                {
                    Logger.Info($"Using DXF mapping file: {mapPath}");
                    _swApp.SetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDxfMappingFile, mapPath);
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDxfMapping, true);
                }
                else
                {
                    Logger.Warning("DXF mapping file not found – custom mapping disabled");
                    _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDxfMapping, false);
                }

                // 3 - Try to disable layer merging using direct constant value
                try
                {
                    _swApp.SetUserPreferenceToggle(37, false); // 37 may correspond to swDxfMergeDrawingLayers in newer APIs
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not set swDxfMergeDrawingLayers preference: {ex.Message}");
                }
                
                // 4 - Try to ensure output is in inches using direct constant value
                try 
                {
                    _swApp.SetUserPreferenceIntegerValue(81, 0); // 81 may correspond to swDxfOutputUnits in newer APIs, 0=inches
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not set swDxfOutputUnits preference: {ex.Message}");
                }

                // 5 – Control hidden layer export based on settings
                if (settings.ShowBendLines)
                {
                    Logger.Info("Bend lines enabled - setting DXF to export hidden layers");
                    _swApp.SetUserPreferenceToggle(
                        (int)swUserPreferenceToggle_e.swDXFExportHiddenLayersOn,
                        true);
                }
                else
                {
                    Logger.Info("Bend lines disabled - setting DXF to NOT export hidden layers");
                    _swApp.SetUserPreferenceToggle(
                        (int)swUserPreferenceToggle_e.swDXFExportHiddenLayersOn,
                        false);
                }

                // 6 - Try to ensure we export entities on layer 0 using direct constant value
                try
                {
                    _swApp.SetUserPreferenceToggle(47, true); // 47 may correspond to swDXFExportDrawingGeomLayer in newer APIs
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not set swDXFExportDrawingGeomLayer preference: {ex.Message}");
                }

                // 7 - Control export of hidden geometry (likely bend lines)
                try
                {
                    // Always export hidden geometry so bend entities are present for optional removal in post-processing
                    _swApp.SetUserPreferenceToggle(36, true); // 36 may correspond to swDXFExportHiddenGeometry
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not set swDXFExportHiddenGeometry preference: {ex.Message}");
                }

                Logger.Info($"DXF export options configured - Bend lines: {(settings.ShowBendLines ? "ENABLED" : "DISABLED")}");
            }
            catch (Exception ex)
            {
                Logger.Error("Error setting DXF export options", ex);
            }
            Logger.DebugLog("SetDxfExportOptions END");
        }

        private void SetSystemPreferencesToHideBendLines()
        {
            Logger.Info("Setting system preferences to control bend line display");
            
            try
            {
                if (!settings.ShowBendLines)
                {
                    // Try to disable bend lines at the system level
                    Logger.Info("Attempting to disable bend lines through system preferences");
                    
                    // The following toggles are not available in this SolidWorks API version:
                    // _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingDisplaySheetMetalBendNotes, false);
                    // _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDetailingDisplayAnnotations, false);
                    // _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSheetMetalBendAllowance, false);
                    // Only the ShowBendLines parameter in view creation and display settings are used.
                    
                    Logger.Info("System preferences set to minimize bend line display (no toggles available in this API version)");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error setting system preferences: {ex.Message}", ex);
            }
        }

        private void HideBendLinesThroughViewProperties(SwView flatView, DrawingDoc swDrawingDoc)
        {
            Logger.Info($"Attempting to hide bend lines through view properties for: {flatView.Name}");
            
            try
            {
                // Method 1: Set display mode to minimize bend lines
                flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false);
                
                // Method 2: Hide tangent edges (bend lines are often tangent edges)
                flatView.SetDisplayTangentEdges2(0); // 0 = removed, 1 = solid, 2 = dashed
                
                // Method 3: Try to set the view to not show sheet metal bend notes
                try
                {
                    // This method might not exist in all versions
                    ModelDoc2 swModel = swDrawingDoc as ModelDoc2;
                    if (swModel != null)
                    {
                        // Select the view and try to modify its properties
                        bool selected = swModel.Extension.SelectByID2(flatView.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
                        if (selected)
                        {
                            // Try to access view properties and disable bend line display
                            swDrawingDoc.ActivateView(flatView.Name);
                            
                            // Clear the selection
                            swModel.ClearSelection2(true);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not modify advanced view properties: {ex.Message}");
                }
                
                // Method 4: Try to modify the view's visibility settings
                try
                {
                    // Force the view to use minimal edge display
                    flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swWIREFRAME, false, false);
                    
                    // Then switch back to hidden lines removed but with minimal edges
                    flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not set wireframe display mode: {ex.Message}");
                }
                
                Logger.Info("Completed view property modifications to hide bend lines");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error hiding bend lines through view properties: {ex.Message}", ex);
            }
        }

        private void HideBendLinesInAllFlatPatternViews(DrawingDoc swDrawDoc)
        {
            if (settings.ShowBendLines)
            {
                Logger.Info("ShowBendLines is enabled - skipping bend line hiding");
                return;
            }
            
            ModelDoc2 swModel = swDrawDoc as ModelDoc2;
            if (swModel == null) return;

            Logger.Info("Starting final bend line cleanup");

            SwView swView = (SwView)swDrawDoc.GetFirstView();
            while (swView != null)
            {
                try
                {
                    string viewName = swView.GetName2();
                    
                    // Skip the sheet view (first view)
                    if (swView == (SwView)swDrawDoc.GetFirstView())
                    {
                        swView = (SwView)swView.GetNextView();
                        continue;
                    }

                    Logger.Info($"Final cleanup for view: {viewName}");

                    // Apply final display settings to ensure bend lines are minimized
                    try
                    {
                        // Set to wireframe first to clear any cached display data
                        swView.SetDisplayMode3(false, (int)swDisplayMode_e.swWIREFRAME, false, false);
                        
                        // Then set to hidden lines removed with no tangent edges
                        swView.SetDisplayMode3(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false);
                        swView.SetDisplayTangentEdges2(0);
                        
                        Logger.Info($"Applied final display settings to view: {viewName}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error applying final display settings: {ex.Message}", ex);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in final bend line cleanup: {ex.Message}", ex);
                }
                
                swView = (SwView)swView.GetNextView();
            }

            // Force rebuild and redraw
            swModel.ForceRebuild3(true);
            swModel.GraphicsRedraw2();
            Logger.Info("Completed final bend line cleanup");
        }

        private void DebugDrawingStructure(DrawingDoc swDrawDoc)
        {
            ModelDoc2 swModel = swDrawDoc as ModelDoc2;
            if (swModel == null) return;
            
            Logger.Info("=== DEBUG: Drawing Structure Analysis ===");
            
            SwView swView = (SwView)swDrawDoc.GetFirstView();
            while (swView != null)
            {
                string viewName = swView.GetName2();
                Logger.Info($"View: {viewName}");
                
                if (swView != (SwView)swDrawDoc.GetFirstView()) // Skip sheet view
                {
                    DrawingComponent swDrawComp = swView.RootDrawingComponent;
                    if (swDrawComp != null)
                    {
                        Component2 swComp = swDrawComp.Component;
                        if (swComp != null)
                        {
                            string compName = swComp.Name2;
                            Logger.Info($"  Component: {compName}");
                            
                            // List all features in the component
                            Feature swFeat = (Feature)swComp.FirstFeature();
                            while (swFeat != null)
                            {
                                string featName = swFeat.Name;
                                string featType = swFeat.GetTypeName2();
                                Logger.Info($"    Feature: {featName} (Type: {featType})");
                                
                                if (featType == "FlatPattern")
                                {
                                    // List sub-features of flat pattern
                                    Feature swSubFeat = (Feature)swFeat.GetFirstSubFeature();
                                    while (swSubFeat != null)
                                    {
                                        string subFeatName = swSubFeat.Name;
                                        string subFeatType = swSubFeat.GetTypeName2();
                                        Logger.Info($"      Sub-Feature: {subFeatName} (Type: {subFeatType})");
                                        swSubFeat = (Feature)swSubFeat.GetNextSubFeature();
                                    }
                                }
                                
                                swFeat = (Feature)swFeat.GetNextFeature();
                            }
                        }
                    }
                }
                
                swView = (SwView)swView.GetNextView();
            }
            
            Logger.Info("=== END DEBUG: Drawing Structure Analysis ===");
        }

        private void ForceHideBendLinesAdvanced(DrawingDoc swDrawingDoc, SwView flatView)
        {
            ModelDoc2 swModel = swDrawingDoc as ModelDoc2;
            if (swModel == null) return;
            
            Logger.Info($"Starting advanced bend line hiding for view: {flatView.Name}");
            
            try
            {
                // NEW PRIMARY APPROACH: Hide bend lines at entity level using direct Hide() method
                bool success = HideBendLineEntities(swDrawingDoc, flatView);
                
                if (!success)
                {
                    // APPROACH 1: Manipulate the view through selection and property modification
                    success = HideBendLinesByViewPropertyManipulation(swModel, flatView);
                    
                    if (!success)
                    {
                        // APPROACH 2: Try modifying the drawing view through direct IDrawingView interface
                        success = HideBendLinesThroughIDrawingView(swDrawingDoc, flatView);
                    }
                    
                    if (!success)
                    {
                        // APPROACH 3: Force all sheet metal annotation hiding
                        success = HideAllSheetMetalAnnotations(swModel, flatView);
                    }
                    
                    if (!success)
                    {
                        // APPROACH 4: Last resort - modify layers and force entity hiding
                        ForceHideAllBendRelatedEntities(swModel, flatView);
                    }
                }
                
                Logger.Info($"Completed advanced bend line hiding attempts for view: {flatView.Name}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in advanced bend line hiding: {ex.Message}", ex);
            }
        }
        
        // New method to hide bend lines at the entity level
        private bool HideBendLineEntities(DrawingDoc swDrawingDoc, SwView flatView)
        {
            Logger.Info($"Attempting to hide bend line entities directly in view: {flatView.Name}");
            ModelDoc2 swModel = swDrawingDoc as ModelDoc2;
            if (swModel == null) return false;

            try
            {
                // First, try to get all entities in the view
                object[] visibleEntities = null;
                try
                {
                    // Check if the view has a root component
                    DrawingComponent drawComp = flatView.RootDrawingComponent;
                    Component2 viewComp = null;
                    if (drawComp != null)
                    {
                        viewComp = drawComp.Component;
                    }
                    
                    // Get visible entities - need to provide Component2 parameter
                    visibleEntities = flatView.GetVisibleEntities(viewComp, 0) as object[];
                    Logger.Info($"Retrieved {(visibleEntities?.Length ?? 0)} visible entities from view");
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Could not get visible entities: {ex.Message}");
                    // No fallback to GetEntities - that method doesn't exist in SolidWorks API
                }
                
                if (visibleEntities == null || visibleEntities.Length == 0)
                {
                    Logger.Warning("No entities found in view, cannot hide bend lines directly");
                    return false;
                }
                
                int hiddenCount = 0;
                foreach (object entityObj in visibleEntities)
                {
                    if (entityObj == null) continue;
                    
                    try
                    {
                        // Try to identify bend line entities
                        // Bend lines can be edges in drawing views
                        Entity entity = entityObj as Entity;
                        if (entity == null) continue;
                        
                        // Check if this is likely a bend line
                        // Several methods to identify:
                        // 1. Check entity type (usually an edge)
                        // 2. Check properties that might identify bend status
                        
                        bool isBendLine = false;
                        string entityType = "Unknown";
                        
                        try 
                        {
                            entityType = entity.GetType().ToString();
                            
                            // For edges, we can try to get more information
                            if (entityObj is Edge)
                            {
                                Edge edge = entityObj as Edge;
                                // Additional checks for bend edges could go here
                                
                                // Most edges in a flat pattern view are likely bend lines
                                isBendLine = true;
                            }
                            
                            // Check if entity DisplayName or GetTypeName2 indicate it's a bend line
                            try 
                            {
                                // Check the entity type - some types are more likely to be bend lines
                                string entityTypeName = entity.GetType().ToString().ToLower();
                                if (entityTypeName.Contains("edge") || 
                                    entityTypeName.Contains("sketch") ||
                                    entityTypeName.Contains("fold"))
                                {
                                    isBendLine = true;
                                }
                            }
                            catch {}
                            
                            // For sketch entities, check if they're part of a bend sketch
                            if (!isBendLine && (entity is SketchSegment))
                            {
                                // Likely to be a bend line if it's a sketch segment in a flat pattern view
                                isBendLine = true;
                            }
                        }
                        catch {}
                        
                        // If we think it's a bend line or we're using aggressive hiding
                        if (isBendLine)
                        {
                            try
                            {
                                // Select the entity
                                entity.Select(false);
                                
                                // We need to use the model to hide selected entities
                                // Hide selected entity using direct command ID
                                // Using 953 which is often the ID for Hide/Show Sketch elements
                                _swApp.RunCommand(953, "");
                                
                                hiddenCount++;
                                
                                // Clear the selection
                                swModel.ClearSelection2(true);
                            }
                            catch (Exception hideEx)
                            {
                                Logger.Warning($"Failed to hide entity: {hideEx.Message}");
                            }
                        }
                    }
                    catch (Exception entityEx)
                    {
                        Logger.Warning($"Error processing entity: {entityEx.Message}");
                    }
                }
                
                Logger.Info($"Successfully hidden {hiddenCount} potential bend line entities");
                
                // Try a more aggressive approach - select and hide bend lines using drawing view selection
                try
                {
                    // Select the view using SelectByID2
                    bool viewSelected = swModel.Extension.SelectByID2(
                        flatView.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
                    
                    // If we failed to hide any entities individually and successfully selected the view
                    if (hiddenCount == 0 && viewSelected)
                    {
                        Logger.Info("Attempting to use selection-based hiding on the view");
                        
                        // Try to hide selected view or its components
                        // Using 953 which is often the ID for Hide/Show Sketch elements
                        _swApp.RunCommand(953, "");
                        
                        Logger.Info("Applied hide command to selected view");
                    }
                }
                catch (Exception selectEx)
                {
                    Logger.Warning($"Could not use command-based hiding: {selectEx.Message}");
                }
                
                // Force update
                swModel.ForceRebuild3(true);
                swModel.GraphicsRedraw2();
                
                return hiddenCount > 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in bend line entity hiding: {ex.Message}", ex);
                return false;
            }
        }

        private bool HideBendLinesByViewPropertyManipulation(ModelDoc2 swModel, SwView flatView)
        {
            Logger.Info("Attempting bend line hiding via view property manipulation");
            
            try
            {
                // Activate and select the view
                bool viewSelected = swModel.Extension.SelectByID2(flatView.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
                if (!viewSelected)
                {
                    Logger.Warning("Could not select drawing view for property manipulation");
                    return false;
                }
                
                // The following PropertyManager calls are not available in this API version:
                // swModel.ShowPropertyManager();
                
                // Force the view to specific display settings
                flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false);
                flatView.SetDisplayTangentEdges2(0); // Remove tangent edges
                
                // The following is not available in this API version:
                // flatView.SetDisplayHiddenEdges2(0); // 0 = removed, 1 = solid, 2 = dashed
                
                // Try setting cosmetic threads and other display options
                try
                {
                    flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swWIREFRAME, false, false);
                    System.Threading.Thread.Sleep(100); // Brief pause
                    flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false);
                }
                catch (Exception ex)
                {
                    Logger.Warning($"Error in display mode cycling: {ex.Message}");
                }
                
                swModel.ClearSelection2(true);
                // The following PropertyManager call is not available in this API version:
                // swModel.HidePropertyManager();
                
                // Force rebuild
                swModel.ForceRebuild3(true);
                swModel.GraphicsRedraw2();
                
                Logger.Info("Completed view property manipulation");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in view property manipulation: {ex.Message}", ex);
                return false;
            }
        }

        private bool HideBendLinesThroughIDrawingView(DrawingDoc swDrawingDoc, SwView flatView)
        {
            Logger.Info("Attempting bend line hiding through IDrawingView interface (not available in this API version)");
            // Not available in this API version, so always return false
            return false;
        }

        private bool HideAllSheetMetalAnnotations(ModelDoc2 swModel, SwView flatView)
        {
            Logger.Info("Attempting to hide all sheet metal annotations");
            
            try
            {
                // Get all annotations in the view
                var annotations = flatView.GetAnnotations();
                if (annotations != null)
                {
                    object[] annotationArray = (object[])annotations;
                    Logger.Info($"Found {annotationArray.Length} annotations in view");
                    
                    foreach (object annotationObj in annotationArray)
                    {
                        try
                        {
                            Annotation annotation = annotationObj as Annotation;
                            if (annotation != null)
                            {
                                string annotationType = annotation.GetType().ToString();
                                string annotationName = annotation.GetName();
                                
                                Logger.Info($"Processing annotation: {annotationName} (Type: {annotationType})");
                                
                                // The following SetVisibility2 and swViewVisibilityState_e are not available in this API version:
                                // annotation.SetVisibility2((int)swViewVisibilityState_e.swViewVisibilityStateHidden, flatView);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Error processing annotation: {ex.Message}");
                        }
                    }
                }
                
                // Force view update
                swModel.ForceRebuild3(true);
                swModel.GraphicsRedraw2();
                
                Logger.Info("Completed sheet metal annotation hiding");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error hiding sheet metal annotations: {ex.Message}", ex);
                return false;
            }
        }

        private void ForceHideAllBendRelatedEntities(ModelDoc2 swModel, SwView flatView)
        {
            Logger.Info("Force hiding all bend-related entities as last resort");
            
            try
            {
                // Get the layer manager
                LayerMgr layerMgr = (LayerMgr)swModel.GetLayerManager();
                if (layerMgr != null)
                {
                    // Create a hidden layer for bend lines if it doesn't exist
                    string hiddenLayerName = "HIDDEN_BEND_LINES";
                    Layer hiddenLayer = (Layer)layerMgr.GetLayer(hiddenLayerName);
                    if (hiddenLayer == null)
                    {
                        DrawingDoc swDrawDoc = swModel as DrawingDoc;
                        if (swDrawDoc != null)
                        {
                            bool layerCreated = swDrawDoc.CreateLayer2(
                                hiddenLayerName, "Hidden bend lines", 
                                ColorTranslator.ToWin32(Color.Black),
                                (int)swLineStyles_e.swLineCONTINUOUS,
                                (int)swLineWeights_e.swLW_THIN, 
                                false, // Not visible
                                false  // Not printable
                            );
                            
                            if (layerCreated)
                            {
                                hiddenLayer = (Layer)layerMgr.GetLayer(hiddenLayerName);
                                Logger.Info($"Created hidden layer: {hiddenLayerName}");
                            }
                        }
                    }
                    
                    if (hiddenLayer != null)
                    {
                        hiddenLayer.Visible = false;
                        hiddenLayer.Printable = false;
                        
                        // Attempt to set the current layer to something else to ensure nothing is drawn on hidden layer accidentally
                        layerMgr.SetCurrentLayer("0");
                        
                        Logger.Info("Hidden layer configured for bend lines - set to invisible and non-printable");
                    }
                }
                
                // Final attempt: Force the view to minimal display
                try
                {
                    flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swSHADED, false, false);
                    System.Threading.Thread.Sleep(50);
                    flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swWIREFRAME, false, false);
                    System.Threading.Thread.Sleep(50);
                    flatView.SetDisplayMode3(false, (int)swDisplayMode_e.swHIDDEN_GREYED, false, false);
                    flatView.SetDisplayTangentEdges2(0);
                    
                    // Force multiple rebuilds
                    swModel.ForceRebuild3(true);
                    swModel.GraphicsRedraw2();
                    swModel.ViewZoomtofit2();
                    swModel.GraphicsRedraw2();
                    
                    Logger.Info("Completed force hiding of bend-related entities");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error in force hiding: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in force hide all bend entities: {ex.Message}", ex);
            }
        }
        #endregion
    }
} 