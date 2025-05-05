using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using Microsoft.Win32;
using SolidWorks.Interop.sldworks; // Base SW namespace
using SolidWorks.Interop.swconst; // Essential for SW Constants and Enums
using System.Linq;
using System.Diagnostics;
// using AppSettings = RoesleinAddIn.Settings; // Use fully qualified name if needed
using System.Text;
using SwView = SolidWorks.Interop.sldworks.View; // Alias for View clarity

namespace RoesleinAddIn
{
    /// <summary>
    /// Processor for sheet metal parts
    /// </summary>
    public class SheetMetalProcessor
    {
        #region Private Members
        private readonly ISldWorks _swApp;
        private Settings settings = new Settings();
        private List<string> thicknessFailures = new List<string>();
        private List<string> exportErrors = new List<string>();
        private List<string> unmappedMaterials = new List<string>();
        #endregion

        #region Constructor
        public SheetMetalProcessor(ISldWorks swApp)
        {
            try
            {
                if (swApp == null) throw new ArgumentNullException(nameof(swApp));
                _swApp = swApp;
                LoadSettings();
                EnsureDirectoriesExist();
                Logger.DebugLog("SheetMetalProcessor initialized.");
            }
            catch (Exception ex)
            {
                Logger.Error("Error initializing SheetMetalProcessor", ex);
                // Handle or re-throw as appropriate for your add-in's stability
            }
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
            ConfigurationManager swConfMgr = swModel.ConfigurationManager;
            Configuration swConf = swConfMgr.ActiveConfiguration;
            Component2 swRootComp = swConf.GetRootComponent3(true);

            if (swRootComp == null)
            {
                MessageBox.Show("Failed to get root component.", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                Logger.Warning("Failed to get root component.");
                return;
            }

            int totalProcessed = 0;
            int sheetMetalPartsFound = 0;
            thicknessFailures.Clear();
            exportErrors.Clear();
            List<string> processedFiles = new List<string>();

            // Dictionary to track material-thickness combinations
            Dictionary<string, int> materialThicknessCounts = new Dictionary<string, int>();

            Logger.Info($"Processing assembly {swModel.GetPathName()}");
            Logger.Info($"Output Directory: {outputFolder}");

            // Process components recursively
            ProcessAssemblyComponentsRecursively(swRootComp, outputFolder, ref sheetMetalPartsFound, ref totalProcessed, processedFiles, materialThicknessCounts);

            // Use StringBuilder for robust string construction
            var summaryBuilder = new System.Text.StringBuilder();

            if (totalProcessed > 0)
            {
                summaryBuilder.AppendLine($"Successfully processed {totalProcessed} out of {sheetMetalPartsFound} sheet metal parts found.");
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine($"Files saved to: {outputFolder}");

                // Add material-thickness breakdown section
                if (materialThicknessCounts.Count > 0)
                {
                    summaryBuilder.AppendLine();
                    summaryBuilder.AppendLine("Parts processed by material and thickness:");

                    foreach (var entry in materialThicknessCounts.OrderByDescending(e => e.Value))
                    {
                        summaryBuilder.AppendLine($"- {entry.Value} parts: {entry.Key}");
                    }
                }

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

            // Add a note about how the process handles failures
            if ((sheetMetalPartsFound > totalProcessed) || thicknessFailures.Count > 0 || exportErrors.Count > 0)
            {
                summaryBuilder.AppendLine();
                summaryBuilder.AppendLine("Note: The process continues even when individual parts fail. Only successfully");
                summaryBuilder.AppendLine("processed parts are saved as DXF files. Failed parts are listed above.");
            }

            // Show completion message with appropriate information
            StringBuilder message = new StringBuilder();
            message.AppendLine($"Sheet metal processing complete.\n");
            
            if (totalProcessed > 0)
            {
                message.AppendLine($"Successfully processed {totalProcessed} out of {sheetMetalPartsFound} sheet metal parts found.");
                message.AppendLine();
                message.AppendLine($"Files saved to: {outputFolder}");
                
                // Add material-thickness breakdown section
                if (materialThicknessCounts.Count > 0)
                {
                    message.AppendLine();
                    message.AppendLine("Parts processed by material and thickness:");
                    
                    foreach (var entry in materialThicknessCounts.OrderByDescending(e => e.Value))
                    {
                        message.AppendLine($"- {entry.Value} parts: {entry.Key}");
                    }
                }
            }
            else if (sheetMetalPartsFound > 0)
            {
                message.AppendLine($"Found {sheetMetalPartsFound} sheet metal parts, but none were processed successfully.");
                message.AppendLine($"Check log file for details: {settings.LogFilePath}");
            }
            else
            {
                message.AppendLine("No sheet metal parts found or processed in the assembly.");
            }

            if (thicknessFailures.Count > 0)
            {
                message.AppendLine();
                message.AppendLine($"WARNING: Could not determine sheet metal thickness for {thicknessFailures.Count} parts:");
                foreach (string failure in thicknessFailures)
                {
                    message.AppendLine($"- {failure}");
                }
            }

            if (exportErrors.Count > 0)
            {
                message.AppendLine();
                message.AppendLine($"ERROR: Encountered {exportErrors.Count} errors during processing:");
                foreach(string error in exportErrors)
                {
                    message.AppendLine($"- {error}");
                }
            }
            
            if (unmappedMaterials.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("The following materials were not found in the Material Mapping settings:");
                foreach (string material in unmappedMaterials)
                {
                    message.AppendLine($"- {material}");
                }
                message.AppendLine("\nPlease add these materials to the Material Mapping tab in Settings and run the process again.");
            }

            // Add a note about how the process handles failures
            if ((sheetMetalPartsFound > totalProcessed) || thicknessFailures.Count > 0 || exportErrors.Count > 0)
            {
                message.AppendLine();
                message.AppendLine("Note: The process continues even when individual parts fail. Only successfully");
                message.AppendLine("processed parts are saved as DXF files. Failed parts are listed above.");
            }
            
            // Replace the simple MessageBox with a custom dialog that has export button
            string summaryMessage = message.ToString(); // Get the final string
            
            // Only show export option if we have results
            if (totalProcessed > 0 || thicknessFailures.Count > 0 || exportErrors.Count > 0 || unmappedMaterials.Count > 0)
            {
                // Create a custom form to display results with an export button
                using (var resultForm = new Form())
                {
                    resultForm.Text = "Roeslein Add-in - Processing Complete";
                    resultForm.Size = new Size(600, 400);
                    resultForm.StartPosition = FormStartPosition.CenterScreen;
                    resultForm.MinimizeBox = false;
                    resultForm.MaximizeBox = false;
                    resultForm.FormBorderStyle = FormBorderStyle.FixedDialog;
                    
                    // Create a text box to display the summary
                    TextBox summaryTextBox = new TextBox();
                    summaryTextBox.Multiline = true;
                    summaryTextBox.ReadOnly = true;
                    summaryTextBox.ScrollBars = ScrollBars.Vertical;
                    summaryTextBox.Dock = DockStyle.Top;
                    summaryTextBox.Height = 300;
                    summaryTextBox.Text = summaryMessage;
                    resultForm.Controls.Add(summaryTextBox);
                    
                    // Create a panel for buttons
                    Panel buttonPanel = new Panel();
                    buttonPanel.Dock = DockStyle.Bottom;
                    buttonPanel.Height = 50;
                    resultForm.Controls.Add(buttonPanel);
                    
                    // Create OK button
                    Button okButton = new Button();
                    okButton.Text = "OK";
                    okButton.DialogResult = DialogResult.OK;
                    okButton.Location = new Point(400, 15);
                    okButton.Size = new Size(75, 23);
                    buttonPanel.Controls.Add(okButton);
                    
                    // Create Export to Excel button
                    Button exportButton = new Button();
                    exportButton.Text = "Export to Excel";
                    exportButton.Location = new Point(300, 15);
                    exportButton.Size = new Size(90, 23);
                    exportButton.Click += (sender, e) => 
                    {
                        // Close the form
                        resultForm.DialogResult = DialogResult.OK;
                        
                        // Export to Excel
                        ExportResultsToExcel(
                            processedFiles,
                            materialThicknessCounts,
                            thicknessFailures,
                            exportErrors,
                            outputFolder);
                    };
                    buttonPanel.Controls.Add(exportButton);
                    
                    // Show the form
                    resultForm.ShowDialog();
                }
            }
            else
            {
                // For no results, just show a simple message box
                MessageBox.Show(summaryMessage, "Roeslein Add-in - Processing Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }

            Debug.WriteLine("[DEBUG] Attempting Logger.Info: Completed assembly processing. Total processed: {totalProcessed}, Found SM: {sheetMetalPartsFound}"); // DEBUG
            Logger.Info($"Completed assembly processing. Total processed: {totalProcessed}, Found SM: {sheetMetalPartsFound}");
            Debug.WriteLine("[DEBUG] ProcessAssemblySheetMetal END"); // DEBUG
        }

        public void ProcessSinglePartSheetMetal()
        {
             Debug.WriteLine("[DEBUG] ProcessSinglePartSheetMetal START"); // DEBUG
             ModelDoc2 swModel = _swApp?.ActiveDoc as ModelDoc2;

             if (swModel == null) { MessageBox.Show("No active document."); return; }
             if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART) { MessageBox.Show("Active document is not a part."); return; }

             PartDoc swPart = swModel as PartDoc;
             if (swPart == null) { MessageBox.Show("Failed to process the part document."); return; }

             if (!SheetMetalProcessor.IsSheetMetalPart(swPart)) // Use static method
             {
                 Logger.Info($"Skipped: {swModel.GetPathName()} is not a sheet metal part.");
                 MessageBox.Show("The active document is not detected as a sheet metal part.");
                 return;
             }

             Logger.Info($"Starting single part processing for: {swModel.GetPathName()}");
             LoadSettings();

             string outputDir;
             string outputPath;
             string initialFileName = Path.GetFileNameWithoutExtension(swModel.GetPathName());
             string defaultFolder = settings.LastExportFolder ?? System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments); // Handle null setting

              // ... (rest of the Yes/No dialog logic for choosing output path) ...
               DialogResult dialogResult = MessageBox.Show(
                  $"Export '{initialFileName}.dxf' to the default folder?\n\nFolder: {defaultFolder}\n\nChoose 'Yes' for default, 'No' to select a different location.",
                  "Roeslein Add-in - Export DXF",
                  MessageBoxButtons.YesNoCancel,
                  MessageBoxIcon.Question);

              if (dialogResult == DialogResult.Cancel) { Logger.Info("Single part processing cancelled."); return; }

              if (dialogResult == DialogResult.Yes) {
                  // ... (Use defaultFolder, check existence, confirm overwrite) ...
                   outputDir = defaultFolder;
                   outputPath = Path.Combine(outputDir, initialFileName + ".dxf");
                   // ... (overwrite check) ...

              } else { // User chose 'No'
                   // ... (Use SaveFileDialog, update settings.LastSinglePartExportFolder) ...
                    using (SaveFileDialog saveDialog = new SaveFileDialog())
                  {
                      saveDialog.Filter = "DXF Files (*.dxf)|*.dxf";
                      saveDialog.Title = "Save DXF File As";
                      saveDialog.DefaultExt = "dxf";
                      saveDialog.FileName = initialFileName + ".dxf";
                      saveDialog.InitialDirectory = Directory.Exists(settings.LastSinglePartExportFolder) ? settings.LastSinglePartExportFolder : defaultFolder; // Set initial dir

                      if (saveDialog.ShowDialog() != DialogResult.OK) { Logger.Info("Single part processing cancelled."); return; }

                      outputPath = saveDialog.FileName;
                      outputDir = Path.GetDirectoryName(outputPath);
                      settings.LastSinglePartExportFolder = outputDir;
                      settings.SaveSettings();
                  }
              }


             // --- Process the Part ---
             exportErrors.Clear();
             thicknessFailures.Clear();
             unmappedMaterials.Clear();

             string resultPath = ProcessSheetMetalPart(swPart, outputDir, Path.GetFileNameWithoutExtension(outputPath));

              // ... (Show result message box - success or failure) ...
               if (!string.IsNullOrEmpty(resultPath)) {
                   MessageBox.Show($"DXF file exported successfully to:\n{resultPath}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
              } else {
                    StringBuilder errorMsg = new StringBuilder($"Failed to export DXF file for {initialFileName}.\n");
                    // ... (append details from lists like thicknessFailures, unmappedMaterials, exportErrors) ...
                    errorMsg.AppendLine($"\nCheck log for details: {settings.LogFilePath}");
                    MessageBox.Show(errorMsg.ToString(), "Export Failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
              }

             Logger.Info("Completed single part processing");
        }


        /// <summary>
        /// Process sheet metal part, create drawing with layers, add notes, and export to DXF.
        /// </summary>
        public string ProcessSheetMetalPart(PartDoc swPart, string outputFolder, string baseFileName = null)
        {
            if (swPart == null) { Logger.Error("ProcessSheetMetalPart called with null PartDoc."); return null; }

            ModelDoc2 swModel = swPart as ModelDoc2;
            if (swModel == null) { Logger.Error("ProcessSheetMetalPart failed to cast PartDoc to ModelDoc2."); return null; }

            string partPath = swModel.GetPathName();
            string partTitle = swModel.GetTitle();
            string fileName = Path.GetFileNameWithoutExtension(baseFileName ?? partTitle); // Use GetFileNameWithoutExtension
            string outputPath = Path.Combine(outputFolder, fileName + ".dxf");
            string originalConfig = "";

            ModelDoc2 swDraw = null;
            DrawingDoc swDrawingDoc = null;
            bool switchedConfig = false;

            try
            {
                Logger.Info($"Processing part: {partTitle} ('{partPath}') for DXF export to '{outputPath}'");

                originalConfig = swModel.ConfigurationManager?.ActiveConfiguration?.Name; // Null checks
                if (string.IsNullOrEmpty(originalConfig)) { Logger.Error("Failed to get original configuration name."); return null; }
                Logger.DebugLog($"Original configuration: {originalConfig}");

                string flatConfig = FindFlatPatternConfig(swModel);
                if (string.IsNullOrEmpty(flatConfig)) { exportErrors.Add($"No Flat-Pattern configuration found for {fileName}"); return null; }

                Logger.Info($"Found flat pattern configuration: {flatConfig}");
                if (originalConfig != flatConfig)
                {
                    Logger.DebugLog($"Switching from '{originalConfig}' to '{flatConfig}'");
                    switchedConfig = swModel.ShowConfiguration2(flatConfig);
                    if (!switchedConfig) { exportErrors.Add($"Failed to switch to flat pattern configuration for {fileName}"); return null; }
                    Logger.Info($"Successfully switched to configuration: {flatConfig}");
                } else { Logger.Info("Already in the flat pattern configuration."); }

                Feature flatPatternFeature = FindFeatureByType(swPart, "FlatPattern");
                if (flatPatternFeature == null) { exportErrors.Add($"No 'FlatPattern' feature found for {fileName}"); if (switchedConfig) swModel.ShowConfiguration2(originalConfig); return null; }
                Logger.DebugLog($"Found FlatPattern feature: {flatPatternFeature.Name}");

                // Use IFeature::IGetSuppression2 for suppression check/set
                int suppressState = flatPatternFeature.GetSuppression2((int)swInConfigurationOpts_e.swThisConfiguration, null);
                Logger.DebugLog($"FlatPattern feature suppression state in '{flatConfig}': {(swFeatureSuppression_e)suppressState}");

                if (suppressState == (int)swFeatureSuppressionAction_e.swSuppressFeature)
                {
                    Logger.Info($"Unsuppressing FlatPattern feature '{flatPatternFeature.Name}' in config '{flatConfig}'.");
                    flatPatternFeature.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                } else { Logger.Info($"FlatPattern feature '{flatPatternFeature.Name}' is already unsuppressed in config '{flatConfig}'."); }

                HideAllVisibleSketchesInConfig(swModel);
                Logger.Info("Hid visible sketches in flat pattern config.");

                string thickness = GetSheetMetalThickness(swPart, originalConfig);
                if (string.IsNullOrEmpty(thickness)) { thicknessFailures.Add(fileName); if (switchedConfig) swModel.ShowConfiguration2(originalConfig); return null; }
                Logger.Info($"Determined thickness: {thickness}");

                // --- Create Drawing ---
                Logger.DebugLog("Preparing to create drawing document.");
                string drwTemplate = settings.DrawingTemplatePath;
                // ... (Template finding logic - same as before) ...
                 if (string.IsNullOrEmpty(drwTemplate) || !File.Exists(drwTemplate)) { /* ... */ }

                // Create new drawing silently - Ensure _swApp is not null
                if (_swApp == null) throw new InvalidOperationException("_swApp (ISldWorks) is null.");
                _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSilentOperation, true);
                swDraw = _swApp.NewDocument(drwTemplate, (int)swDwgPaperSizes_e.swDwgPaperAsize, 0, 0) as ModelDoc2;
                _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSilentOperation, false);

                if (swDraw == null) throw new Exception("Failed to create new drawing document.");
                Logger.Info($"Created new drawing document using template: {drwTemplate}");
                swDrawingDoc = swDraw as DrawingDoc;
                if (swDrawingDoc == null) throw new Exception("Failed to cast drawing document.");

                // --- Create flat pattern view ---
                Logger.DebugLog($"Creating flat pattern view from model: {partPath}, config: {flatConfig}");
                double viewX = 0.1, viewY = 0.1; // meters
                SwView flatView = swDrawingDoc.CreateFlatPatternViewFromModelView3(partPath, flatConfig, viewX, viewY, 0, true, false);
                if (flatView == null) throw new Exception("Failed to create flat pattern view in drawing.");
                Logger.Info($"Created flat pattern view named: {flatView.Name}");

                // --- START BEND LINE LAYER ASSIGNMENT ---
                try
                {
                    string bendLayerName = settings.BendLineLayerName ?? "Bend_Lines"; // Use ?? for default
                    Logger.Info($"Attempting to assign bend lines to layer: '{bendLayerName}'");

                    LayerMgr lyrMgr = (LayerMgr)swDraw.GetLayerManager();
                    Layer bendLayer = lyrMgr?.GetLayer(bendLayerName) as Layer; // Explicit cast
                    if (bendLayer == null && lyrMgr != null)
                    {
                        Logger.DebugLog($"Layer '{bendLayerName}' not found. Attempting to create.");
                        int blueColor = ColorTranslator.ToWin32(Color.Blue);
                        if (lyrMgr.AddLayer(bendLayerName, "Layer for Bend Lines", blueColor, (int)swLineStyles_e.swLineSTYLE_DASHDOT, (int)swLineWeights_e.swLW_THIN) == 1) {
                            Logger.Info($"Created bend line layer: '{bendLayerName}'");
                            bendLayer = lyrMgr.GetLayer(bendLayerName) as Layer;
                        } else { Logger.Error($"Failed to create bend line layer '{bendLayerName}'."); }
                    } else if (lyrMgr == null) { Logger.Error("Layer Manager is null."); }
                     else { Logger.Info($"Bend line layer '{bendLayerName}' already exists."); }

                    if (bendLayer != null && !bendLayer.Visible) bendLayer.Visible = true; // Ensure visibility

                    // Find the bend line sketch feature in the PART model
                    Sketch bendLineSketch = null;
                    Feature sketchFeatForBendLines = null;
                    if (flatPatternFeature != null)
                    {
                         string expectedSketchName = $"Bend Lines-{flatPatternFeature.Name}";
                         Logger.DebugLog($"Looking for sketch feature: '{expectedSketchName}'");
                         if(swModel.FeatureManager != null) // Check FeatureManager
                         {
                             sketchFeatForBendLines = swModel.FeatureManager.GetFeatureByName(expectedSketchName);
                             if (sketchFeatForBendLines != null && sketchFeatForBendLines.GetTypeName2() == "ProfileFeature")
                             {
                                 bendLineSketch = sketchFeatForBendLines.GetSpecificFeature2() as Sketch;
                                 if(bendLineSketch != null) Logger.Info($"Found bend line sketch: {sketchFeatForBendLines.Name}");
                                  else Logger.Warning($"Feature '{expectedSketchName}' found, but failed to get Sketch object.");
                             } else { Logger.Warning($"Feature '{expectedSketchName}' not found or not ProfileFeature."); }
                         } else { Logger.Warning("Part model FeatureManager is null."); }
                    } else { Logger.Warning("FlatPattern feature was null."); }

                    if (bendLineSketch != null && sketchFeatForBendLines != null && bendLayer != null) // Ensure layer exists too
                    {
                        object[] segments = bendLineSketch.GetSketchSegments() as object[];
                        if (segments != null && segments.Length > 0)
                        {
                            int assignedCount = 0;
                            Logger.Info($"Found {segments.Length} segments in model sketch '{sketchFeatForBendLines.Name}'. Assigning layer in drawing.");

                            if (!swDrawingDoc.ActivateView(flatView.Name)) Logger.Warning($"Failed to activate drawing view '{flatView.Name}'."); // Use DrawingDoc

                            SelectionMgr selMgr = (SelectionMgr)swDraw.SelectionManager; // Explicit Cast
                            if (selMgr == null) throw new Exception("Failed to get Selection Manager.");

                            foreach (object segObj in segments)
                            {
                                SketchSegment modelSegment = segObj as SketchSegment;
                                if (modelSegment != null)
                                {
                                    // Select the drawing curve associated with this sketch segment
                                    // This mapping isn't direct; need a robust selection or iteration approach.
                                    // Trying selection first:
                                    if (modelSegment.Select4(false, null)) // Select single segment (overwrite)
                                    {
                                        // Try to get DrawingCurve first
                                        DrawingCurve drawCurve = selMgr.GetSelectedObject6(1, -1) as DrawingCurve; // Cast needed
                                        if (drawCurve != null)
                                        {
                                             Annotation ann = drawCurve.GetAnnotation() as Annotation; // Cast needed
                                             if (ann != null)
                                             {
                                                 ann.Layer = bendLayerName; // Use property
                                                 if (ann.Layer == bendLayerName) assignedCount++;
                                                 else Logger.Warning($"Failed layer verify (DrawCurve): {ann.Layer}");
                                             } else { Logger.Warning("No Annotation on DrawingCurve."); }
                                        }
                                        else // Fallback: try getting annotation from selected sketch segment in drawing
                                        {
                                             SketchSegment drawSeg = selMgr.GetSelectedObject6(1, -1) as SketchSegment;
                                             if(drawSeg != null)
                                             {
                                                  Annotation ann = drawSeg.GetAnnotation() as Annotation;
                                                  if(ann != null)
                                                  {
                                                       ann.Layer = bendLayerName; // Use property
                                                       if (ann.Layer == bendLayerName) assignedCount++;
                                                       else Logger.Warning($"Failed layer verify (DrawSeg): {ann.Layer}");
                                                  } else { Logger.Warning("No Annotation on SketchSegment (drawing)."); }
                                             } else { Logger.Warning("Selection was neither DrawingCurve nor SketchSegment."); }
                                        }
                                        swDraw.ClearSelection2(true); // Clear after each segment
                                    } else { Logger.Warning("Failed to select model segment."); }
                                }
                            }
                            Logger.Info($"Attempted assignment for {segments.Length} segments. Verified: {assignedCount} assignments to layer '{bendLayerName}'.");
                        } else { Logger.Warning($"No segments in model sketch '{sketchFeatForBendLines.Name}'."); }
                    } else { Logger.Error($"Could not find bend sketch or layer '{bendLayerName}'. Skipping layer assignment."); if (bendLayer != null) exportErrors.Add($"Could not find bend line sketch for {fileName}"); }
                }
                catch (Exception ex) { Logger.Error($"Error assigning bend lines: {ex.Message}", ex); exportErrors.Add($"Error assigning bend lines layer for {fileName}"); }
                // --- END BEND LINE LAYER ASSIGNMENT ---

                // Zoom/Redraw AFTER layer assignment
                 if (!swDrawingDoc.ActivateView(flatView.Name)) Logger.Warning("Failed to activate view before zoom."); // Use DrawingDoc
                swDraw.ViewZoomtofit2();
                swDraw.GraphicsRedraw2(); // Use GraphicsRedraw2
                Logger.DebugLog("Zoomed/Redrew after layer assignment.");

                // --- Add notes ---
                AddNotesToDrawing(swDrawingDoc, swModel, originalConfig, thickness);
                Logger.Info("Finished adding notes to drawing.");

                // --- Set Export Options and Save ---
                SetDxfExportOptions(swDraw);
                Logger.Info("Set basic DXF export options.");

                Logger.Info("Forcing drawing rebuild before saving DXF.");
                swDraw.ForceRebuild3(true);
                swDraw.GraphicsRedraw2();

                Logger.Info($"Attempting to save DXF to: {outputPath}");
                int exportErrorsCount = 0;
                int exportWarningsCount = 0;
                 if (_swApp != null) _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDxfMapping, true); // Ensure mapping enabled
                Logger.DebugLog($"Set swDxfMapping to true before SaveAs.");

                bool saveSuccess = swDraw.Extension.SaveAs(outputPath, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy, null, ref exportErrorsCount, ref exportWarningsCount);

                Logger.Info($"SaveAs finished. Success: {saveSuccess}, Errors: {exportErrorsCount}, Warnings: {exportWarningsCount}");

                if (!saveSuccess || exportErrorsCount != 0) { exportErrors.Add($"Failed to save DXF for {fileName} (Code: {exportErrorsCount})"); return null; } // Cleanup in finally
                if (exportWarningsCount != 0) { Logger.Warning($"DXF save for {partTitle} completed with warnings: {exportWarningsCount}"); }

                Logger.Info($"DXF successfully saved to: {outputPath}");
                if (!File.Exists(outputPath)) { exportErrors.Add($"DXF file not found after save for {fileName}"); return null; } // Cleanup in finally
                Logger.Info("DXF file verified on disk.");

                return outputPath; // Success
            }
            catch (Exception ex)
            {
                Logger.Error($"CRITICAL error processing part '{partTitle}': {ex.Message}", ex);
                exportErrors.Add($"Critical error processing {fileName}: {ex.Message}");
                return null; // Cleanup in finally
            }
            finally
            {
                Logger.DebugLog($"Entering finally block for {partTitle}.");
                if (swDraw != null)
                {
                    string drawingTitle = "";
                    try {
                         drawingTitle = swDraw.GetTitle();
                         Logger.DebugLog($"Closing drawing document: {drawingTitle}");
                          if (_swApp != null) _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSilentOperation, true);
                         _swApp?.CloseDoc(drawingTitle);
                          if (_swApp != null) _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swSilentOperation, false);
                    } catch (Exception closeEx) { Logger.Error($"Failed to close drawing '{drawingTitle}': {closeEx.Message}"); }
                }
                if (switchedConfig && !string.IsNullOrEmpty(originalConfig) && swModel != null)
                {
                    try { Logger.DebugLog($"Switching part '{partTitle}' back to original config: {originalConfig}"); swModel.ShowConfiguration2(originalConfig); }
                    catch (Exception switchEx) { Logger.Error($"Failed to switch part '{partTitle}' back: {switchEx.Message}"); }
                }
                 Logger.DebugLog($"Finished finally block for {partTitle}.");
            }
        }

