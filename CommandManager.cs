using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace RoesleinAddIn
{
    /// <summary>
    /// Manages commands and menu items for the Roeslein Add-in
    /// </summary>
    public class CommandManager
    {
        private readonly ISldWorks _swApp;
        private readonly RoesleinAddIn _addIn;
        private int _cmdGroupId;
        
        // Define command IDs
        private const int CMD_ExportDXF = 0;
        private const int CMD_Settings = 1;
        private const int CMD_About = 2;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CommandManager(ISldWorks swApp, RoesleinAddIn addIn)
        {
            _swApp = swApp;
            _addIn = addIn;
            _cmdGroupId = 1;  // Unique ID for the command group
        }
        
        /// <summary>
        /// Initialize the command manager
        /// </summary>
        public bool Initialize()
        {
            try
            {
                // Create unique command group ID
                string addInTitle = "Roeslein";
                _cmdGroupId = addInTitle.GetHashCode();
                
                // Ensure positive ID
                if (_cmdGroupId < 0)
                    _cmdGroupId = _cmdGroupId * -1;
                
                // For SolidWorks, we'll use a more direct approach with older APIs
                // Create a simple menu instead of command group for older versions
                try
                {
                    // Check if menu exists
                    string menuToolbarKey = "Roeslein Add-in";
                    
                    // Remove existing menu if it exists
                    object menuState = _swApp.GetToolbarState(menuToolbarKey, 0, 1);
                    int menuStateInt = 0;
                    if (menuState != null)
                    {
                        try
                        {
                            menuStateInt = Convert.ToInt32(menuState);
                            if (menuStateInt != 0)
                            {
                                _swApp.RemoveMenu((int)swDocumentTypes_e.swDocNONE, menuToolbarKey, "");
                            }
                        }
                        catch
                        {
                            // Ignore conversion errors
                        }
                    }
                    
                    // Add menu items with callback registration for older SolidWorks versions
                    // Register the callback using SetAddinCallbackFunction
                    // (Not needed in this version, the method name matching is sufficient)
                    
                    // Add our menu items to the main menu using older API methods with compatible signature
                    // In this version, AddMenuItem takes 4 arguments 
                    // (docType, itemName, position, menuID)
                    _swApp.AddMenuItem(
                        (int)swDocumentTypes_e.swDocPART, 
                        "ExportDXF", 
                        0, 
                        menuToolbarKey);
                        
                    _swApp.AddMenuItem(
                        (int)swDocumentTypes_e.swDocPART, 
                        "ShowSettings", 
                        0, 
                        menuToolbarKey);
                        
                    _swApp.AddMenuItem(
                        (int)swDocumentTypes_e.swDocPART, 
                        "ShowAbout", 
                        0, 
                        menuToolbarKey);
                    
                    // Try to add to assembly menus too
                    try {
                        _swApp.AddMenuItem(
                            (int)swDocumentTypes_e.swDocASSEMBLY, 
                            "ExportDXF", 
                            0, 
                            menuToolbarKey);
                    } catch (Exception) {
                        // Silently ignore errors adding to assembly menu
                    }
                    
                    Logger.Info("Menu items created successfully using older API");
                    return true;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error creating menu items: {ex.Message}", ex);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error initializing command manager: {ex.Message}", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Handle export DXF command
        /// </summary>
        public void ExportDXF()
        {
            try
            {
                ModelDoc2 swModel = _swApp.ActiveDoc as ModelDoc2;
                
                if (swModel == null)
                {
                    System.Windows.Forms.MessageBox.Show("No active document.", "Roeslein Add-in", 
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                
                // Check document type using int comparison for compatibility
                int docType = 0;
                docType = swModel.GetType();
                
                if (docType != (int)swDocumentTypes_e.swDocPART)
                {
                    System.Windows.Forms.MessageBox.Show("Please open a part document.", "Roeslein Add-in", 
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                
                PartDoc swPart = swModel as PartDoc;
                
                // Check if it's a sheet metal part
                if (SheetMetalProcessor.IsSheetMetalPart(swPart) == 0)
                {
                    System.Windows.Forms.MessageBox.Show("Not a sheet metal part.", "Roeslein Add-in", 
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
                    return;
                }
                
                // Show the export dialog
                var exportDialog = new ExportDialog();
                
                // Set default file name based on part name
                string partName = Path.GetFileNameWithoutExtension(swModel.GetPathName());
                exportDialog.FileName = partName;
                
                if (exportDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string outputPath = exportDialog.OutputPath;
                    
                    // Process sheet metal part
                    SheetMetalProcessor processor = new SheetMetalProcessor(_swApp);
                    string resultPath = processor.ProcessSheetMetalPart(swPart, outputPath);
                    
                    if (!string.IsNullOrEmpty(resultPath))
                    {
                        System.Windows.Forms.MessageBox.Show($"DXF file exported successfully to:\n{outputPath}", 
                            "Roeslein Add-in", System.Windows.Forms.MessageBoxButtons.OK, 
                            System.Windows.Forms.MessageBoxIcon.Information);
                    }
                    else
                    {
                        System.Windows.Forms.MessageBox.Show("Failed to export DXF file.", "Roeslein Add-in", 
                            System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling export DXF command: {ex.Message}", ex);
                System.Windows.Forms.MessageBox.Show($"Error: {ex.Message}", "Roeslein Add-in", 
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Handle settings command
        /// </summary>
        public void ShowSettings()
        {
            try
            {
                // Show settings dialog (to be implemented)
                System.Windows.Forms.MessageBox.Show("Settings dialog not yet implemented.", "Roeslein Add-in", 
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling settings command: {ex.Message}", ex);
                System.Windows.Forms.MessageBox.Show($"Error: {ex.Message}", "Roeslein Add-in", 
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
        
        /// <summary>
        /// Handle about command
        /// </summary>
        public void ShowAbout()
        {
            try
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                Version version = assembly.GetName().Version;
                
                string message = $"Roeslein Sheet Metal Add-in\nVersion {version}\n\n" +
                                 "Developed by Roeslein & Associates, Inc.\n" +
                                 "For technical support, contact IT department.";
                
                System.Windows.Forms.MessageBox.Show(message, "About Roeslein Add-in", 
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error handling about command: {ex.Message}", ex);
                System.Windows.Forms.MessageBox.Show($"Error: {ex.Message}", "Roeslein Add-in", 
                    System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
} 