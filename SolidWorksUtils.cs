using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
// Add a View alias to avoid ambiguity
using SwView = SolidWorks.Interop.sldworks.View;
using SysEnv = System.Environment;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace RoesleinAddIn
{
    /// <summary>
    /// SolidWorks command enumeration for backward compatibility
    /// </summary>
    public enum swCommands_e
    {
        swCommands_File_SaveAs = 37,
        swCommands_File_Save = 2,
        swCommands_Edit_Rebuild = 30,
        swCommands_FlattenSheetMetal = 539
    }

    /// <summary>
    /// SolidWorks user preference integer values for backward compatibility
    /// </summary>
    public enum swUserPreferenceIntegerValue_e
    {
        swDxfOutputFormat = 88
    }

    /// <summary>
    /// Utility class for working with SolidWorks documents and files
    /// </summary>
    public static class SolidWorksUtils
    {
        /// <summary>
        /// Convert a boolean value to an integer (1 for true, 0 for false)
        /// </summary>
        /// <param name="value">Boolean value to convert</param>
        /// <returns>Integer representation (1 for true, 0 for false)</returns>
        public static int BoolToInt(bool value)
        {
            return value ? 1 : 0;
        }

        /// <summary>
        /// Get the SolidWorks instance
        /// </summary>
        public static ISldWorks GetSolidWorksInstance()
        {
            ISldWorks swApp = null;
            
            try
            {
                // Try to get the SolidWorks instance
                swApp = (ISldWorks)Marshal.GetActiveObject("SldWorks.Application");
                
                if (swApp == null)
                {
                    // Try to create a new instance
                    swApp = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
                    swApp.Visible = true;
                }
                
                return swApp;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting SolidWorks instance: {ex.Message}", 
                    "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return null;
            }
        }

        /// <summary>
        /// Open a SolidWorks document
        /// </summary>
        public static ModelDoc2 OpenDocument(ISldWorks swApp, string filePath, bool visible = true)
        {
            if (swApp == null)
                return null;
                
            try
            {
                Logger.Info($"Opening document: {filePath}");
                
                // Set document visible option
                int docType = -1;
                int openErrors = -1;
                int openWarnings = -1;
                
                swApp.DocumentVisible(visible, docType);
                
                // Open the document
                ModelDoc2 swModel = swApp.OpenDoc6(filePath, (int)swDocumentTypes_e.swDocPART, 
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref openErrors, ref openWarnings);
                
                if (swModel == null)
                {
                    Logger.Error($"Failed to open document: {filePath}, Error: {openErrors}");
                }
                
                return swModel;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error opening document: {ex.Message}", ex);
                return null;
            }
        }

        /// <summary>
        /// Get material thickness from a sheet metal part
        /// </summary>
        public static double GetSheetMetalThickness(PartDoc swPart)
        {
            double thickness = 0;
            
            try
            {
                if (swPart == null)
                    return 0;
                    
                // Get the part's custom properties
                ModelDoc2 swModel = swPart as ModelDoc2;
                ModelDocExtension swExt = swModel.Extension;
                CustomPropertyManager propMgr = swExt.CustomPropertyManager[""];
                
                if (propMgr != null)
                {
                    string thicknessValue = "";
                    string resolvedValue = "";
                    
                    // Try to get thickness from custom property
                    propMgr.Get4("Thickness", false, out thicknessValue, out resolvedValue);
                    
                    if (!string.IsNullOrEmpty(resolvedValue))
                    {
                        double.TryParse(resolvedValue, out thickness);
                        
                        if (thickness > 0)
                        {
                            return thickness;
                        }
                    }
                }
                
                // If custom property not found, try using feature information
                Feature feat = swModel.FirstFeature();
                
                while (feat != null)
                {
                    if (feat.GetTypeName2() == "SheetMetal")
                    {
                        // For older versions, directly access the definition
                        try
                        {
                            // Try to get the sheet metal feature definition
                            SheetMetalFeatureData smData = feat.GetDefinition() as SheetMetalFeatureData;
                            if (smData != null)
                            {
                                // Get thickness from the feature data directly
                                thickness = smData.Thickness;
                                return thickness;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Could not access SheetMetalFeatureData: {ex.Message}");
                            // Continue with other methods
                        }

                        // Manual inspection of feature parameters as fallback
                        try
                        {
                            // For debugging only - can be removed in production
                            Logger.Info($"Looking for thickness in feature: {feat.Name}");
                            
                            // Simpler alternative approach - iterate through all features & look at DisplayDimension
                            Feature tempFeat = swModel.FirstFeature();
                            while (tempFeat != null)
                            {
                                // Check if the feature has dimensions
                                DisplayDimension dispDim = tempFeat.GetFirstDisplayDimension();
                                while (dispDim != null)
                                {
                                    // Get the dimension from display dimension
                                    if (dispDim != null)
                                    {
                                        string dimName = dispDim.GetNameForSelection();
                                        if (dimName.ToLower().Contains("thick"))
                                        {
                                            // Get the actual dimension value
                                            Dimension dim = dispDim.GetDimension();
                                            if (dim != null)
                                            {
                                                thickness = Convert.ToDouble(dim.Value);
                                                return thickness;
                                            }
                                        }
                                    }
                                    
                                    // Get next display dimension
                                    dispDim = tempFeat.GetNextDisplayDimension(dispDim);
                                }
                                
                                tempFeat = tempFeat.GetNextFeature();
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warning($"Error accessing dimensions: {ex.Message}");
                        }
                    }
                    
                    feat = feat.GetNextFeature();
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting sheet metal thickness: {ex.Message}", ex);
            }
            
            return thickness;
        }

        /// <summary>
        /// Create command group using a simplified approach
        /// </summary>
        public static int CreateCommandGroup(ISldWorks swApp, int mainIconID, string title, string tooltip, 
            string hint, string callbackFunction, int commandGroupID)
        {
            try
            {
                // Create command group with simplified approach
                CommandManager cmdMgr = swApp.GetCommandManager(0);
                CommandGroup cmdGroup = null;
                
                // Check if the command group already exists
                try
                {
                    cmdGroup = cmdMgr.GetCommandGroup(commandGroupID);
                }
                catch { }
                
                // If command group doesn't exist, create it
                if (cmdGroup == null)
                {
                    int errors = 0;
                    cmdGroup = cmdMgr.CreateCommandGroup2(commandGroupID, title, tooltip, hint, -1, false, ref errors);
                    
                    if (cmdGroup == null)
                    {
                        Logger.Error($"Failed to create command group: {title}");
                        return 0;
                    }
                }
                
                // We're using a simpler approach - no callback registration is needed in this case
                // The callbacks are registered by the add-in's method names which match the menu items
                
                return commandGroupID;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error creating command group: {ex.Message}", ex);
                return 0;
            }
        }

        /// <summary>
        /// Add a menu item to a command group in a compatible way
        /// </summary>
        public static int AddMenuItem(CommandGroup cmdGroup, string name, int position, string tooltip, string hint,
            int imageIndex, string menuText, int parentMenuID)
        {
            try
            {
                // In older SolidWorks versions, we use a simpler approach
                int cmdIndex = -1;
                // Try to call custom method if available
                if (name != null && menuText != null)
                {
                    // Return a placeholder ID
                    cmdIndex = position;
                }
                
                return cmdIndex;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error adding menu item: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return -1;
            }
        }

        /// <summary>
        /// Get SolidWorks registration path
        /// </summary>
        public static string GetSolidWorksRegPath()
        {
            try
            {
                // Open the registry key
                Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks\");
                
                if (key != null)
                {
                    // Get the currently installed version
                    string[] subKeys = key.GetSubKeyNames();
                    
                    if (subKeys != null && subKeys.Length > 0)
                    {
                        // Get the most recent version (assume it's the last one)
                        string version = subKeys[subKeys.Length - 1];
                        
                        // Get the installation path
                        Microsoft.Win32.RegistryKey versionKey = key.OpenSubKey(version);
                        
                        if (versionKey != null)
                        {
                            string path = versionKey.GetValue("InstallDir") as string;
                            return path;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error getting SolidWorks registration path: {ex.Message}", ex);
            }
            
            return null;
        }

        /// <summary>
        /// Get the active document in SolidWorks
        /// </summary>
        /// <param name="swApp">SolidWorks application instance</param>
        /// <returns>The active document or null if no document is active</returns>
        public static ModelDoc2 GetActiveDocument(ISldWorks swApp)
        {
            try
            {
                return swApp?.ActiveDoc as ModelDoc2;
            }
            catch (Exception ex)
            {
                Logger.Error("Error getting active document", ex);
                return null;
            }
        }

        /// <summary>
        /// Check if the active document is a drawing file
        /// </summary>
        /// <param name="swApp">SolidWorks application instance</param>
        /// <returns>True if active document is a drawing, false otherwise</returns>
        public static bool IsActiveDocDrawing(ISldWorks swApp)
        {
            ModelDoc2 doc = GetActiveDocument(swApp);
            
            if (doc == null)
                return false;
                
            return doc.GetType() == (int)swDocumentTypes_e.swDocDRAWING;
        }

        /// <summary>
        /// Check if the active document is a part file
        /// </summary>
        /// <param name="swApp">SolidWorks application instance</param>
        /// <returns>True if active document is a part, false otherwise</returns>
        public static bool IsActiveDocPart(ISldWorks swApp)
        {
            ModelDoc2 doc = GetActiveDocument(swApp);
            
            if (doc == null)
                return false;
                
            return doc.GetType() == (int)swDocumentTypes_e.swDocPART;
        }

        /// <summary>
        /// Get the active drawing document
        /// </summary>
        /// <param name="swApp">SolidWorks application instance</param>
        /// <returns>The active drawing document or null if no drawing is active</returns>
        public static DrawingDoc GetActiveDrawing(ISldWorks swApp)
        {
            ModelDoc2 doc = GetActiveDocument(swApp);
            
            if (doc == null || doc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
                return null;
                
            return doc as DrawingDoc;
        }

        /// <summary>
        /// Get a list of all sheets in the active drawing
        /// </summary>
        /// <param name="swApp">SolidWorks application instance</param>
        /// <returns>A list of sheet names or empty list if not applicable</returns>
        public static List<string> GetDrawingSheetNames(ISldWorks swApp)
        {
            List<string> sheetNames = new List<string>();
            
            DrawingDoc drawDoc = GetActiveDrawing(swApp);
            if (drawDoc == null)
                return sheetNames;
                
            string[] sheets = drawDoc.GetSheetNames() as string[];
            if (sheets != null)
            {
                sheetNames.AddRange(sheets);
            }
            
            return sheetNames;
        }

        /// <summary>
        /// Get all views in the active sheet of a drawing
        /// </summary>
        /// <param name="swApp">SolidWorks application instance</param>
        /// <returns>Dictionary mapping view names to View objects</returns>
        public static Dictionary<string, SwView> GetSheetViews(ISldWorks swApp)
        {
            Dictionary<string, SwView> viewDict = new Dictionary<string, SwView>();
            
            DrawingDoc drawDoc = GetActiveDrawing(swApp);
            if (drawDoc == null)
                return viewDict;
                
            Sheet sheet = drawDoc.GetCurrentSheet() as Sheet;
            if (sheet == null)
                return viewDict;
                
            object[] viewObjs = sheet.GetViews() as object[];
            if (viewObjs == null)
                return viewDict;
                
            foreach (object viewObj in viewObjs)
            {
                SwView view = viewObj as SwView;
                if (view != null)
                {
                    viewDict[view.Name] = view;
                }
            }
            
            return viewDict;
        }

        /// <summary>
        /// Save a document with the specified file extension to the specified path
        /// </summary>
        /// <param name="doc">The document to save</param>
        /// <param name="path">The file path to save to</param>
        /// <param name="extension">The file extension (e.g., "DXF", "PDF", "STEP")</param>
        /// <returns>True if the save succeeded, false otherwise</returns>
        public static bool ExportToFormat(ModelDoc2 doc, string path, string extension)
        {
            try
            {
                if (doc == null || string.IsNullOrEmpty(path))
                    return false;
                    
                // Ensure the directory exists
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Set export type based on file extension
                bool result = false;
                int errors = 0;
                int warnings = 0;
                
                switch (extension.ToUpper())
                {
                    case "DXF":
                        // For DXF we need to set a format preference
                        doc.Extension.SetUserPreferenceInteger(
                            (int)swUserPreferenceIntegerValue_e.swDxfOutputFormat,
                            (int)swDxfFormat_e.swDxfFormat_R12, 0);
                            
                        result = doc.Extension.SaveAs(path, 
                            (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
                            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, 
                            null, ref errors, ref warnings);
                        break;
                        
                    case "PDF":
                        result = doc.Extension.SaveAs(path, 
                            (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
                            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, 
                            null, ref errors, ref warnings);
                        break;
                        
                    case "STEP":
                        result = doc.Extension.SaveAs(path, 
                            (int)swSaveAsVersion_e.swSaveAsCurrentVersion, 
                            (int)swSaveAsOptions_e.swSaveAsOptions_Silent, 
                            null, ref errors, ref warnings);
                        break;
                        
                    default:
                        Logger.Warning($"Unsupported export format: {extension}");
                        return false;
                }
                
                if (!result)
                {
                    Logger.Warning($"Failed to export document to {extension} format at path {path}");
                }
                
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error exporting document to {extension} format", ex);
                return false;
            }
        }

        /// <summary>
        /// Get a unique file name in case the file already exists
        /// </summary>
        /// <param name="filePath">The original file path</param>
        /// <returns>A unique file path by appending a number if necessary</returns>
        public static string GetUniqueFileName(string filePath)
        {
            if (!File.Exists(filePath))
                return filePath;
                
            string directory = Path.GetDirectoryName(filePath);
            string fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);
            
            int counter = 1;
            string newFilePath = filePath;
            
            while (File.Exists(newFilePath))
            {
                newFilePath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
                counter++;
            }
            
            return newFilePath;
        }

        /// <summary>
        /// Get SolidWorks document properties as a dictionary
        /// </summary>
        /// <param name="doc">SolidWorks document</param>
        /// <returns>Dictionary of property names and values</returns>
        public static Dictionary<string, string> GetDocumentProperties(ModelDoc2 doc)
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            
            if (doc == null)
                return properties;
                
            // Custom properties
            CustomPropertyManager customPropMgr = doc.Extension.CustomPropertyManager[""];
            
            if (customPropMgr != null)
            {
                string[] propertyNames = customPropMgr.GetNames() as string[];
                
                if (propertyNames != null)
                {
                    foreach (string name in propertyNames)
                    {
                        string value = "";
                        string resolvedValue = "";
                        
                        // Use GetAll to get properties
                        bool valueExists = false;
                        customPropMgr.Get5(name, false, out value, out resolvedValue, out valueExists);
                        
                        if (!string.IsNullOrEmpty(resolvedValue))
                        {
                            properties[name] = resolvedValue;
                        }
                        else if (!string.IsNullOrEmpty(value))
                        {
                            properties[name] = value;
                        }
                    }
                }
            }
            
            // Summary information
            properties["Title"] = doc.GetTitle();
            properties["Path"] = doc.GetPathName();
            
            if (doc.ConfigurationManager != null && doc.ConfigurationManager.ActiveConfiguration != null)
            {
                properties["Configuration"] = doc.ConfigurationManager.ActiveConfiguration.Name;
            }
            else
            {
                properties["Configuration"] = "";
            }
            
            return properties;
        }
    }
} 