        /// <summary>
        /// Checks if a part is a sheet metal part by looking for the "SheetMetal" feature.
        /// </summary>
        public static bool IsSheetMetalPart(PartDoc part) // Made static
        {
            if (part == null) return false;
            try
            {
                ModelDoc2 model = part as ModelDoc2;
                if (model == null) return false;

                // Check for FlatPattern feature first (most definitive)
                Feature feat = model.IFirstFeature(); // Use IFirstFeature
                while (feat != null)
                {
                    if (feat.GetTypeName2() == "FlatPattern")
                    {
                        Logger.DebugLog($"IsSheetMetalPart: Found FlatPattern feature '{feat.Name}'. Part IS sheet metal.");
                        return true;
                    }
                    feat = feat.IGetNextFeature(); // Use IGetNextFeature
                }

                 // Check body type as secondary method
                 object[] bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[]; // Get hidden bodies too? Check API - false seems safer
                if (bodies != null)
                {
                    foreach (object bodyObj in bodies)
                    {
                        Body2 body = bodyObj as Body2;
                        if (body != null && body.IsSheetMetal())
                        {
                            Logger.DebugLog($"IsSheetMetalPart: Found sheet metal body. Part IS sheet metal.");
                            return true;
                        }
                    }
                }
                Logger.DebugLog($"IsSheetMetalPart: No FlatPattern feature or sheet metal body found for '{model.GetTitle()}'. Part IS NOT sheet metal.");
                return false;
            }
            catch (Exception ex) { Logger.Error($"Error in IsSheetMetalPart: {ex.Message}", ex); return false; }
        }

