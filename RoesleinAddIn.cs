using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swpublished;
using System.IO;
using System.Diagnostics;
using System.ComponentModel;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using RoesleinAddIn;
using EPDM.Interop.epdm;

namespace RoesleinAddIn
{
    /// <summary>
    /// Roeslein SolidWorks Add-in for Sheet Metal Export
    /// </summary>
    [Guid("8FD47D86-0F0C-4C16-8E89-4E12A9AD0C20")]  // Keep your existing GUID
    [ComVisible(true)]
    public class RoesleinAddIn : ISwAddin
    {
        #region Private Members
        private ISldWorks swApp;
        private ICommandManager cmdMgr;
        private int addinID;

        // Command IDs - use large numbers to avoid conflicts
        private const int CMD_GROUP_ID = 29876;
        private const int CMD_EXPORT_ID = 0;
        private const int CMD_SETTINGS_ID = 1;
        private const int CMD_PROCESS_ID = 2;
        private const int CMD_BOM_ID = 3;
        private const int CMD_ID_TAG_ID = 4;
        private const int CMD_APPLY_PROPS_ID = 5;
        private const int CMD_CREATE_EDRAWING_ID = 6;

        // Resources
        private string addinPath;

        // Main objects
        private ICommandGroup cmdGroup;
        private SheetMetalProcessor processor;
        private BomProcessor bomProcessor;
        private SettingsFormV2 settingsForm;

        // PDM Vault Object
        private EPDM.Interop.epdm.IEdmVault5 pdmVault;

        private PropertyStandardManager propertyStandardManager;
        #endregion

