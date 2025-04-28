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

        private void AddNotesToDrawing(DrawingDoc swDrawingDoc, ModelDoc2 swPartModel, string configName, string thickness)
        {
            ModelDoc2 swDraw = swDrawingDoc as ModelDoc2;
            if (swDraw == null)
            {
                LogToFile("ERROR: Could not get ModelDoc2 from DrawingDoc in AddNotesToDrawing");
                return;
            }

            try
            {
                // Get/Create Notes Layer
                LayerMgr lyrMgr = swDraw.GetLayerManager();
                if (lyrMgr == null)
                {
                    LogToFile("ERROR: Could not get Layer Manager");
                    return;
                }

                string layerName = "Notes";
                Layer notesLayer = lyrMgr.GetLayer(layerName);
                
                if (notesLayer == null)
                {
                    LogToFile("Creating new 'Notes' layer");
                    
                    // Create the Notes layer with magenta color and continuous line
                    int magentaColor = ColorTranslator.ToWin32(Color.Magenta);
                    bool result = swDrawingDoc.CreateLayer2(layerName, 
                                                          "Property Notes",
                                                          magentaColor,
                                                          (int)swLineStyles_e.swLineCONTINUOUS,
                                                          (int)swLineWeights_e.swLW_NORMAL,
                                                          true,
                                                          true);
                    
                    if (!result)
                    {
                        LogToFile("ERROR: Failed to create 'Notes' layer");
                        return;
                    }
                    
                    notesLayer = lyrMgr.GetLayer(layerName);
                }
                else
                {
                    LogToFile("Using existing 'Notes' layer");
                    
                    // Ensure layer properties are set correctly
                    notesLayer.Color = ColorTranslator.ToWin32(Color.Magenta);
                    notesLayer.Visible = true;
                    notesLayer.Printable = true;
                }

                CustomPropertyManager propMgr = swPartModel.Extension.CustomPropertyManager[configName];
                if (propMgr == null)
                {
                    LogToFile("ERROR: Could not get CustomPropertyManager");
                    return;
                }

                // Get all metadata values
                string partNum = GetCustomPropertyValue(propMgr, "Part Number");
                string desc = GetCustomPropertyValue(propMgr, "Description");
                string rev = GetCustomPropertyValue(propMgr, "Revision");
                string mat = GetCustomPropertyValue(propMgr, "Material");
                string route = GetCustomPropertyValue(propMgr, "Shop Route");

                LogToFile("Adding notes to drawing with values:");
                LogToFile($"  - Part Number: {partNum}");
                LogToFile($"  - Description: {desc}");
                LogToFile($"  - Revision: {rev}");
                LogToFile($"  - Material: {mat}");
                LogToFile($"  - Thickness: {thickness}");
                LogToFile($"  - Shop Route: {route}");

                // Create notes array in the same order as Ben's macro
                string[] notes = new string[]
                {
                    $"Shop Route: {route}",
                    $"Thickness: {thickness}",
                    $"Material: {mat}",
                    $"Revision: {rev}",
                    $"Description: {desc}",
                    $"Part Number: {partNum}"
                };

                // Position settings for notes
                double noteX = 0.02;
                double noteY = 0.02;
                double yIncrement = 0.006;

                // Add each note
                foreach (string noteText in notes)
                {
                    if (!string.IsNullOrEmpty(noteText))
                    {
                        Note swNote = swDraw.InsertNote(noteText);
                        if (swNote != null)
                        {
                            Annotation ann = swNote.GetAnnotation();
                            if (ann != null)
                            {
                                ann.Layer = layerName;  // Assign to Notes layer
                                ann.SetPosition2(noteX, noteY, 0);
                                LogToFile($"Added note to layer '{layerName}': {noteText}");
                                noteY += yIncrement;
                            }
                        }
                        else
                        {
                            LogToFile($"ERROR: Failed to insert note: {noteText}");
                        }
                    }
                }

                LogToFile("Successfully added all notes to drawing");
            }
            catch (Exception ex)
            {
                LogToFile($"ERROR in AddNotesToDrawing: {ex.Message}\n{ex.StackTrace}");
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
                 settings.LastExportFolder = Path.GetDirectoryName(outputPath);
                 settings.SaveSettings();

                 string result = ProcessSheetMetalPart(swPart, Path.GetDirectoryName(outputPath), Path.GetFileNameWithoutExtension(outputPath));

                 if (!string.IsNullOrEmpty(result))
                 {
                      Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Single part DXF exported successfully to: {result}"); // DEBUG
                      Logger.Info($"Single part DXF exported successfully to: {result}");
                 }
                 else
                 {
                      Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Single part DXF export failed for: {swModel.GetPathName()}"); // DEBUG
                      Logger.Warning($"Single part DXF export failed for: {swModel.GetPathName()}");
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
            
            return ProcessSheetMetalPart(swPart, outputPath);
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

            try
            {
                swModel = swPart as ModelDoc2;
                string partPath = swModel.GetPathName();
                partTitle = swModel.GetTitle(); // Assign value inside try
                fileName = baseFileName ?? Path.GetFileNameWithoutExtension(partTitle); // Assign value inside try
                string outputPath = Path.Combine(outputFolder, fileName + ".dxf");

                Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Processing part: {partTitle} for DXF export to {outputPath}"); // DEBUG
                Logger.Info($"Processing part: {partTitle} for DXF export to {outputPath}");

                string originalConfig = swModel.ConfigurationManager.ActiveConfiguration.Name;
                string flatConfig = FindFlatPatternConfig(swModel);

                if (string.IsNullOrEmpty(flatConfig))
                {
                    Debug.WriteLine($"[DEBUG] Attempting Logger.Error: No Flat-Pattern configuration found for {partTitle}. Cannot export DXF."); // DEBUG
                    Logger.Error($"No Flat-Pattern configuration found for {partTitle}. Cannot export DXF.");
                    exportErrors.Add($"No Flat-Pattern configuration found for {fileName}");
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

                ModelDoc2 swDraw = null;
                string tempDrawingPath = Path.Combine(Path.GetTempPath(), fileName + "_tempDXF.SLDDRW");
                try
                {
                     string drwTemplate = _swApp.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing);
                     if (string.IsNullOrEmpty(drwTemplate) || !File.Exists(drwTemplate))
                     {
                         Debug.WriteLine($"[DEBUG] Attempting Logger.Warning: Default drawing template not found or invalid. Using blank."); // DEBUG
                         Logger.Warning("Default drawing template not found or invalid. Using blank.");
                         drwTemplate = "";
                     }

                     swDraw = _swApp.NewDocument(drwTemplate, (int)swDwgPaperSizes_e.swDwgPaperAsize, 0, 0) as ModelDoc2;
                     if (swDraw == null) throw new Exception("Failed to create new drawing document.");

                     DrawingDoc swDrawingDoc = swDraw as DrawingDoc;

                     SwView flatView = swDrawingDoc.CreateFlatPatternViewFromModelView3(partPath, flatConfig, 0.05, 0.05, 0, false, false);
                     if (flatView == null) throw new Exception("Failed to create flat pattern view in drawing.");
                     Debug.WriteLine($"[DEBUG] Attempting Logger.Info: Created flat pattern view."); // DEBUG
                     Logger.Info("Created flat pattern view.");

                     AddNotesToDrawing(swDrawingDoc, swModel, originalConfig, thickness);

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
                    swModel.ShowConfiguration2(originalConfig);
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
            File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                $"\n[{DateTime.Now}] Checking sheet metal thickness for {partTitle} ({partPath}) in config {configName}");

            CustomPropertyManager propMgr = swModel.Extension.CustomPropertyManager[configName];
            if (propMgr == null)
            {
                File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                    $"\n[{DateTime.Now}] ERROR: Failed to get CustomPropertyManager for {partTitle} config {configName}");
                return null;
            }

            string val = "";
            string resVal = "";
            bool wasRes = false;
            bool wasLinked = false;

            // Check custom property first
            File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                $"\n[{DateTime.Now}] Checking 'Sheet Metal Thickness' custom property for {partTitle}");
            
            int ret = propMgr.Get6("Sheet Metal Thickness", true, out val, out resVal, out wasRes, out wasLinked);
            
            File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                $"\n[{DateTime.Now}] Property check results for {partTitle}:" +
                $"\n  - Return code: {ret}" +
                $"\n  - Raw value: '{val}'" +
                $"\n  - Resolved value: '{resVal}'" +
                $"\n  - Was resolved: {wasRes}" +
                $"\n  - Is linked: {wasLinked}");

            // If we have a valid resolved value that's not an equation
            if (ret == 1 && wasRes && !string.IsNullOrEmpty(resVal) && !resVal.Contains("Thickness@"))
            {
                File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                    $"\n[{DateTime.Now}] Using existing thickness value for {partTitle}: {resVal}");
                return resVal;
            }

            // Get thickness from feature
            File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                $"\n[{DateTime.Now}] Checking SheetMetal feature for {partTitle}");

            Feature swSMFeat = FindFeatureByType(swPart, "SheetMetal");
            if (swSMFeat != null)
            {
                SheetMetalFeatureData swSMData = swSMFeat.GetDefinition() as SheetMetalFeatureData;
                if (swSMData != null)
                {
                    double thicknessMeters = swSMData.Thickness;
                    double thicknessInches = Math.Round(thicknessMeters * 39.3701, 4);
                    string formattedThickness = thicknessInches.ToString("0.0000");

                    File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                        $"\n[{DateTime.Now}] Found thickness in feature for {partTitle}:" +
                        $"\n  - Meters: {thicknessMeters}" +
                        $"\n  - Inches: {thicknessInches}" +
                        $"\n  - Formatted: {formattedThickness}");

                    // Update the custom property
                    File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                        $"\n[{DateTime.Now}] Updating thickness custom property for {partTitle}");

                    propMgr.Add3("Sheet Metal Thickness", 
                               (int)swCustomInfoType_e.swCustomInfoText, 
                               formattedThickness, 
                               (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);

                    // Also update these properties to match Ben's macro
                    UpdateCustomProperty(propMgr, "Part Number", partTitle.Replace(".SLDPRT", ""));
                    UpdateCustomProperty(propMgr, "Description", GetCustomPropertyValue(propMgr, "Description"));
                    UpdateCustomProperty(propMgr, "Revision", GetCustomPropertyValue(propMgr, "Revision"));
                    UpdateCustomProperty(propMgr, "Material", GetCustomPropertyValue(propMgr, "Material"));
                    UpdateCustomProperty(propMgr, "Shop Route", GetCustomPropertyValue(propMgr, "Shop Route"));

                    File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                        $"\n[{DateTime.Now}] Successfully updated all properties for {partTitle}");

                    return formattedThickness;
                }
                else
                {
                    File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                        $"\n[{DateTime.Now}] ERROR: Could not get SheetMetalFeatureData for {partTitle}");
                }
            }
            else
            {
                File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                    $"\n[{DateTime.Now}] ERROR: No SheetMetal feature found in {partTitle}");
            }

            File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                $"\n[{DateTime.Now}] WARNING: Could not determine thickness for {partTitle}");
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
                
                File.AppendAllText(@"c:\Users\Ben Roland\OneDrive - Roeslein\Documents\RoesleinAddIn.log", 
                    $"\n[{DateTime.Now}] Updated {propName}: {value}");
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
    }
} 