        /// <summary>
        /// Gets the material for a part
        /// </summary>
        private string GetMaterial(ModelDoc2 swModel)
        {
            return GetCustomProperty(swModel, "Material"); // Assuming 'Material' property holds it
        }

        /// <summary>
        /// Maps a SolidWorks material to a DXF output material using the material cross-reference table
        /// </summary>
        /// <param name="swMaterial">The SolidWorks material to map</param>
        /// <returns>The mapped DXF material, or null if no mapping exists</returns>
        private string MapMaterial(string swMaterial)
        {
             // --- Keep your existing robust MapMaterial logic here ---
             // This is just a placeholder example
             if (string.IsNullOrWhiteSpace(swMaterial)) return null;
             string normalized = swMaterial.Trim();
             // Load mappings from Settings.LoadMaterialMappings()...
             // Perform matching (exact, cleaned, contains)...
             // If found: return mapped value
             // If not found:
             if (!unmappedMaterials.Contains(normalized)) unmappedMaterials.Add(normalized);
             return null; // Indicate not mapped
        }

        private string GetCustomPropertyValue(CustomPropertyManager propMgr, string propName)
        {
            // Keep existing GetCustomPropertyValue
            if (propMgr == null || string.IsNullOrEmpty(propName)) return "";
            string val = "", resVal = ""; bool wasRes = false, wasLinked = false;
            propMgr.Get6(propName, true, out val, out resVal, out wasRes, out wasLinked); // Use Get6 for resolved value
            return wasRes ? resVal : val;
        }