        #region COM Registration
        [ComRegisterFunction]
        public static void RegisterFunction(Type t)
        {
            try
            {
                string title = "Roeslein Connex";
                string description = "Roeslein Connex - Sheet Metal Export Tools";
                bool loadAtStartup = true;

                // The registry key for the add-in
                string addInRegKey = string.Format(@"SOFTWARE\SolidWorks\AddIns\{0}", t.GUID.ToString("B"));

                // Create the add-in's registry key
                using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(addInRegKey))
                {
                    // Set the add-in's title and description
                    rk.SetValue(null, 0);  // Required; default value
                    rk.SetValue("Title", title);
                    rk.SetValue("Description", description);
                }

                // The registry key for startup
                string addInStartupKey = string.Format(@"Software\SolidWorks\AddInsStartup\{0}", t.GUID.ToString("B"));

                // Create the startup key
                using (Microsoft.Win32.RegistryKey rk = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(addInStartupKey))
                {
                    rk.SetValue(null, Convert.ToInt32(loadAtStartup), Microsoft.Win32.RegistryValueKind.DWord);
                }

                Debug.WriteLine($"Registered add-in: {t.FullName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering add-in: {ex.Message}");
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterFunction(Type t)
        {
            try
            {
                // The registry key for the add-in
                string addInRegKey = string.Format(@"SOFTWARE\SolidWorks\AddIns\{0}", t.GUID.ToString("B"));

                // The registry key for startup
                string addInStartupKey = string.Format(@"Software\SolidWorks\AddInsStartup\{0}", t.GUID.ToString("B"));

                // Remove the registry keys
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(addInRegKey, false);
                Microsoft.Win32.Registry.CurrentUser.DeleteSubKey(addInStartupKey, false);

                Debug.WriteLine($"Unregistered add-in: {t.FullName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error unregistering add-in: {ex.Message}");
            }
        }
        #endregion

        #region ISwAddin Implementation
        /// <summary>
        /// Called when SolidWorks connects to this add-in
        /// </summary>
        public bool ConnectToSW(object ThisSW, int Cookie)
        {
            try
            {
                WriteToLog("=== ConnectToSW Started ===");
                Debug.WriteLine("=== ConnectToSW Started ===");

                // Store references
                swApp = (ISldWorks)ThisSW;
                addinID = Cookie;

                // Set callback info
                bool result = swApp.SetAddinCallbackInfo2(0, this, addinID);
                WriteToLog($"SetAddinCallbackInfo2 result: {result}");

                // Create path to add-in assembly
                addinPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                WriteToLog($"Add-in path: {addinPath}");
                WriteToLog($"Add-in path exists: {Directory.Exists(addinPath)}");

                // Create processors
                processor = new SheetMetalProcessor(swApp);
                bomProcessor = new BomProcessor(swApp);
                propertyStandardManager = new PropertyStandardManager(pdmVault, swApp, null);

                // Attempt to connect to PDM Vault for the active document
                TryConnectToPdmVaultForActiveDoc();

                // Add the CommandManager
                AddCommandMgr();
                
                WriteToLog("=== ConnectToSW Completed ===");
                return true;
            }
            catch (Exception ex)
            {
                WriteToLog($"ERROR in ConnectToSW: {ex.Message}");
                WriteToLog($"StackTrace: {ex.StackTrace}");
                Debug.WriteLine($"FATAL ERROR in ConnectToSW: {ex.Message}");
                Debug.WriteLine($"StackTrace: {ex.StackTrace}");
                // Consider a user message if ConnectToSW fails catastrophically
                // swApp.SendMsgToUser2($"Critical Add-in Initialization Failed: {ex.Message}", (int)swMessageBoxIcon_e.swMbStop, (int)swMessageBoxBtn_e.swMbOk);
                return false;
            }
        }

        /// <summary>
        /// Called when SolidWorks disconnects from this add-in
        /// </summary>
        public bool DisconnectFromSW()
        {
            Debug.WriteLine("DisconnectFromSW called");
            
            // Remove the CommandManager commands
            try
            {
                if (cmdGroup != null && cmdMgr != null)
                {
                    cmdMgr.RemoveCommandGroup(CMD_GROUP_ID);
                    cmdGroup = null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error removing command group: {ex.Message}");
            }

            // Release references
            cmdMgr = null;
            swApp = null;
            processor = null;
            bomProcessor = null;
            propertyStandardManager = null;

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();

            return true;
        }
        #endregion

        #region Command Manager Setup
        /// <summary>
        /// Add the Command Manager commands and toolbar buttons
        /// </summary>
        public void AddCommandMgr()
        {
            try
            {
                WriteToLog("=== AddCommandMgr Started ===");

                // Get the command manager from SolidWorks
                cmdMgr = swApp.GetCommandManager(addinID);
                WriteToLog("Retrieved CommandManager");

                // Make sure any existing command group is removed
                RemoveCommandMgr();
                WriteToLog("Removed existing command group");

                // Create a new command group
                int cmdGroupErr = 0;
                bool ignorePrevious = true; // Always recreate toolbar to ensure proper icons

                // First, create the command group
                cmdGroup = cmdMgr.CreateCommandGroup2(
                    CMD_GROUP_ID,
                    "Roeslein Connex",
                    "Roeslein Connex - Sheet Metal Export Tools",
                    "",
                    -1,
                    ignorePrevious,
                    ref cmdGroupErr);

                WriteToLog($"CreateCommandGroup2 error code: {cmdGroupErr}");

                if (cmdGroup != null)
                {
                    WriteToLog("Command group created successfully");

                    // Path to the INSTALLED individual source icons (e.g., C:\Program Files\...\Resources)
                    string sourceIconPath = Path.Combine(addinPath, "Resources");
                    WriteToLog($"Source Icon path directory: {sourceIconPath}");
                    WriteToLog($"Source Icon directory exists: {Directory.Exists(sourceIconPath)}");

                    // Define a user-writable path for the GENERATED icon strips
                    string userGeneratedResourcesPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RoesleinAddIn", "GeneratedIconStrips");
                    try
                    {
                        // Ensure the directory for generated strips exists
                        if (!Directory.Exists(userGeneratedResourcesPath))
                        {
                            Directory.CreateDirectory(userGeneratedResourcesPath);
                            WriteToLog($"Created user-writable directory for generated icon strips: {userGeneratedResourcesPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        WriteToLog($"Error creating user-writable directory {userGeneratedResourcesPath}: {ex.Message}");
                        // Handle error appropriately, maybe disable custom icons or show a message
                    }
                    

                    // Check if individual source icon files exist in the installation directory
                    CheckIconsExist(sourceIconPath);

                    // Create magenta-background image strips for SolidWorks
                    try
                    {
                        WriteToLog("Creating magenta-background image strips...");
                        MagentaImageStripCreator.CreateAllStrips(sourceIconPath, userGeneratedResourcesPath);
                        WriteToLog("Image strips created successfully in user-writable directory.");
                    }
                    catch (Exception ex)
                    {
                        WriteToLog($"Error creating image strips: {ex.Message}");
                        WriteToLog($"StackTrace: {ex.StackTrace}");
                    }

                    // Define strip icon paths FROM THE USER-WRITABLE LOCATION
                    string[] iconList = new string[] {
                        Path.Combine(userGeneratedResourcesPath, "ConnexIcons_20x20.bmp"),
                        Path.Combine(userGeneratedResourcesPath, "ConnexIcons_32x32.bmp"),
                        Path.Combine(userGeneratedResourcesPath, "ConnexIcons_40x40.bmp")
                    };

                    // Verify image strips were created
                    foreach (string stripPathInUserDir in iconList)
                    {
                        WriteToLog($"Image strip exists in user dir: {Path.GetFileName(stripPathInUserDir)} = {File.Exists(stripPathInUserDir)}");
                    }

                    cmdGroup.IconList = iconList;
                    cmdGroup.MainIconList = iconList;

                    const int cmdItemType = (int)(swCommandItemType_e.swMenuItem | swCommandItemType_e.swToolbarItem);

                    // Create commands in the correct order
                    // 1. Assembly Export DXF (position 1)
                    cmdGroup.AddCommandItem2(
                        "Export Assembly Sheet Metal",
                        -1,
                        "Produce Sheet Metal Flat Patterns from an Assembly",
                        "Export sheet metal parts from assembly to DXF",
                        0,
                        "Export_DXF",
                        "Enable_Export_DXF",
                        CMD_EXPORT_ID,
                        cmdItemType);

                    // 2. Single Part Export (position 2)
                    cmdGroup.AddCommandItem2(
                        "Export Single Part",
                        -1,
                        "Produce Sheet Metal Flat Pattern from a single part file",
                        "Export single sheet metal part to DXF",
                        1,
                        "Process_Part",
                        "Enable_Process_Part",
                        CMD_PROCESS_ID,
                        cmdItemType);

                    // 3. Export BOM (position 3)
                    cmdGroup.AddCommandItem2(
                        "Export BOM",
                        -1,
                        "Export Bill of Materials from Assembly",
                        "Export BOM to Excel",
                        2,
                        "Export_BOM",
                        "Enable_BOM",
                        CMD_BOM_ID,
                        cmdItemType);

                    // 4. ID Tag (position 4)
                    cmdGroup.AddCommandItem2(
                        "ID Tag",
                        -1,
                        "Generate ID Tag for the active drawing",
                        "Generate ID Tag for the active drawing",
                        3,
                        "ID_Tag",
                        "Enable_ID_Tag",
                        CMD_ID_TAG_ID,
                        cmdItemType);

                    // 5. Apply Property Standards (position 5)
                    cmdGroup.AddCommandItem2(
                        "Apply Property Standards",
                        -1,
                        "Apply defined file property standards to the active document",
                        "Apply Property Standards",
                        4,
                        "ApplyPropertyStandards",
                        "EnableApplyPropertyStandards",
                        CMD_APPLY_PROPS_ID,
                        cmdItemType);

                    // 6. Create eDrawings (position 6)
                    cmdGroup.AddCommandItem2(
                        "Create eDrawings",
                        -1,
                        "Create eDrawings from the active document",
                        "Create eDrawings from the active document",
                        5,
                        "Create_eDrawings",
                        "Enable_Create_eDrawings",
                        CMD_CREATE_EDRAWING_ID,
                        cmdItemType);

                    // 7. Settings (position 7 - last)
                    cmdGroup.AddCommandItem2(
                        "Settings",
                        -1,
                        "Connex Add-in Settings",
                        "Configure Connex add-in settings",
                        6,
                        "Show_Settings",
                        "Enable_Settings",
                        CMD_SETTINGS_ID,
                        cmdItemType);

                    cmdGroup.HasToolbar = true;
                    cmdGroup.HasMenu = true;
                    cmdGroup.Activate();

                    WriteToLog("=== AddCommandMgr Completed Successfully ===");
                }
                else
                {
                    WriteToLog($"Failed to create command group, error: {cmdGroupErr}");
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"ERROR in AddCommandMgr: {ex.Message}");
                WriteToLog($"StackTrace: {ex.StackTrace}");
                MessageBox.Show($"Error creating toolbar: {ex.Message}", "Roeslein Add-in", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Remove the Command Manager commands
        /// </summary>
        private void RemoveCommandMgr()
        {
            Debug.WriteLine("RemoveCommandMgr called");

            try
            {
                if (cmdMgr != null)
                {
                    cmdMgr.RemoveCommandGroup(CMD_GROUP_ID);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in RemoveCommandMgr: {ex.Message}");
            }
        }

        /// <summary>
        /// Checks if all individual icon files exist and logs their paths
        /// </summary>
        private void CheckIconsExist(string iconPath)
        {
            try
            {
                string[] iconFiles = {
                    // 1. Assembly Export icons
                    "Produce Assy 20x20.bmp",
                    "Produce Assy 32x32.bmp",
                    "Produce Assy 40x40.bmp",
                    
                    // 2. Single Part Export icons
                    "Produce Single Part 20x20.bmp",
                    "Produce Single Part 32x32.bmp",
                    "Produce Single Part 40x40.bmp",
                    
                    // 3. Export BOM icons
                    "Export BOM 20x20.bmp",
                    "Export BOM 32x32.bmp",
                    "Export BOM 40x40.bmp",

                    // 4. ID Tag icons
                    "ID_Tag 20X20.bmp",
                    "ID_Tag 32X32.bmp",
                    "ID_Tag 40X40.bmp",

                    // 5. Property Standards icons
                    "FileProp 20x20.bmp",
                    "FileProp 32x32.bmp",
                    "FileProp 40x40.bmp",

                    // 6. Create eDrawings icons
                    "Create_eDrawing 20x20.bmp",
                    "Create_eDrawing 32x32.bmp",
                    "Create_eDrawing 40x40.bmp",
                    
                    // 7. Settings icons (last)
                    "Settings 20x20.bmp",
                    "Settings 32x32.bmp",
                    "Settings 40x40.bmp"
                };

                WriteToLog("Checking icon files:");
                foreach (string file in iconFiles)
                {
                    string fullPath = Path.Combine(iconPath, file);
                    bool exists = File.Exists(fullPath);
                    WriteToLog($"  {file}: {(exists ? "EXISTS" : "MISSING")}");
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Error checking icon files: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates image strips from individual BMP files
        /// </summary>
        private void CreateImageStrips(string iconPath)
        {
            try
            {
                // Small icons strip (20x20)
                string[] smallIconFiles = {
                    Path.Combine(iconPath, "Produce Assy 20x20.bmp"),
                    Path.Combine(iconPath, "Produce Single Part 20x20.bmp"),
                    Path.Combine(iconPath, "Export BOM 20x20.bmp"),
                    Path.Combine(iconPath, "ID_Tag 20X20.bmp"),
                    Path.Combine(iconPath, "FileProp 20x20.bmp"),
                    Path.Combine(iconPath, "Create_eDrawing 20x20.bmp"),
                    Path.Combine(iconPath, "Settings 20x20.bmp")
                };

                // Medium icons strip (32x32)
                string[] mediumIconFiles = {
                    Path.Combine(iconPath, "Produce Assy 32x32.bmp"),
                    Path.Combine(iconPath, "Produce Single Part 32x32.bmp"),
                    Path.Combine(iconPath, "Export BOM 32x32.bmp"),
                    Path.Combine(iconPath, "ID_Tag 32X32.bmp"),
                    Path.Combine(iconPath, "FileProp 32x32.bmp"),
                    Path.Combine(iconPath, "Create_eDrawing 32x32.bmp"),
                    Path.Combine(iconPath, "Settings 32x32.bmp")
                };

                // Large icons strip (40x40)
                string[] largeIconFiles = {
                    Path.Combine(iconPath, "Produce Assy 40x40.bmp"),
                    Path.Combine(iconPath, "Produce Single Part 40x40.bmp"),
                    Path.Combine(iconPath, "Export BOM 40x40.bmp"),
                    Path.Combine(iconPath, "ID_Tag 40X40.bmp"),
                    Path.Combine(iconPath, "FileProp 40x40.bmp"),
                    Path.Combine(iconPath, "Create_eDrawing 40x40.bmp"),
                    Path.Combine(iconPath, "Settings 40x40.bmp")
                };

                // Create the image strips
                CreateStrip(smallIconFiles, Path.Combine(iconPath, "ConnexIcons_20x20.bmp"), 20);
                CreateStrip(mediumIconFiles, Path.Combine(iconPath, "ConnexIcons_32x32.bmp"), 32);
                CreateStrip(largeIconFiles, Path.Combine(iconPath, "ConnexIcons_40x40.bmp"), 40);

                WriteToLog("Image strips created successfully");
            }
            catch (Exception ex)
            {
                WriteToLog($"Error creating image strips: {ex.Message}");
                WriteToLog($"StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Creates a single image strip from an array of images
        /// </summary>
        private void CreateStrip(string[] sourceFiles, string outputFile, int size)
        {
            try
            {
                // Check if all source files exist
                foreach (string file in sourceFiles)
                {
                    if (!File.Exists(file))
                    {
                        WriteToLog($"Source file not found: {file}");
                        return;
                    }
                }

                // Create bitmap with enough width for all icons
                using (Bitmap stripBitmap = new Bitmap(size * sourceFiles.Length, size))
                {
                    using (Graphics g = Graphics.FromImage(stripBitmap))
                    {
                        g.Clear(Color.White); // Use white as the background to maintain transparency

                        // Add each icon to the strip
                        for (int i = 0; i < sourceFiles.Length; i++)
                        {
                            using (Bitmap iconBmp = new Bitmap(sourceFiles[i]))
                            {
                                g.DrawImage(iconBmp, i * size, 0, size, size);
                            }
                        }
                    }

                    // Save the strip
                    stripBitmap.Save(outputFile, System.Drawing.Imaging.ImageFormat.Bmp);
                    WriteToLog($"Created image strip: {outputFile}");
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Error creating image strip {outputFile}: {ex.Message}");
            }
        }

        /// <summary>
        /// Compares two arrays of command IDs to determine if they match
        /// </summary>
        /// <param name="storedIDs">IDs stored in registry</param>
        /// <param name="addinIDs">IDs from current add-in</param>
        /// <returns>True if arrays match, false otherwise</returns>
        private bool CompareIDs(int[] storedIDs, int[] addinIDs)
        {
            try
            {
                if (storedIDs == null || addinIDs == null)
                    return false;

                List<int> storedList = new List<int>(storedIDs);
                List<int> addinList = new List<int>(addinIDs);

                addinList.Sort();
                storedList.Sort();

                if (addinList.Count != storedList.Count)
                {
                    return false;
                }

                for (int i = 0; i < addinList.Count; i++)
                {
                    if (addinList[i] != storedList[i])
                    {
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in CompareIDs: {ex.Message}");
                return false;
            }
        }
        #endregion

        #region Command Callbacks
        // These methods MUST match the names given in AddCommandItem2 above

        /// <summary>
        /// Export Sheet Metal DXF command
        /// </summary>
        public void Export_DXF()
        {
            WriteToLog("Export_DXF called");
            Debug.WriteLine("[DEBUG] Export_DXF called"); // DEBUG

            try
            {
                Debug.WriteLine("[DEBUG] Export_DXF: Checking processor instance..."); // DEBUG
                // Processor should have been created in ConnectToSW
                if (processor == null)
                {
                    Debug.WriteLine("[DEBUG] Export_DXF: ERROR - Processor instance is null!"); // DEBUG
                    WriteToLog("Export_DXF: ERROR - Processor instance is null!");
                    MessageBox.Show("SheetMetalProcessor failed to initialize. Please check logs.",
                                    "Roeslein Add-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return; // Cannot proceed
                }

                Debug.WriteLine("[DEBUG] Export_DXF: Calling ProcessAssemblySheetMetal..."); // DEBUG
                processor.ProcessAssemblySheetMetal();
                Debug.WriteLine("[DEBUG] Export_DXF: ProcessAssemblySheetMetal completed."); // DEBUG
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] Export_DXF: Error: {ex.Message}\n{ex.StackTrace}"); // DEBUG
                WriteToLog($"Export_DXF: Error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error processing assembly: {ex.Message}", "Roeslein Add-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Export BOM command
        /// </summary>
        public void Export_BOM()
        {
            WriteToLog("Export_BOM called");
            Debug.WriteLine("[DEBUG] Export_BOM called");

            try
            {
                // Check if bomProcessor is initialized
                if (bomProcessor == null)
                {
                    Debug.WriteLine("[DEBUG] Export_BOM: ERROR - BomProcessor instance is null!");
                    WriteToLog("Export_BOM: ERROR - BomProcessor instance is null!");
                    MessageBox.Show("BomProcessor failed to initialize. Please check logs.",
                                   "Roeslein Add-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Show BOM options dialog
                if (swApp != null)
                {
                    BomExportForm bomDialog = new BomExportForm(swApp);
                    DialogResult dialogResult = bomDialog.ShowDialog();

                    if (dialogResult == DialogResult.OK)
                    {
                        System.Diagnostics.Debug.WriteLine("BOM Export process initiated via BomExportForm.");
                    }
                    else if (dialogResult == DialogResult.Cancel)
                    {
                        System.Diagnostics.Debug.WriteLine("BOM Export was cancelled by the user in the BomExportForm.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"BOM Export form closed with result: {dialogResult}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("ISldWorks _swApp instance is null. Cannot show BOM Export form.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DEBUG] Export_BOM: Error: {ex.Message}\n{ex.StackTrace}");
                WriteToLog($"Export_BOM: Error: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Error exporting BOM: {ex.Message}", 
                               "Roeslein Add-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Callback for the Settings command.
        /// </summary>
        public void Show_Settings()
        {
            try
            {
                WriteToLog("Settings command invoked.");
                // Always ensure vault is connected before opening settings
                EnsurePdmVaultConnected("PCSVAULT");

                if (settingsForm == null || settingsForm.IsDisposed)
                {
                    Settings.SetGlobalVaultForSettings(this.pdmVault);
                    settingsForm = new SettingsFormV2(this.swApp, this.pdmVault);
                }
                settingsForm.ShowDialog();
            }
            catch (Exception ex)
            {
                WriteToLog($"Error displaying settings form: {ex.Message}\nStackTrace: {ex.StackTrace}");
                if (swApp != null)
                {
                    swApp.SendMsgToUser2($"An error occurred while opening settings: {ex.Message}", 
                        (int)swMessageBoxIcon_e.swMbStop, (int)swMessageBoxBtn_e.swMbOk);
                }
                else
                {
                    System.Windows.Forms.MessageBox.Show($"An error occurred while opening settings: {ex.Message}", "Error", 
                        System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
                }
            }
        }

        /// <summary>
        /// Enable callback for Export Sheet Metal DXF command
        /// </summary>
        public int Enable_Export_DXF()
        {
            // Enable if there's an active document AND it's an assembly
            ModelDoc2 swModel = swApp?.ActiveDoc as ModelDoc2;
            if (swModel == null) return 0;

            if (swModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                return 0;
            
            return 1;
        }

        /// <summary>
        /// Enable callback for Show Settings command
        /// </summary>
        public int Enable_Settings()
        {
            // Always enable
            return 1;
        }

        /// <summary>
        /// Process Single Part command
        /// </summary>
        public void Process_Part()
        {
            WriteToLog("Process_Part called");
            Debug.WriteLine("[DEBUG] Process_Part called"); // DEBUG

            try
            {
                Debug.WriteLine("[DEBUG] Process_Part: Checking processor instance..."); // DEBUG
                // Processor should have been created in ConnectToSW
                if (processor == null)
                {
                    Debug.WriteLine("[DEBUG] Process_Part: ERROR - Processor instance is null!"); // DEBUG
                    WriteToLog("Process_Part: ERROR - Processor instance is null!");
                    MessageBox.Show("SheetMetalProcessor failed to initialize. Please check logs.",
                                    "Roeslein Add-in Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return; // Cannot proceed
                }

                Debug.WriteLine("[DEBUG] Process_Part: Calling ProcessSinglePartSheetMetal..."); // DEBUG
                processor.ProcessSinglePartSheetMetal();
                Debug.WriteLine("[DEBUG] Process_Part: ProcessSinglePartSheetMetal completed."); // DEBUG
            }
            catch (Exception ex)
            {
                Debug.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                Debug.WriteLine($"[DEBUG] ERROR in Process_Part: {ex.GetType().Name} - {ex.Message}");
                Debug.WriteLine($"[DEBUG] StackTrace: {ex.StackTrace}");
                Debug.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
                WriteToLog($"Error in Process_Part: {ex.Message}"); // Keep existing simple log for now
                MessageBox.Show($"Error processing part: {ex.Message}", "Roeslein Add-in",
                               MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            Debug.WriteLine("[DEBUG] Process_Part END"); // DEBUG
        }

        /// <summary>
        /// Enable the Process Part button if a sheet metal part is active
        /// </summary>
        public int Enable_Process_Part()
        {
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                if (swModel == null) return 0;
                
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocPART)
                    return 0;
                
                return 1;
            }
            catch (Exception ex)
            {
                WriteToLog($"Error in Enable_Process_Part: {ex.Message}\n{ex.StackTrace}");
                return 0;
            }
        }

        /// <summary>
        /// Enable the BOM button if an assembly is active
        /// </summary>
        public int Enable_BOM()
        {
            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                if (swModel == null) return 0;
                
                if (swModel.GetType() != (int)swDocumentTypes_e.swDocASSEMBLY)
                    return 0;
                
                return 1;
            }
            catch (Exception ex)
            {
                WriteToLog($"Error in Enable_BOM: {ex.Message}\n{ex.StackTrace}");
                return 0;
            }
        }

        /// <summary>
        /// Callback for the Apply Property Standards command.
        /// </summary>
        public void ApplyPropertyStandards()
        {
            try
            {
                WriteToLog("ApplyPropertyStandards command invoked.");
                ModelDoc2 activeDoc = swApp.ActiveDoc as ModelDoc2;
                if (activeDoc == null)
                {
                    swApp.SendMsgToUser2("No active document to apply standards to.", (int)swMessageBoxIcon_e.swMbWarning, (int)swMessageBoxBtn_e.swMbOk);
                    WriteToLog("ApplyPropertyStandards: No active document.");
                    return;
                }

                // Ensure PDM connection FIRST
                WriteToLog("Ensuring PDM connection is established for the active document.");
                TryConnectToPdmVaultForActiveDoc();

                // Only set global vault if logged in
                if (this.pdmVault != null && this.pdmVault.IsLoggedIn)
                {
                    Settings.SetGlobalVaultForSettings(this.pdmVault);
                }
                else
                {
                    WriteToLog("PDM vault is not logged in. Cannot proceed with property standards.");
                    swApp.SendMsgToUser2("Could not connect to PDM vault. Property standards cannot be applied.", (int)swMessageBoxIcon_e.swMbStop, (int)swMessageBoxBtn_e.swMbOk);
                    return;
                }

                // Now load settings (vault is set and logged in)
                var loadedSettings = Settings.LoadSettings();
                Logger.SetMainLogFilePath(loadedSettings.LogFilePath);
                Logger.Instance.SetDebugLogPath(loadedSettings.DebugLogFilePath);
                string currentStandardsVersion = !string.IsNullOrEmpty(loadedSettings.SettingsVersion)
                    ? loadedSettings.SettingsVersion
                    : "1.0.0";

                if (propertyStandardManager == null)
                {
                    propertyStandardManager = new PropertyStandardManager(this.pdmVault, swApp, null);
                }
                else
                {
                    propertyStandardManager = new PropertyStandardManager(this.pdmVault, swApp, null);
                }

                if (activeDoc.GetType() == (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocASSEMBLY)
                {
                    propertyStandardManager.ProcessAssemblyForStandards(activeDoc, currentStandardsVersion, false);
                }
                else if (activeDoc.GetType() == (int)SolidWorks.Interop.swconst.swDocumentTypes_e.swDocDRAWING)
                {
                    propertyStandardManager.ProcessDrawingForStandards(activeDoc, currentStandardsVersion, false);
                }
                else
                {
                    propertyStandardManager.ApplyStandardsToActiveDocument(activeDoc, currentStandardsVersion, false, loadedSettings);
                }
            }
            catch (Exception ex)
            {
                WriteToLog($"Error in ApplyPropertyStandards: {ex.Message}\nStackTrace: {ex.StackTrace}");
                swApp.SendMsgToUser2($"An unexpected error occurred: {ex.Message}", (int)swMessageBoxIcon_e.swMbStop, (int)swMessageBoxBtn_e.swMbOk);
            }
        }

        /// <summary>
        /// Enable method for the Apply Property Standards command.
        /// </summary>
        public int EnableApplyPropertyStandards()
        {
            ModelDoc2 activeDoc = swApp.ActiveDoc as ModelDoc2;
            if (activeDoc != null)
            {
                swDocumentTypes_e docType = (swDocumentTypes_e)activeDoc.GetType();
                return (docType == swDocumentTypes_e.swDocPART || 
                        docType == swDocumentTypes_e.swDocASSEMBLY || 
                        docType == swDocumentTypes_e.swDocDRAWING) ? 1 : 0;
            }
            return 0;
        }

        /// <summary>
        /// Create eDrawings command callback
        /// </summary>
        public void Create_eDrawings()
        {
            WriteToLog("Create_eDrawings called");
            Debug.WriteLine("[DEBUG] Create_eDrawings called");

            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                
                if (swModel == null)
                {
                    MessageBox.Show("No active document.", "Roeslein Add-in", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Ensure PDM vault is connected before creating eDrawings
                TryConnectToPdmVaultForActiveDoc();
                
                // Create eDrawingsCreator instance and process the active document
                var creator = new eDrawingsCreator(swApp, pdmVault);
                creator.CreateeDrawingsFromActiveDocument();
                
                WriteToLog("Create_eDrawings completed successfully");
            }
            catch (Exception ex)
            {
                WriteToLog($"Error in Create_eDrawings: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"Error creating eDrawings: {ex.Message}", "Roeslein Add-in Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Enable callback for Create eDrawings command
        /// </summary>
        public int Enable_Create_eDrawings()
        {
            try
            {
                ModelDoc2 swModel = swApp?.ActiveDoc as ModelDoc2;
                if (swModel == null) return 0;

                // Enable for parts, assemblies, and drawings
                int docType = swModel.GetType();
                if (docType == (int)swDocumentTypes_e.swDocPART ||
                    docType == (int)swDocumentTypes_e.swDocASSEMBLY ||
                    docType == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    return 1;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                WriteToLog($"Error in Enable_Create_eDrawings: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// ID Tag command callback
        /// </summary>
        public void ID_Tag()
        {
            WriteToLog("ID_Tag called");
            Debug.WriteLine("[DEBUG] ID_Tag called");

            try
            {
                ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
                
                if (swModel == null)
                {
                    MessageBox.Show("No active document.", "Roeslein Add-in", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Check document type - must be a drawing
                int docType = swModel.GetType();
                if (docType != (int)swDocumentTypes_e.swDocDRAWING)
                {
                    MessageBox.Show("ID Tag can only be used on drawing documents.", "Roeslein Add-in", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                // Show the shipping label manager
                ShippingLabelManagerForm.ShowShippingLabelManager(swApp);
                
                WriteToLog("ID_Tag completed successfully");
            }
            catch (Exception ex)
            {
                WriteToLog($"Error in ID_Tag: {ex.Message}\nStackTrace: {ex.StackTrace}");
                MessageBox.Show($"Error with ID Tag: {ex.Message}", "Roeslein Add-in Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Enable callback for ID Tag command - only enabled for drawing documents
        /// </summary>
        public int Enable_ID_Tag()
        {
            try
            {
                ModelDoc2 swModel = swApp?.ActiveDoc as ModelDoc2;
                if (swModel == null) return 0;

                // Only enable for drawing documents
                int docType = swModel.GetType();
                if (docType == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    return 1;
                }
                
                return 0;
            }
            catch (Exception ex)
            {
                WriteToLog($"Error in Enable_ID_Tag: {ex.Message}");
                return 0;
            }
        }
        #endregion

        #region Logging
        private void WriteToLog(string message)
        {
            try
            {
                // Output to Visual Studio's debug console unconditionally for development
                Debug.WriteLine($"[RoesleinAddIn DEBUG] {message}");
                
                // Use the Logger class for all persistent logging
                if (Logger.Instance != null)
                {
                    // Logger.DebugLog will internally check if debug logging is enabled.
                    // If paths are not set, it might log to a default location or not at all,
                    // depending on Logger's internal implementation.
                    Logger.DebugLog(message); 
                }
            }
            catch
            {
                // Silent fail - we don't want logging errors to cause crashes
            }
        }
        #endregion

        #region PDM Integration
        private void TryConnectToPdmVaultForActiveDoc()
        {
            if (swApp == null)
            {
                // Use Logger.Warning for conditions that prevent operation but aren't necessarily critical errors.
                Logger.Warning("PDM: SolidWorks application not available. Cannot connect to PDM vault.");
                return;
            }

            ModelDoc2 swModel = swApp.ActiveDoc as ModelDoc2;
            if (swModel == null)
            {
                Logger.Info("PDM: No active SolidWorks document. PDM connection not attempted for a specific file at this time.");
                // It's not an error if no doc is open, PDM might be connected later or not needed.
                return;
            }

            string filePath = swModel.GetPathName();
            if (string.IsNullOrEmpty(filePath))
            {
                Logger.Info("PDM: Active SolidWorks document is not saved. PDM connection not attempted for this file.");
                return;
            }

            Logger.Info($"PDM: Attempting to connect to vault for file: '{filePath}'");
            try
            {
                if (this.pdmVault == null || !this.pdmVault.IsLoggedIn)
                {
                    this.pdmVault = new EPDM.Interop.epdm.EdmVault5();
                    string vaultNameForFile = this.pdmVault.GetVaultNameFromPath(filePath);
                    if (!string.IsNullOrEmpty(vaultNameForFile))
                    {
                        WriteToLog($"PDM: File '{filePath}' is in vault view for vault: '{vaultNameForFile}'. Attempting to log in.");
                        this.pdmVault.LoginAuto(vaultNameForFile, 0);
                        if (this.pdmVault.IsLoggedIn)
                        {
                            WriteToLog($"PDM: Successfully connected to vault: {this.pdmVault.Name} (Root Path: {this.pdmVault.RootFolderPath})");
                            if (this.pdmVault != null && this.pdmVault.IsLoggedIn)
                            {
                                Settings.SetGlobalVaultForSettings(this.pdmVault);
                            }
                        }
                        else
                        {
                            WriteToLog($"PDM: Failed to auto-login to vault '{vaultNameForFile}'. Ensure PDM client is logged in.");
                            this.pdmVault = null;
                        }
                    }
                    else
                    {
                        WriteToLog($"PDM: File '{filePath}' does not appear to be in a PDM vault view.");
                        this.pdmVault = null;
                    }
                }
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                // Use Logger.Error for actual errors.
                Logger.Error($"PDM: COM Exception during PDM connection for '{filePath}'. HRESULT: {comEx.ErrorCode:X}, Message: {comEx.Message}. Ensure PDM interops are correctly registered and version compatible.");
                this.pdmVault = null;
            }
            catch (Exception ex)
            {
                Logger.Error($"PDM: General exception during PDM connection for '{filePath}': {ex.Message}");
                this.pdmVault = null;
            }
        }

        // Robust version: Ensure a persistent PDM vault connection and only set global vault if logged in
        private void EnsurePdmVaultConnected(string vaultName)
        {
            if (this.pdmVault == null)
                this.pdmVault = new EPDM.Interop.epdm.EdmVault5();

            if (!this.pdmVault.IsLoggedIn)
            {
                try
                {
                    this.pdmVault.LoginAuto(vaultName, 0);
                }
                catch (Exception ex)
                {
                    WriteToLog($"PDM LoginAuto failed: {ex.Message}");
                }
            }

            if (this.pdmVault.IsLoggedIn)
            {
                Settings.SetGlobalVaultForSettings(this.pdmVault);
            }
            else
            {
                WriteToLog("PDM vault is not logged in after LoginAuto. Not setting global vault for settings.");
                MessageBox.Show($"Could not connect to PDM vault '{vaultName}'. Please ensure PDM is running and you are logged in.", "PDM Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        #endregion
    }
}