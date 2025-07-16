using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using System.Windows.Forms;

namespace RoesleinAddIn
{
    /// <summary>
    /// Manages commands and menu items for the Roeslein Add-in
    /// </summary>
    public class CommandManager
    {
        private readonly ISldWorks _swApp;
        private readonly ICommandManager _cmdMgr;
        private readonly int _cmdGroupId;
        private readonly int _cmdTabId;
        private readonly SheetMetalProcessor _processor;
        
        // Define command IDs
        private const int CMD_ExportDXF = 0;
        private const int CMD_Settings = 1;
        private const int CMD_About = 2;
        
        /// <summary>
        /// Constructor
        /// </summary>
        public CommandManager(ISldWorks swApp, SheetMetalProcessor processor)
        {
            _swApp = swApp;
            _processor = processor;
            _cmdMgr = _swApp.GetCommandManager(0);
            _cmdGroupId = _cmdMgr.CreateCommandGroup2(0, "Roeslein Tools", "Roeslein Tools", "", -1, true, ref _cmdTabId);
            
            if (_cmdGroupId > 0)
            {
                AddCommands();
                _cmdMgr.ActivateTab(_cmdTabId, _cmdGroupId);
            }
        }
        
        private void AddCommands()
        {
            // Add Process Assembly button
            _cmdMgr.AddCommandItem2("Process Assembly", -1, "Process Assembly", "Process Assembly", -1, 
                "Process all sheet metal parts in the assembly", "Process all sheet metal parts in the assembly", 
                _cmdGroupId, (int)swCommandItemType_e.swToolbarButton, _cmdGroupId, ref _cmdTabId);

            // Add Process Part button
            _cmdMgr.AddCommandItem2("Process Part", -1, "Process Part", "Process Part", -1, 
                "Process the current sheet metal part", "Process the current sheet metal part", 
                _cmdGroupId, (int)swCommandItemType_e.swToolbarButton, _cmdGroupId, ref _cmdTabId);

            // Add Settings button
            _cmdMgr.AddCommandItem2("Settings", -1, "Settings", "Settings", -1, 
                "Configure add-in settings", "Configure add-in settings", 
                _cmdGroupId, (int)swCommandItemType_e.swToolbarButton, _cmdGroupId, ref _cmdTabId);

            // Set up command callbacks
            _cmdMgr.AddCallback(_cmdGroupId, "Process Assembly", ProcessAssemblyCallback);
            _cmdMgr.AddCallback(_cmdGroupId, "Process Part", ProcessPartCallback);
            _cmdMgr.AddCallback(_cmdGroupId, "Settings", SettingsCallback);
        }

        private void ProcessAssemblyCallback()
        {
            _processor.ProcessAssemblySheetMetal();
        }

        private void ProcessPartCallback()
        {
            _processor.ProcessSinglePartSheetMetal();
        }

        private void SettingsCallback()
        {
            using (var settingsForm = new SettingsFormV2())
            {
                settingsForm.ShowDialog();
            }
        }

        public void Remove()
        {
            if (_cmdMgr != null && _cmdGroupId > 0)
            {
                _cmdMgr.RemoveCommandGroup(_cmdGroupId);
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