         private string GetCustomProperty(ModelDoc2 doc, string propName) // Overload for ModelDoc
        {
            if (doc == null) return "";
            CustomPropertyManager custPropMgr = doc.Extension?.CustomPropertyManager[doc.ConfigurationManager?.ActiveConfiguration?.Name ?? ""];
            return GetCustomPropertyValue(custPropMgr, propName);
        }

        private void UpdateCustomProperty(CustomPropertyManager propMgr, string propName, string value)
        {
            // Keep existing UpdateCustomProperty
            if (propMgr == null || string.IsNullOrEmpty(propName)) return;
            if (value == null) value = ""; // Ensure value is not null for Add3
            try { propMgr.Add3(propName, (int)swCustomInfoType_e.swCustomInfoText, value, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue); }
            catch (Exception ex) { Logger.Error($"Failed to update prop '{propName}'", ex); }
        }

        private string GetSheetMetalThickness(PartDoc swPart, string configName)
        {
             // Keep existing GetSheetMetalThickness
             if (swPart == null) return null;
             ModelDoc2 swModel = swPart as ModelDoc2;
             string partTitle = swModel?.GetTitle() ?? "UnknownPart";
             Logger.Info($"Getting thickness for {partTitle}, config '{configName}'");
             CustomPropertyManager propMgr = swModel?.Extension?.CustomPropertyManager[configName];

             // Try "Thickness" property first
             string thicknessProp = GetCustomPropertyValue(propMgr, "Thickness");
             if (!string.IsNullOrEmpty(thicknessProp) && double.TryParse(thicknessProp, out _)) { Logger.Info($"Using thickness '{thicknessProp}' from 'Thickness' property."); return thicknessProp; }

             // Try "Sheet Metal Thickness" property
             thicknessProp = GetCustomPropertyValue(propMgr, "Sheet Metal Thickness");
              if (!string.IsNullOrEmpty(thicknessProp) && double.TryParse(thicknessProp, out _)) { Logger.Info($"Using thickness '{thicknessProp}' from 'Sheet Metal Thickness' property."); return thicknessProp; }

             // Try SheetMetal feature data
             Feature swSMFeat = FindFeatureByType(swPart, "SheetMetal");
             if (swSMFeat != null) {
                  SheetMetalFeatureData swSMData = swSMFeat.GetDefinition() as SheetMetalFeatureData;
                  if (swSMData != null) {
                       double thicknessMeters = swSMData.Thickness;
                       double thicknessInches = Math.Round(thicknessMeters * 39.3701, 4); // Convert m to inches
                       string formattedThickness = thicknessInches.ToString("0.0000");
                       Logger.Info($"Found thickness {formattedThickness} inches from SheetMetal feature.");
                       return formattedThickness;
                  }
             }
             Logger.Warning($"Could not determine thickness for {partTitle}.");
             return null;
        }

