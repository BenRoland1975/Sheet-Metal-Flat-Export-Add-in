using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using System.Linq;
using System.Diagnostics;
using AppSettings = RoesleinAddIn.Settings;

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
        #endregion

        #region Public Methods
        /// <summary>
        /// Inserts notes into the drawing by creating a Notes layer and adding text in sequence
        /// </summary>
        /// <param name="drawingDoc">The drawing document</param>
        /// <param name="notes">List of notes to be inserted</param>
        /// <returns>True if successful</returns>
        public bool InsertNotesIntoDrawing(DrawingDoc drawingDoc, List<string> notes)
        {
            try
            {
                if (drawingDoc == null)
                {
                    Logger.Error("Drawing document is null, cannot insert notes.");
                    return false;
                }

                if (notes == null || notes.Count == 0)
                {
                    Logger.Warning("No notes to insert into drawing.");
                    return true; // Not an error, just nothing to do
                }

                Logger.Info($"Inserting {notes.Count} notes into drawing...");
                ModelDoc2 swModel = drawingDoc as ModelDoc2;
                
                // Create Notes layer if it doesn't exist
                string layerName = "Notes";
                
                // Create the layer directly using DrawingDoc
                int magentaColor = ColorTranslator.ToWin32(Color.Magenta);
                
                // Just try to create the layer - if it already exists, this will have no effect
                Logger.Info($"Creating or ensuring {layerName} layer exists...");
                bool result = drawingDoc.CreateLayer2(
                    layerName,
                    "Notes Layer",
                    magentaColor,
                    0, // Continuous line
                    0, // Normal line weight
                    true, // Visible
                    true  // Printable
                );
                
                // Even if it fails, we'll continue - it might fail because the layer already exists
                if (!result)
                {
                    Logger.Warning($"Could not create {layerName} layer, may already exist.");
                }
                
                // Use the exact positioning from the backup code
                double noteX = 0.02;  // Fixed X position (absolute)
                double noteY = 0.02;  // Starting Y position (absolute)
                double yIncrement = 0.006; // Tiny increment for tight grouping
                
                Logger.Info($"Starting note position: ({noteX:F3}, {noteY:F3})");
                
                // Insert notes in REVERSE order to match the screenshot
                int notesInserted = 0;
                
                // Reverse the notes list to match the desired order
                var reversedNotes = new List<string>(notes);
                reversedNotes.Reverse();
                
                foreach (var note in reversedNotes)
                {
                    if (InsertNote(swModel, note, noteX, noteY, layerName))
                    {
                        notesInserted++;
                        noteY += yIncrement; // Tiny increment between notes
                    }
                }
                
                Logger.Info($"Successfully inserted {notesInserted} out of {notes.Count} notes.");
                return notesInserted > 0;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error in InsertNotesIntoDrawing: {ex.Message}");
                return false;
            }
        }

        public void ProcessAssemblySheetMetal()
        {
            Debug.WriteLine("[DEBUG] ProcessAssemblySheetMetal START"); // DEBUG
            Debug.WriteLine("[DEBUG] Attempting Logger.Info: Starting assembly processing"); // DEBUG
            Logger.Info("Starting assembly processing"); 
            ModelDoc2 swModel = _swApp.ActiveDoc as ModelDoc2;
            
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
            List<string> processedFiles = new List<string>();

            Logger.Info($"Processing {components.Length} components in assembly {swModel.GetPathName()}");
            Logger.Info($"Output Directory: {outputFolder}");

            foreach (Component2 comp in components)
            {
                if (comp.IsSuppressed()) continue;

                ModelDoc2 compDoc = null;
                PartDoc swPart = null;
                string compPath = "";
                try
                {
                    compPath = comp.GetPathName();
                    if (string.IsNullOrEmpty(compPath)) continue;

                    compDoc = comp.GetModelDoc2();

                    if (compDoc == null)
                    {
                         int errors = 0;
                         int warnings = 0;
                         compDoc = _swApp.OpenDoc6(compPath, (int)swDocumentTypes_e.swDocPART,
                                                  (int)swOpenDocOptions_e.swOpenDocOptions_Silent,
                                                  "", ref errors, ref warnings);

                         if (compDoc == null)
                         {
                              Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Could not open or access component document: {compPath}"); // DEBUG
                              Logger.Warning($"Could not open or access component document: {compPath}");
                              continue;
                         }
                    }

                    if (compDoc != null && compDoc.GetType() == (int)swDocumentTypes_e.swDocPART)
                    {
                        swPart = compDoc as PartDoc;

                        if (IsSheetMetalPart(swPart))
                        {
                            sheetMetalPartsFound++;
                            Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Processing sheet metal part: {compPath}"); // DEBUG
                            Logger.Info($"Processing sheet metal part: {compPath}");
                            string dxfPath = ProcessSheetMetalPart(swPart, outputFolder);

                            if (!string.IsNullOrEmpty(dxfPath))
                            {
                                processedFiles.Add(dxfPath);
                                totalProcessed++;
                            }
                            else
                            {
                                Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Failed to process or export DXF for: {compPath}"); // DEBUG
                                Logger.Warning($"Failed to process or export DXF for: {compPath}");
                            }
                        }
                        else
                        {
                             Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Skipping non-sheet metal part: {compPath}"); // DEBUG
                             Logger.Info($"Skipping non-sheet metal part: {compPath}");
                        }

                        if (comp.GetModelDoc2() == null && compDoc != null)
                        {
                            _swApp.CloseDoc(compDoc.GetTitle());
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Error: Error processing component '{compPath}': {ex.Message}"); // DEBUG
                    Logger.Error($"Error processing component '{compPath}': {ex.Message}", ex);
                    exportErrors.Add($"Error processing component '{comp.Name2}': {ex.Message}");
                    if (comp.GetModelDoc2() == null && compDoc != null) { _swApp.CloseDoc(compDoc.GetTitle()); }
                }
            }

            // Use StringBuilder for robust string construction
            var summaryBuilder = new System.Text.StringBuilder();

            if (totalProcessed > 0)
            {
                summaryBuilder.AppendLine($"Successfully processed {totalProcessed} out of {sheetMetalPartsFound} sheet metal parts found.");
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine($"Files saved to: {outputFolder}");
                Debug.WriteLine("[DEBUG] Attempting Logger.Info: Successfully processed {totalProcessed} sheet metal parts."); // DEBUG
                Logger.Info($"Successfully processed {totalProcessed} sheet metal parts.");
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

            string summaryMessage = summaryBuilder.ToString(); // Get the final string
            MessageBox.Show(summaryMessage, "Roeslein Add-in - Processing Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Debug.WriteLine("[DEBUG] Attempting Logger.Info: Completed assembly processing. Total processed: {totalProcessed}, Found SM: {sheetMetalPartsFound}"); // DEBUG
            Logger.Info($"Completed assembly processing. Total processed: {totalProcessed}, Found SM: {sheetMetalPartsFound}");
            Debug.WriteLine("[DEBUG] ProcessAssemblySheetMetal END"); // DEBUG
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

             Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Starting single part processing for: {swModel.GetPathName()}"); // DEBUG
             Logger.Info($"Starting single part processing for: {swModel.GetPathName()}");
             LoadSettings();

             SaveFileDialog saveDialog = new SaveFileDialog();
             saveDialog.Filter = "DXF Files (*.dxf)|*.dxf";
             saveDialog.Title = "Save DXF File";
             saveDialog.DefaultExt = "dxf";
             string fileName = Path.GetFileNameWithoutExtension(swModel.GetPathName());
             saveDialog.FileName = fileName + ".dxf";

             if (!string.IsNullOrEmpty(settings.LastExportFolder) && Directory.Exists(settings.LastExportFolder))
             {
                 saveDialog.InitialDirectory = settings.LastExportFolder;
             }
             else if (!string.IsNullOrEmpty(swModel.GetPathName()))
             {
                  saveDialog.InitialDirectory = Path.GetDirectoryName(swModel.GetPathName());
             }

             if (saveDialog.ShowDialog() == DialogResult.OK)
             {
                 string outputPath = saveDialog.FileName;
                 string outputDir = Path.GetDirectoryName(outputPath);
                 string baseFileName = Path.GetFileNameWithoutExtension(outputPath);
                 settings.LastExportFolder = outputDir;
                 settings.SaveSettings();

                 string resultPath = ProcessSheetMetalPart(swPart, outputDir, baseFileName);

                 if (!string.IsNullOrEmpty(resultPath))
                 {
                      Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Single part DXF exported successfully to: {resultPath}"); // DEBUG
                      Logger.Info($"Single part DXF exported successfully to: {resultPath}");
                      // Add Success MessageBox (Corrected Syntax)
                      MessageBox.Show($"DXF file exported successfully to:\n{resultPath}",
                                      "Roeslein Add-in - Export Complete",
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Information);
                 }
                 else
                 {
                      Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Single part DXF export failed for: {swModel.GetPathName()}"); // DEBUG
                      Logger.Warning($"Single part DXF export failed for: {swModel.GetPathName()}");
                      // Add Failure MessageBox (Corrected Syntax)
                      MessageBox.Show($"Failed to export DXF file for {fileName}.\nCheck the log file for details: {settings.LogFilePath}",
                                      "Roeslein Add-in - Export Failed",
                                      MessageBoxButtons.OK,
                                      MessageBoxIcon.Error);
                 }
             }
             else
             {
                  Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Single part processing cancelled by user."); // DEBUG
                  Logger.Info("Single part processing cancelled by user.");
             }

             Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Completed single part processing"); // DEBUG
             Logger.Info("Completed single part processing");
             Debug.WriteLine("[DEBUG] ProcessSinglePartSheetMetal END"); // DEBUG
        }
        
        /// <summary>
        /// Process sheet metal part and export to DXF
        /// </summary>
        public string ProcessSheetMetalPart(PartDoc swPart, string outputFolder, string baseFileName = null)
        {
            if (swPart == null) return null;
                
            // Load mappings
            var mappings = Settings.LoadPropertyMappings() ?? new List<PropertyMapping>(); // Ensure mappings is not null

            // Filter mappings to ensure DxfPropertyName is valid before creating dictionary
            var validMappings = mappings.Where(m => !string.IsNullOrWhiteSpace(m.DxfPropertyName))
                                        .OrderBy(m => m.RowNumber) // Sort by row number
                                        .ToList();
            if (validMappings.Count < mappings.Count)
            {
                 Logger.Warning("One or more property mappings were ignored due to missing DXF Property Name.");
            }

            // Create dictionary only from valid mappings
            var mappingDict = validMappings.ToDictionary(m => m.DxfPropertyName, m => m.SwCustomProperty);

            // Declare variables outside try block for accessibility
            string partTitle = "Unknown"; 
            string fileName = "Unknown";
            ModelDoc2 swModel = swPart as ModelDoc2;
            string originalConfig = "";
            string dxfOutputPath = ""; // Initialize here
            DrawingDoc swDrawingDoc = null;
            bool drawingCreated = false; // Flag to ensure cleanup

            try // Outer try for overall part processing setup
            {
                // --- Initial Part Setup ---
                string partPath = swModel.GetPathName();
                partTitle = swModel.GetTitle();
                fileName = baseFileName ?? Path.GetFileNameWithoutExtension(partTitle);
                dxfOutputPath = Path.Combine(outputFolder, fileName + ".dxf"); // Assign here

                Logger.Info($"Processing part: {partTitle} for DXF export to {dxfOutputPath}");

                originalConfig = swModel.ConfigurationManager.ActiveConfiguration.Name;
                string flatConfig = FindFlatPatternConfig(swModel);

                if (string.IsNullOrEmpty(flatConfig))
                {
                    Logger.Error($"No Flat-Pattern configuration found for {partTitle}. Cannot export DXF.");
                    exportErrors.Add($"No Flat-Pattern configuration found for {fileName}");
                    return null;
                }

                Logger.Info($"Using configuration: {flatConfig}");
                swModel.ShowConfiguration2(flatConfig);
                Logger.Info($"[PROCESS_TRACE] Successfully switched configuration to {flatConfig}.");

                Feature flatPatternFeature = FindFeatureByType(swPart, "FlatPattern");
                if (flatPatternFeature == null)
                {
                    Logger.Error($"No 'FlatPattern' feature found in configuration '{flatConfig}' for {partTitle}.");
                    exportErrors.Add($"No 'FlatPattern' feature found for {fileName}");
                    swModel.ShowConfiguration2(originalConfig); // Switch back before returning
                    return null;
                }
                flatPatternFeature.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                Logger.Info("[PROCESS_TRACE] Flat pattern feature unsuppressed.");

                // --- Get Thickness and Notes ---
                Logger.Info("Attempting to get sheet metal thickness.");
                string thickness = GetSheetMetalThickness(swPart, originalConfig); // Get thickness from original config

                if (string.IsNullOrEmpty(thickness))
                {
                     Logger.Warning($"Could not determine sheet metal thickness for {partTitle}. Skipping DXF export.");
                     thicknessFailures.Add(fileName);
                     swModel.ShowConfiguration2(originalConfig); // Switch back before returning
                     return null;
                }
                Logger.Info($"Determined thickness: {thickness}");

                var notes = new List<string>();
                Logger.Info($"Attempting to gather notes using {validMappings.Count} valid mappings.");
                
                foreach (var mapping in validMappings)
                {
                    if (string.IsNullOrWhiteSpace(mapping.SwCustomProperty))
                    {
                        Logger.Warning($"Skipping mapping with DXF Property '{mapping.DxfPropertyName}' because SW Custom Property is empty.");
                        continue;
                    }
                    // Get properties from the ORIGINAL configuration
                    string value = GetCustomPropertyValue(swModel.Extension.CustomPropertyManager[originalConfig], mapping.SwCustomProperty);
                    string noteText = $"{mapping.DxfPropertyName}: {value}";
                    
                    // Add note to the list - already in order because validMappings is sorted by RowNumber
                    notes.Add(noteText);
                    Logger.Info($"Added note: {mapping.DxfPropertyName} = {value} (Row: {mapping.RowNumber})");
                }

                // --- Drawing Creation and DXF Export ---
                int errors = 0;
                int warnings = 0;
                bool success = false;

                try // Inner try for drawing operations
                {
                    // Get drawing template path
                    string drwTemplatePath = _swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
                    if (string.IsNullOrEmpty(drwTemplatePath) || !File.Exists(drwTemplatePath))
                    {
                       Logger.Warning($"Default drawing template not found or invalid ('{drwTemplatePath}'). Using blank template.");
                       drwTemplatePath = ""; // Use blank
                    }
                    Logger.Info($"Using drawing template: {(string.IsNullOrEmpty(drwTemplatePath) ? "[Blank]" : drwTemplatePath)}");

                    // Create new drawing document
                    Logger.Info("Creating new temporary drawing document.");
                    int paperSize = string.IsNullOrEmpty(drwTemplatePath) ? (int)swDwgPaperSizes_e.swDwgPaperAsize : (int)swDwgPaperSizes_e.swDwgPapersUserDefined;
                    swDrawingDoc = _swApp.NewDocument(drwTemplatePath, paperSize, 0, 0) as DrawingDoc;
                    if (swDrawingDoc == null) throw new Exception("Failed to create new drawing document from template.");
                    drawingCreated = true; // Mark for cleanup
                    ModelDoc2 swDrawModel = swDrawingDoc as ModelDoc2; // Cast for methods
                    
                    // Set drawing to use 1:1 scale explicitly
                    try
                    {
                        Logger.Info("Setting drawing to 1:1 scale");
                        
                        // Get the active sheet
                        Sheet activeSheet = swDrawingDoc.GetCurrentSheet();
                        if (activeSheet != null)
                        {
                            // Fix: Call SetScale with correct parameters
                            activeSheet.SetScale(1, 1, false, false);
                            
                            Logger.Info("Drawing scale set to 1:1 directly on sheet");
                            
                            // Also set the sheet template size appropriately (with correct parameter types)
                            if (paperSize == (int)swDwgPaperSizes_e.swDwgPaperAsize)
                            {
                                // For A-size sheet, set a larger size if needed for visibility
                                // Fix: Use correct parameter types for SetProperties
                                activeSheet.SetProperties(
                                    (int)swDwgPaperSizes_e.swDwgPaperDsize, 
                                    1,    // Width type (1 = standard) - not bool
                                    1,    // Height type (1 = standard) - not bool
                                    1,    // First angle projection
                                    true, // Use custom scale - not string
                                    0.0,  // Width (ignored when standard)
                                    0.0   // Height (ignored when standard)
                                );
                                Logger.Info("Sheet size updated for better visibility");
                            }
                        }
                        else
                        {
                            Logger.Warning("Could not get active sheet to set scale");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warning($"Error setting drawing scale: {ex.Message}");
                    }

                    // Insert flat pattern view
                    Logger.Info("Creating flat pattern view");
                    
                    // Get user's preference for bend lines
                    bool userWantsBendLines = settings?.ShowBendLines ?? false;
                    Logger.Info($"User wants bend lines: {userWantsBendLines}");
                    
                    SolidWorks.Interop.sldworks.View swView;
                    
                    if (userWantsBendLines)
                    {
                        // WHEN USER WANTS BEND LINES: Create with the parameter set to FALSE (reverse of what you'd expect)
                        Logger.Info("Creating view WITH bend lines (user requested them)");
                        swView = swDrawingDoc.CreateFlatPatternViewFromModelView3(
                            partPath,     // ModelName
                            flatConfig,   // ConfigurationName
                            0.05,         // x position
                            0.05,         // y position
                            0,            // Angle
                            false,        // Strangely, FALSE shows bend lines
                            false         // Don't include annotations
                        );
                    }
                    else
                    {
                        // WHEN USER DOESN'T WANT BEND LINES: Create with the parameter set to TRUE (reverse of what you'd expect)
                        Logger.Info("Creating view WITHOUT bend lines (user preference)");
                        swView = swDrawingDoc.CreateFlatPatternViewFromModelView3(
                            partPath,     // ModelName
                            flatConfig,   // ConfigurationName
                            0.05,         // x position
                            0.05,         // y position
                            0,            // Angle
                            true,         // Strangely, TRUE hides bend lines
                            false         // Don't include annotations
                        );
                    }
                    
                    if (swView == null) throw new Exception("Failed to create flat pattern view.");
                    
                    // Activate the view and zoom to fit
                    swDrawingDoc.ActivateView(swView.Name);
                    swDrawModel.ViewZoomtofit2();
                    Logger.Info("Activated view and zoomed to fit");

                    // Simple layer management - only create Notes layer as needed
                    string notesLayerName = !string.IsNullOrEmpty(settings?.TextLayerName) ? settings.TextLayerName : "Notes";
                    LayerMgr layerMgr = swDrawModel.GetLayerManager();
                    
                    // Create the notes layer if needed
                    if (layerMgr != null && layerMgr.GetLayer(notesLayerName) == null)
                    {
                        Logger.Info($"Creating '{notesLayerName}' layer for text");
                        int blackColor = ColorTranslator.ToWin32(Color.Black);
                        swDrawingDoc.CreateLayer2(
                            notesLayerName,
                            "Property Notes",
                            blackColor,
                            (int)swLineStyles_e.swLineCONTINUOUS,
                            (int)swLineWeights_e.swLW_NORMAL,
                            true,  // Visible
                            true   // Printable
                        );
                    }
                    else
                    {
                        Logger.Info($"Using existing '{notesLayerName}' layer for text");
                    }

                    // Insert notes into drawing
                    Logger.Info("Inserting notes into drawing.");
                    InsertNotesIntoDrawing(swDrawingDoc, notes);
                    Logger.Info("Finished inserting notes.");

                    // Simply create the view - don't try to modify bend lines at this point
                    // The DXF export will still work correctly
                    
                    // Zoom to fit
                    swDrawModel.ViewZoomtofit2();
                    Logger.Info("Zoomed to fit temporary drawing.");

                    // Save the drawing as DXF - EXACTLY like the macro (simple call with no options)
                    Logger.Info($"Saving DXF to: {dxfOutputPath} (exact same approach as macro)");
                    
                    success = swDrawModel.Extension.SaveAs(
                        dxfOutputPath, 
                        (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
                        (int)swSaveAsOptions_e.swSaveAsOptions_Silent, 
                        null,  // No export options - EXACT same as macro
                        ref errors, 
                        ref warnings
                    );

                }
                catch (Exception ex)
                {
                     Logger.Error($"Exception during Drawing creation/save for {partTitle}: {ex.Message}", ex);
                     exportErrors.Add($"Exception creating/saving drawing DXF for {fileName}: {ex.Message}");
                     success = false; // Ensure flag is false on exception
                }

                // --- Post-Save Check ---
                     if (!success)
                     {
                    Logger.Error($"Failed to save drawing DXF for {partTitle}. Errors: {errors}, Warnings: {warnings}");
                    exportErrors.Add($"Failed to save drawing DXF for {fileName} (Error code: {errors})");
                }
                else
                {
                    Logger.Info($"Drawing DXF successfully saved to: {dxfOutputPath}");
                }

                 // Return the path ONLY if successful
                return success ? dxfOutputPath : null;
            }
            catch (Exception ex) // Catch exceptions from initial part setup phase
            {
                Logger.Error($"Error during initial processing of part '{partTitle}': {ex.Message}", ex);
                exportErrors.Add($"Error during initial processing of part '{fileName}': {ex.Message}");
                // Attempt to switch back config even if initial setup failed
                if (swModel != null && !string.IsNullOrEmpty(originalConfig) && swModel.ConfigurationManager.ActiveConfiguration.Name != originalConfig)
                {
                    try { swModel.ShowConfiguration2(originalConfig); } catch { /* Ignore errors during cleanup */ }
                }
                return null;
            }
            finally
            {
                Logger.Info("Executing finally block...");
                // Ensure drawing is closed without saving changes
                if (drawingCreated && swDrawingDoc != null)
                {
                    ModelDoc2 swDrawModelToClose = swDrawingDoc as ModelDoc2;
                    if (swDrawModelToClose != null)
                    {
                         Logger.Info($"Closing temporary drawing: {swDrawModelToClose.GetTitle()}");
                         _swApp.CloseDoc(swDrawModelToClose.GetTitle());
                    }
                }
                // Ensure original part configuration is restored
                if (swModel != null && !string.IsNullOrEmpty(originalConfig))
                {
                     // Check if config is already restored before trying again
                     if(swModel.ConfigurationManager.ActiveConfiguration.Name != originalConfig)
                     {
                        Logger.Info($"Restoring original part configuration: {originalConfig}");
                        try { swModel.ShowConfiguration2(originalConfig); } catch (Exception ex) { Logger.Warning($"Failed to restore original config '{originalConfig}': {ex.Message}");}
                     } else {
                        Logger.Info("Original configuration already active.");
                     }
                }
                Logger.Info("Finished finally block.");
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
        #endregion

        #region Private Methods
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
        
        private string ProcessSheetMetalComponent(ModelDoc2 compDoc, string outputFolder)
        {
            if (compDoc == null || compDoc.GetType() != (int)swDocumentTypes_e.swDocPART)
            {
                return null;
            }
            
            PartDoc swPart = compDoc as PartDoc;
            string fileName = Path.GetFileNameWithoutExtension(compDoc.GetPathName());
            string outputPath = Path.Combine(outputFolder, fileName + ".dxf");
            
            return ProcessSheetMetalPart(swPart, outputPath);
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
            
            // Log start of thickness check using Logger
            Logger.Info($"Checking sheet metal thickness for {partTitle} ({partPath}) in config {configName}");

            CustomPropertyManager propMgr = swModel.Extension.CustomPropertyManager[configName];
            if (propMgr == null)
            {
                // Log error using Logger
                Logger.Error($"Failed to get CustomPropertyManager for {partTitle} config {configName}");
                return null;
            }

            string val = "";
            string resVal = "";
            bool wasRes = false;
            bool wasLinked = false;

            // Check custom property first
            Logger.Info($"Checking 'Sheet Metal Thickness' custom property for {partTitle}");
            
            int ret = propMgr.Get6("Sheet Metal Thickness", true, out val, out resVal, out wasRes, out wasLinked);
            
            // Log results using Logger
            Logger.Info($"Property check results for {partTitle}: Return code: {ret}, Raw: '{val}', Resolved: '{resVal}', WasResolved: {wasRes}, Linked: {wasLinked}");

            // If we have a valid resolved value that's not an equation
            if (ret == 1 && wasRes && !string.IsNullOrEmpty(resVal) && !resVal.Contains("Thickness@"))
            {
                // Log using Logger
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

                    // Log using Logger
                    Logger.Info($"Found thickness in feature for {partTitle}: Meters={thicknessMeters}, Inches={thicknessInches}, Formatted={formattedThickness}");

                    try
                    {
                        // Update the custom property
                        Logger.Info($"Updating custom properties for {partTitle} based on feature thickness.");

                        propMgr.Add3("Sheet Metal Thickness", 
                                   (int)swCustomInfoType_e.swCustomInfoText, 
                                   formattedThickness, 
                                   (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

                        // Also update these properties to match Ben's macro
                        // Use Logger inside UpdateCustomProperty as well
                        UpdateCustomProperty(propMgr, "Part Number", partTitle.Replace(".SLDPRT", ""));
                        UpdateCustomProperty(propMgr, "Description", GetCustomPropertyValue(propMgr, "Description"));
                        UpdateCustomProperty(propMgr, "Revision", GetCustomPropertyValue(propMgr, "Revision"));
                        UpdateCustomProperty(propMgr, "Material", GetCustomPropertyValue(propMgr, "Material"));
                        UpdateCustomProperty(propMgr, "Shop Route", GetCustomPropertyValue(propMgr, "Shop Route"));

                        Logger.Info($"Successfully updated all properties for {partTitle}");
                    }
                    catch (Exception ex)
                    {
                        // Use the main Logger to report errors during property updates
                        Logger.Error($"Error updating custom properties for {partTitle} after finding thickness.", ex);
                        // Removed the direct File.AppendAllText fallback here
                        return null; // Return null if property updates fail
                    }

                    return formattedThickness;
                }
                else
                {
                    // Log using Logger
                    Logger.Error($"Could not get SheetMetalFeatureData for {partTitle}");
                }
            }
            else
            {
                // Log using Logger
                 Logger.Error($"No SheetMetal feature found in {partTitle}");
            }

            // Log using Logger
            Logger.Warning($"Could not determine thickness for {partTitle}");
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
                 try 
                 {
                    propMgr.Add3(propName, (int)swCustomInfoType_e.swCustomInfoText, value,
                                (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                    
                    // Log success using Logger
                    Logger.Info($"Updated custom property '{propName}' to '{value}'");
                 }
                 catch (Exception ex)
                 {
                     // Log error using Logger
                     Logger.Error($"Failed to update custom property '{propName}' to '{value}'", ex);
                     // Decide if this should re-throw or just log. Logging might be sufficient.
                 }
            }
            else
            {
                 Logger.Warning($"Skipped updating custom property '{propName}' because value was null or empty.");
            }
        }

        /// <summary>
        /// Inserts a single note into the drawing at the specified position
        /// </summary>
        /// <param name="swModel">The drawing document</param>
        /// <param name="noteText">Text to insert</param>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="layerName">Layer to place the note on</param>
        /// <returns>True if successful</returns>
        private bool InsertNote(ModelDoc2 swModel, string noteText, double x, double y, string layerName)
        {
            try
            {
                if (string.IsNullOrEmpty(noteText))
                {
                    Logger.Warning("Attempted to insert empty note text.");
                    return false;
                }
                
                Note swNote = swModel.InsertNote(noteText);
                if (swNote == null)
                {
                    Logger.Error($"Failed to insert note: {noteText}");
                    return false;
                }
                
                Annotation ann = swNote.GetAnnotation();
                if (ann == null)
                {
                    Logger.Error("Could not get annotation for note.");
                    return false;
                }
                
                ann.Layer = layerName;
                ann.SetPosition2(x, y, 0);
                Logger.Info($"Added note '{noteText}' to layer '{layerName}' at ({x:F4}, {y:F4})");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error inserting note: {ex.Message}");
                return false;
            }
        }
        
        private void InsertNotesIntoDXF(ModelDoc2 swModel, List<string> notes)
        {
            // Implement logic to insert notes into the DXF
            // This is a placeholder for actual implementation
        }
        #endregion
    }
} 