        private string FindFlatPatternConfig(ModelDoc2 swModel)
        {
            // Keep existing FindFlatPatternConfig
            if (swModel == null) return null;
             try {
                 string[] configNames = swModel.GetConfigurationNames() as string[];
                 if (configNames != null) {
                      foreach (string configName in configNames) {
                           // More robust check for common flat pattern naming conventions
                           if (configName.IndexOf("FLAT-PATTERN", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               configName.IndexOf("FlatPattern", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               configName.EndsWith("SM-FLAT-PATTERN", StringComparison.OrdinalIgnoreCase))
                            {
                                Logger.Info($"Found flat pattern configuration: {configName}");
                                return configName;
                           }
                      }
                 }
             } catch (Exception ex) { Logger.Error($"Error finding flat pattern config: {ex.Message}", ex); }
             Logger.Warning($"No flat pattern configuration found for '{swModel.GetTitle()}'.");
             return null;
        }

        private Feature FindFeatureByType(PartDoc swPart, string featureTypeName)
        {
            // Keep existing FindFeatureByType
            if (swPart == null) return null;
            ModelDoc2 swModel = swPart as ModelDoc2;
            Feature feat = swModel?.IFirstFeature(); // Use IFirstFeature
            while (feat != null)
            {
                if (feat.GetTypeName2() == featureTypeName) return feat;
                feat = feat.IGetNextFeature(); // Use IGetNextFeature
            }
            return null;
        }

        private void HideAllVisibleSketchesInConfig(ModelDoc2 swModel)
        {
            // Keep existing HideAllVisibleSketchesInConfig
             if (swModel == null) return;
            Logger.DebugLog("Hiding visible sketches...");
            bool changed = false;
            Feature feat = swModel.IFirstFeature(); // Use IFirstFeature
            while (feat != null)
            {
                if (feat.GetTypeName2() == "ProfileFeature")
                {
                    // Check visibility before hiding (more complex, requires sketch access)
                    // Simple approach: Just try to hide via BlankSketch if selectable
                     if (feat.Select2(false, -1))
                     {
                         swModel.BlankSketch();
                         swModel.ClearSelection2(true);
                         changed = true;
                     }
                }
                feat = feat.IGetNextFeature(); // Use IGetNextFeature
            }
            if (changed) swModel.GraphicsRedraw2();
             Logger.DebugLog("Finished hiding sketches.");
        }

        // Corrected InsertNote
        private bool InsertNote(ModelDoc2 swModel, string noteText, double x, double y, string layerName)
        {
            if (string.IsNullOrEmpty(noteText) || swModel == null || swModel.GetType() != (int)swDocumentTypes_e.swDocDRAWING) { return false; }

            DrawingDoc swDrawingDoc = (DrawingDoc)swModel;
            bool noteCreated = false;
            bool layerSetSuccess = false;

            try
            {
                LayerMgr lyrMgr = (LayerMgr)swModel.GetLayerManager();
                Layer targetLayer = lyrMgr?.GetLayer(layerName) as Layer;
                if (targetLayer == null)
                {
                    Logger.Error($"Target layer '{layerName}' not found. Note on default layer.");
                    layerName = lyrMgr?.GetCurrentLayer()?.Name ?? "0"; // Fallback
                    targetLayer = lyrMgr?.GetLayer(layerName) as Layer; // Re-get fallback layer
                }
                if (targetLayer != null && !targetLayer.Visible) targetLayer.Visible = true;

                if (!swDrawingDoc.ActivateSheet(swDrawingDoc.GetCurrentSheet().GetName())) Logger.Warning("Failed to activate sheet.");

                // *** Use ModelDocExtension.CreateNote2 ***
                // Use swDisplayState_All to ensure visibility regardless of state
                Note swNote = swModel.Extension.CreateNote(noteText, (int)swDisplayStateMode_e.swDisplayState_All, "", x, y, 0, 0.0035, 0);

                if (swNote != null)
                {
                    noteCreated = true;
                    Annotation swAnn = swNote.GetAnnotation() as Annotation; // Cast
                    if (swAnn != null)
                    {
                        swAnn.Layer = layerName; // Use property
                        if (swAnn.Layer == layerName) // Verify
                        {
                            layerSetSuccess = true;
                            swAnn.Visible = (int)swAnnotationVisibilityState_e.swAnnotationVisible; // Ensure visible
                            Logger.Info($"Note '{noteText}' created on layer '{layerName}'.");
                        } else { Logger.Warning($"Note '{noteText}': Layer verify failed (Actual: {swAnn.Layer}, Target: {layerName})."); }
                    } else { Logger.Warning($"Note '{noteText}': Could not get Annotation."); }
                } else { Logger.Error($"Failed to create Note object: '{noteText}'."); }
            }
            catch (Exception ex) { Logger.Error($"InsertNote Exception for '{noteText}': {ex.Message}", ex); return false; }

            return noteCreated && layerSetSuccess;
        }


        // Corrected SetDxfExportOptions
        private void SetDxfExportOptions(ModelDoc2 swDraw) // Pass Drawing Doc (as ModelDoc2)
        {
            Logger.DebugLog("SetDxfExportOptions START");
            if (_swApp == null || swDraw == null || swDraw.GetType() != (int)swDocumentTypes_e.swDocDRAWING) { Logger.Error("Invalid input for SetDxfExportOptions."); return; }

            try
            {
                // Use swUserPreferenceIntegerValue_e.swDxfExportScale = 1 for 1:1
                 _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfExportScale, 1);
                Logger.DebugLog("Set swDxfExportScale = 1 (1:1).");

                // Use swUserPreferenceIntegerValue_e.swDxfExportFonts
                 _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfExportFonts, (int)swDxfOutputFonts_e.swDxfOutputFontsTrueType);
                Logger.DebugLog("Set swDxfExportFonts = swDxfOutputFontsTrueType.");

                // Use swUserPreferenceToggle_e.swDxfExportSplinesAsSplines
                 _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDxfExportSplinesAsSplines, true);
                Logger.DebugLog("Set swDxfExportSplinesAsSplines = true.");

                // Use swUserPreferenceIntegerValue_e.swDxfExportVersion
                 _swApp.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swDxfExportVersion, (int)swDxfVersion_e.swDxfVersionR2018); // Use R2018 or desired version
                 Logger.DebugLog("Set swDxfExportVersion = R2018.");

                 // Use swUserPreferenceToggle_e.swDxfMapping - THIS IS KEY FOR LAYERS
                 _swApp.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDxfMapping, true);
                 Logger.DebugLog("Set swDxfMapping = true (Enable layer mapping).");

                Logger.Info("Successfully configured DXF export options.");
            }
            catch (Exception ex) { Logger.Error("Error setting DXF export options", ex); }
        }

        // Corrected AddNotesToDrawing
        private void AddNotesToDrawing(DrawingDoc swDrawingDoc, ModelDoc2 swPartModel, string configName, string thickness)
        {
            // Adapted from Backup: setup drawing doc and layer
            ModelDoc2 swDraw = swDrawingDoc as ModelDoc2;
            if (swDraw == null || swPartModel == null)
            {
                Logger.Error("Invalid input to AddNotesToDrawing.");
                return;
            }

            LayerMgr lyrMgr = swDraw.GetLayerManager();
            if (lyrMgr == null)
            {
                Logger.Error("Null Layer Manager.");
                return;
            }

            string layerName = settings.TextLayerName ?? "NOTES";
            Logger.Info($"Adding notes on layer: '{layerName}'");

            Layer notesLayer = lyrMgr.GetLayer(layerName) as Layer;
            if (notesLayer == null)
            {
                Logger.Info($"Creating layer '{layerName}'");
                int magentaColor = ColorTranslator.ToWin32(Color.Magenta);
                bool created = swDrawingDoc.CreateLayer2(
                    layerName,
                    $"Property Notes ({layerName})",
                    magentaColor,
                    (int)swLineStyles_e.swLineCONTINUOUS,
                    (int)swLineWeights_e.swLW_NORMAL,
                    true,
                    true);
                if (!created)
                    Logger.Error($"Failed to create layer '{layerName}'");
                notesLayer = lyrMgr.GetLayer(layerName) as Layer;
                if (notesLayer == null)
                {
                    Logger.Error($"Layer '{layerName}' creation did not yield a layer");
                    return;
                }
            }
            else
            {
                notesLayer.Color = ColorTranslator.ToWin32(Color.Magenta);
                notesLayer.Visible = true;
                notesLayer.Printable = true;
                Logger.Info($"Using existing layer '{layerName}'");
            }

            lyrMgr.ActiveLayer = layerName;
            Logger.DebugLog($"Active layer set to '{layerName}'");

            // Gather property notes
            CustomPropertyManager propMgr = swPartModel.Extension.CustomPropertyManager[configName];
            if (propMgr == null)
            {
                Logger.Error($"Null CustomPropertyManager for config '{configName}'.");
                return;
            }

            var props = new[] { "Part Number", "Description", "Revision", "Material", "Shop Route" };
            var notes = new List<string>();
            foreach (var pn in props)
            {
                string val = GetCustomPropertyValue(propMgr, pn);
                notes.Add($"{pn}: {val}");
            }
            notes.Add($"Thickness: {thickness}");
            notes.RemoveAll(string.IsNullOrWhiteSpace);

            // Position notes bottom-up
            double noteX = 0.02, noteY = 0.02, yIncrement = 0.006;
            notes.Reverse();

            int inserted = 0;
            foreach (var noteText in notes)
            {
                if (InsertNote(swDraw, noteText, noteX, noteY, layerName))
                {
                    inserted++;
                    noteY += yIncrement;
                }
                else
                {
                    Logger.Warning($"Failed to insert note: {noteText}");
                }
            }

            Logger.Info($"Inserted {inserted}/{notes.Count} notes on layer '{layerName}'");
        }


         private void ConfigureLayer(LayerMgr lyrMgr, string layerName)
         {
            // This method seems redundant if AddNotes/BendLine assignment handles layer creation/visibility.
            // Can be removed or kept for explicit configuration if needed elsewhere.
            // Ensure check is != 1 if kept.
             try
            {
                 if (lyrMgr == null || string.IsNullOrEmpty(layerName)) return;
                 Layer layerObj = lyrMgr.GetLayer(layerName) as Layer;
                 if (layerObj == null) {
                      // ... (AddLayer logic with != 1 check) ...
                 }
                 // ... (Set properties if needed) ...
            } catch (Exception ex) { Logger.Error($"Error configuring layer '{layerName}': {ex.Message}"); }
         }


        private void LoadSettings() { settings = Settings.LoadSettings() ?? new Settings(); } // Simplified
        private void EnsureDirectoriesExist() {
             try {
                 // *** Fully qualify System.Environment to resolve ambiguity CS0104 ***
                 string defaultDocs = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
                 string exportFolder = settings?.LastExportFolder ?? defaultDocs;

                 if (!string.IsNullOrEmpty(exportFolder) && !Directory.Exists(exportFolder)) {
                     Directory.CreateDirectory(exportFolder);
                     Logger.Info($"Created output directory: {exportFolder}");
                 }
                 // Ensure Log directory exists too if needed
                  if (!string.IsNullOrEmpty(settings?.LogFilePath)) {
                       string logDir = Path.GetDirectoryName(settings.LogFilePath);
                       if (!string.IsNullOrEmpty(logDir) && !Directory.Exists(logDir)) Directory.CreateDirectory(logDir);
                  }
             } catch (Exception ex) { Logger.Error($"Error ensuring directories exist: {ex.Message}"); }
        }

        private void ProcessAssemblyComponentsRecursively(Component2 swRootComp, string outputFolder, ref int sheetMetalPartsFound, ref int totalProcessed, List<string> processedFiles, Dictionary<string, int> materialThicknessCounts)
        {
            // Placeholder implementation
            // TODO: Implement the actual logic for processing assembly components recursively
            Logger.Info("Processing components recursively...");
            // Example logic: Iterate over child components and process them
            object[] childComponents = swRootComp.GetChildren();
            foreach (Component2 childComp in childComponents)
            {
                // Check if the component is a sheet metal part and process it
                if (IsSheetMetalPart(childComp.GetModelDoc2() as PartDoc))
                {
                    sheetMetalPartsFound++;
                    // Process the sheet metal part
                    string resultPath = ProcessSheetMetalPart(childComp.GetModelDoc2() as PartDoc, outputFolder);
                    if (!string.IsNullOrEmpty(resultPath))
                    {
                        totalProcessed++;
                        processedFiles.Add(resultPath);
                        // Update material thickness counts
                        string materialThickness = GetSheetMetalThickness(childComp.GetModelDoc2() as PartDoc, childComp.ReferencedConfiguration);
                        if (!string.IsNullOrEmpty(materialThickness))
                        {
                            if (materialThicknessCounts.ContainsKey(materialThickness))
                            {
                                materialThicknessCounts[materialThickness]++;
                            }
                            else
                            {
                                materialThicknessCounts[materialThickness] = 1;
                            }
                        }
                    }
                }
                // Recursively process child components
                ProcessAssemblyComponentsRecursively(childComp, outputFolder, ref sheetMetalPartsFound, ref totalProcessed, processedFiles, materialThicknessCounts);
            }
        }

        private void ExportResultsToExcel(List<string> processedFiles, Dictionary<string, int> materialThicknessCounts, List<string> thicknessFailures, List<string> exportErrors, string outputFolder)
        {
            // Placeholder implementation
            // TODO: Implement the actual logic for exporting results to Excel
            Logger.Info("Exporting results to Excel...");
            // Example logic: Create an Excel file and write data
            string excelFilePath = Path.Combine(outputFolder, "ExportResults.xlsx");
            using (var package = new OfficeOpenXml.ExcelPackage())
            {
                var worksheet = package.Workbook.Worksheets.Add("Results");
                // Write headers
                worksheet.Cells[1, 1].Value = "Processed Files";
                worksheet.Cells[1, 2].Value = "Material Thickness Counts";
                worksheet.Cells[1, 3].Value = "Thickness Failures";
                worksheet.Cells[1, 4].Value = "Export Errors";
                // Write data
                int row = 2;
                foreach (var file in processedFiles)
                {
                    worksheet.Cells[row, 1].Value = file;
                    row++;
                }
                row = 2;
                foreach (var entry in materialThicknessCounts)
                {
                    worksheet.Cells[row, 2].Value = $"{entry.Key}: {entry.Value}";
                    row++;
                }
                row = 2;
                foreach (var failure in thicknessFailures)
                {
                    worksheet.Cells[row, 3].Value = failure;
                    row++;
                }
                row = 2;
                foreach (var error in exportErrors)
                {
                    worksheet.Cells[row, 4].Value = error;
                    row++;
                }
                // Save the Excel file
                package.SaveAs(new FileInfo(excelFilePath));
            }
            Logger.Info($"Results exported to Excel at: {excelFilePath}");
        }

        #endregion Private Methods

    } // End class
} // End namespace