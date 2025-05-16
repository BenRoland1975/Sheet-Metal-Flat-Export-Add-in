using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;
using EPDM.Interop.epdm;
using SolidWorks.Interop.sldworks;
using System.Globalization; // Added for parsing

namespace RoesleinAddIn
{
    // THIS IS THE INTENDED SINGLE DEFINITION
    public class PropertyStandardSetting
    {
        public bool UseDefaultValue { get; set; }
        public string PropertyName { get; set; }
        public string DefaultValueExpr { get; set; }
        public bool IsCustomProperty { get; set; } // Should map to IsCustomPropertyTarget in PropertyStandardManager
        public bool IsConfigSpecific { get; set; } // Should map to IsConfigurationSpecificTarget in PropertyStandardManager
        public bool UseOnPartFiles { get; set; }
        public bool UseOnAssemblyFiles { get; set; }
        public bool UseOnDrawingFiles { get; set; }

        // Default constructor
        public PropertyStandardSetting()
        {
            // Sensible defaults for a new row in the settings grid
            UseDefaultValue = false;
            PropertyName = "New Property";
            DefaultValueExpr = "";
            IsCustomProperty = true;
            IsConfigSpecific = true;
            UseOnPartFiles = true;
            UseOnAssemblyFiles = false;
            UseOnDrawingFiles = false;
        }
    }

    /// <summary>
    /// Settings class for the Roeslein SolidWorks Add-In
    /// </summary>
    [Serializable]
    public class Settings
    {
        // File path settings
        public string LastExportFolder { get; set; }
        
        // Single part export folder - separate from LastExportFolder
        public string LastSinglePartExportFolder { get; set; }
        
        // DXF export settings
        public bool ShowBendLines { get; set; }
        public string TextLayerName { get; set; }
        public string BendLineLayerName { get; set; }
        public string TextLocation { get; set; }
        public bool UseCustomScale { get; set; }
        public double DrawingScale { get; set; }
        public bool AddDimensions { get; set; }
        public bool ImportModelDimensions { get; set; }
        public bool ExportHiddenGeometry { get; set; }
        
        // Title block settings
        public bool AddTitleBlockInfo { get; set; }
        public string TitleBlockText { get; set; }
        
        // Logging settings
        public bool LoggingEnabled { get; set; }
        public bool DebugLoggingEnabled { get; set; }
        public string LogFilePath { get; set; }
        public string DebugLogFilePath { get; set; }
        public string DrawingTemplatePath { get; set; }
        public bool CheckForLaser { get; set; }
        
        // Property Standard Manager specific settings
        public int MaxRecursionDepth { get; set; }
        public bool ForceApplyStandards { get; set; }
        public string DefaultMaterialName { get; set; }
        public string DefaultMaterialDatabase { get; set; }
        public double ThicknessToleranceInches { get; set; }
        
        // Mark instance dictionary with XmlIgnore to prevent serialization errors
        [System.Xml.Serialization.XmlIgnore]
        public Dictionary<double, double> ThicknessMapping { get; set; }
        
        // Mark static dictionary with XmlIgnore to prevent serialization errors
        [System.Xml.Serialization.XmlIgnore]
        public static Dictionary<double, double> ThicknessMappings { get; set; } = new Dictionary<double, double>();
        
        [System.Xml.Serialization.XmlIgnore] // Exclude from main settings XML
        public List<PropertyStandardSetting> PropertyStandards { get; set; }

        [System.Xml.Serialization.XmlIgnore] // Exclude from main settings XML, loaded from PDM shared settings
        public string SettingsVersion { get; set; }
        
        private const string RegistryKeyPath = @"Software\Roeslein\SolidWorksAddIn";
        private static readonly string DefaultSettingsPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
            @"Roeslein\Settings\RoesleinAddInSettings.xml");
        
        private static readonly string DefaultLogPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
            @"Roeslein\Logs\RoesleinAddIn.log");

        // Deprecated - PropertyStandards will be loaded from PdmPropertyStandardsAndVersionPath
        // private static readonly string DefaultPropertyStandardsPath = Path.Combine(
        // System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
        //    @"Roeslein\Settings\PropertyStandardsSettings.xml");

        private static readonly string PdmPropertyStandardsAndVersionPath = @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\PropertyStandardsAndVersion.xml"; // ADJUST THIS PATH AS NEEDED
        
        // To be set by the add-in connection logic
        [System.Xml.Serialization.XmlIgnore]
        public static ISldWorks SwAppForPDM { get; set; } // Static reference to SwApp
        [System.Xml.Serialization.XmlIgnore]
        public static IEdmVault5 PdmVaultForSettings { get; set; } // Static reference to the vault

        public static void SetGlobalVaultForSettings(IEdmVault5 vault)
        {
            PdmVaultForSettings = vault;
            if (vault == null)
            {
                Logger.Info("SetGlobalVaultForSettings: Global PDM vault for settings has been set to NULL.");
            }
            else
            {
                Logger.Info($"SetGlobalVaultForSettings: Global PDM vault for settings has been updated. LoggedIn: {vault.IsLoggedIn}, Name: {vault.Name}");
            }
        }

        public Settings()
        {
            // Default settings
            LastExportFolder = @"\\roeslein.com\locations$\RoesleinFabrication\Connex-Metamation Hot Folder\DXFs From PDM";
            
            // Initialize LastSinglePartExportFolder with same default
            LastSinglePartExportFolder = LastExportFolder;
            
            TextLayerName = "Notes";
            BendLineLayerName = "Bend_Lines";
            TextLocation = "Centered";
            ShowBendLines = true;
            UseCustomScale = false;
            DrawingScale = 1.0;
            AddDimensions = false;
            ImportModelDimensions = false;
            ExportHiddenGeometry = false;
            AddTitleBlockInfo = false;
            TitleBlockText = "";
            LoggingEnabled = true;
            DebugLoggingEnabled = false; // Debug logging off by default
            
            // Set default log path
            LogFilePath = DefaultLogPath;
            
            // Set default debug log path
            DebugLogFilePath = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                "RoesleinAddIn", 
                "logs",
                "RoesleinAddIn_Debug.log");
                
            // Setup property mappings
            this.PropertyMappings = new List<PropertyMapping>();
            this.MaterialMappings = new List<MaterialMapping>();
            ThicknessMapping = new Dictionary<double, double>();

            // Initialize new PropertyStandards list
            PropertyStandards = new List<PropertyStandardSetting>();
            SettingsVersion = "25.1"; // Default version if not loaded

            // Initialize Property Standard Manager specific settings with defaults
            MaxRecursionDepth = 10; // Default recursion depth
            ForceApplyStandards = false; // Default for forcing standards application
            DefaultMaterialName = "AISI 1020 Steel"; // Example default material
            DefaultMaterialDatabase = "SolidWorks Materials"; // Example default DB
            ThicknessToleranceInches = 0.005; // Default tolerance
        }

        [System.Xml.Serialization.XmlIgnore]
        public List<PropertyMapping> PropertyMappings { get; set; }
        
        [System.Xml.Serialization.XmlIgnore]
        public List<MaterialMapping> MaterialMappings { get; set; }

        public List<RawMaterial> RawMaterials { get; set; } = new List<RawMaterial>();

        /// <summary>
        /// Saves settings to both XML file and registry
        /// </summary>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveSettings()
        {
            try
            {
                // Ensure directory exists
                string directory = Path.GetDirectoryName(DefaultSettingsPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Save to XML file
                using (FileStream stream = new FileStream(DefaultSettingsPath, FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                    serializer.Serialize(stream, this);
                }

                // Save critical settings to registry as a backup
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RegistryKeyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("LastExportFolder", LastExportFolder);
                        key.SetValue("LastSinglePartExportFolder", LastSinglePartExportFolder);
                        key.SetValue("LoggingEnabled", LoggingEnabled ? 1 : 0);
                        key.SetValue("DebugLoggingEnabled", DebugLoggingEnabled ? 1 : 0);
                        key.SetValue("LogFilePath", LogFilePath);
                        key.SetValue("DebugLogFilePath", DebugLogFilePath);
                        key.SetValue("DrawingTemplatePath", DrawingTemplatePath);
                    }
                }
                
                // Update static ThicknessMappings with values from this instance if any
                if (ThicknessMapping != null && ThicknessMapping.Count > 0)
                {
                    // Clear existing static mappings and copy from instance
                    ThicknessMappings.Clear();
                    foreach (var mapping in ThicknessMapping)
                    {
                        ThicknessMappings[mapping.Key] = mapping.Value;
                    }
                    
                    // Save to file
                    SaveThicknessMappings();
                }
                
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Loads settings from XML file and falls back to registry if needed
        /// </summary>
        /// <returns>A Settings object with loaded or default values</returns>
        public static Settings LoadSettings()
        {
            Settings settings = null;

            try
            {
                // Try to load from XML file first
                if (File.Exists(DefaultSettingsPath))
                {
                    using (FileStream stream = new FileStream(DefaultSettingsPath, FileMode.Open))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(Settings));
                        settings = (Settings)serializer.Deserialize(stream);
                    }
                }
                else
                {
                    // If XML doesn't exist, create a new settings object
                    settings = new Settings(); // This will set default SettingsVersion and empty PropertyStandards

                    // Try to get critical settings from registry
                    using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath))
                    {
                        if (key != null)
                        {
                            object lastFolder = key.GetValue("LastExportFolder");
                            if (lastFolder != null)
                            {
                                settings.LastExportFolder = lastFolder.ToString();
                            }

                            object lastSinglePartExportFolder = key.GetValue("LastSinglePartExportFolder");
                            if (lastSinglePartExportFolder != null)
                            {
                                settings.LastSinglePartExportFolder = lastSinglePartExportFolder.ToString();
                            }

                            object loggingEnabled = key.GetValue("LoggingEnabled");
                            if (loggingEnabled != null)
                            {
                                settings.LoggingEnabled = Convert.ToBoolean(loggingEnabled);
                            }
                            
                            object debugLoggingEnabled = key.GetValue("DebugLoggingEnabled");
                            if (debugLoggingEnabled != null)
                            {
                                settings.DebugLoggingEnabled = Convert.ToBoolean(debugLoggingEnabled);
                            }

                            object logFilePath = key.GetValue("LogFilePath");
                            if (logFilePath != null)
                            {
                                settings.LogFilePath = logFilePath.ToString();
                            }
                            
                            object debugLogFilePathRegistry = key.GetValue("DebugLogFilePath");
                            if (debugLogFilePathRegistry != null)
                            {
                                settings.DebugLogFilePath = debugLogFilePathRegistry.ToString();
                            }
                            
                            object drawingTemplatePath = key.GetValue("DrawingTemplatePath");
                            if (drawingTemplatePath != null)
                            {
                                settings.DrawingTemplatePath = drawingTemplatePath.ToString();
                            }
                        }
                    }
                    // Save the newly created settings (with registry fallbacks) to establish the local XML
                    settings.SaveSettings();
                }
            }
            catch (Exception ex)
            {
                // Log error, create new settings object with defaults
                Logger.Error("Error loading settings from XML/Registry", ex);
                settings = new Settings();
            }
            
            // Ensure settings object exists
            if (settings == null)
            {
                Logger.Warning("Settings object was null after XML/Registry load attempts. Creating new default settings.");
                settings = new Settings();
            }

            // Load PDM shared data (PropertyStandards and SettingsVersion)
            // Pass the static PdmVaultForSettings
            settings.LoadPdmSharedData(PdmVaultForSettings); 

            // Load other non-serialized lists if necessary (e.g., from their own files or defaults)
            // PropertyMappings and MaterialMappings are already handled by their static Load methods
            // if they are called elsewhere (e.g., in SettingsFormV2)
            // settings.PropertyMappings = LoadPropertyMappings(); // Example if they were instance properties
            // settings.MaterialMappings = LoadMaterialMappings(); // Example

            // Load Thickness Mappings (static, but might be initialized here too)
            LoadThicknessMappings(); // This loads into the static ThicknessMappings dictionary

            return settings;
        }

        private void LoadPdmSharedData(IEdmVault5 vault)
        {
            PdmSharedSettings sharedData = null;
            
            if (vault == null || !vault.IsLoggedIn)
            {
                Logger.Warning("LoadPdmSharedData: PDM vault is not available or not logged in. Using local defaults/creating new.");
                sharedData = new PdmSharedSettings(); // Use defaults
                // Potentially try to load from a local cache if PDM is unavailable, or just use defaults.
                // For now, we'll just proceed with defaults which might then be populated by LoadDefaultPropertyStandardsStatic.
            }
            else
            {
                try
                {
                    IEdmFolder5 pdmFolder = null;
                    IEdmFile5 pdmFile = vault.GetFileFromPath(PdmPropertyStandardsAndVersionPath, out pdmFolder);

                    if (pdmFile != null)
                    {
                        // Get the latest version of the file
                        // The 0 for ParentWndHandle means no UI will be shown on errors by GetFileCopy itself
                        pdmFile.GetFileCopy(0, pdmFile.CurrentVersion, PdmPropertyStandardsAndVersionPath); // ParentWndHandle is int, 0 is fine.
                        Logger.Info($"Retrieved latest version ({pdmFile.CurrentVersion}) of PDM shared settings file: {PdmPropertyStandardsAndVersionPath}");
                    }
                    else
                    {
                        Logger.Warning($"PDM shared settings file does not exist in vault at: '{PdmPropertyStandardsAndVersionPath}'. Will attempt to create it on next save if settings are modified.");
                        // File doesn't exist in PDM, so proceed with defaults. It will be created on first save via SettingsForm.
                    }
                }
                catch (System.Runtime.InteropServices.COMException comEx)
                {
                    Logger.Error($"PDM COMException during GetFileFromPath or GetFileCopy for '{PdmPropertyStandardsAndVersionPath}': {comEx.Message} (ErrorCode: {comEx.ErrorCode:X})", comEx);
                    // Fallback to defaults if PDM access fails.
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error during PDM file retrieval for '{PdmPropertyStandardsAndVersionPath}': {ex.Message}", ex);
                    // Fallback to defaults.
                }
            }

            // Proceed with deserialization attempt regardless of PDM success (might be using local copy or defaults)
            if (File.Exists(PdmPropertyStandardsAndVersionPath))
            {
                try
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(PdmSharedSettings));
                    using (FileStream stream = new FileStream(PdmPropertyStandardsAndVersionPath, FileMode.Open, FileAccess.Read)) // Open with Read access
                    {
                        sharedData = (PdmSharedSettings)serializer.Deserialize(stream);
                    }
                    Logger.Info($"Successfully deserialized shared settings from: {PdmPropertyStandardsAndVersionPath}");
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error deserializing shared settings from '{PdmPropertyStandardsAndVersionPath}': {ex.Message}", ex);
                    sharedData = new PdmSharedSettings(); 
                }
            }
            else
            {
                Logger.Warning($"Local shared settings file not found after PDM ops: '{PdmPropertyStandardsAndVersionPath}'. Using default values.");
                sharedData = new PdmSharedSettings(); 
            }

            this.SettingsVersion = sharedData.SettingsVersion ?? "25.1"; 
            this.PropertyStandards = sharedData.PropertyStandards ?? new List<PropertyStandardSetting>();

            if (this.PropertyStandards.Count == 0)
            {
                Logger.Info("No property standards found in PDM shared settings or local file. Loading hardcoded default standards into current instance.");
                LoadDefaultPropertyStandardsStatic(this.PropertyStandards); 
            }
        }
        
        public static bool SavePdmSharedSettings(IEdmVault5 vault, string settingsVersion, List<PropertyStandardSetting> standards)
        {
            if (vault == null || !vault.IsLoggedIn)
            {
                Logger.Error("SavePdmSharedSettings: PDM vault is not available or not logged in. Cannot save shared settings to PDM.");
                // Optionally, save to a local cache or inform user appropriately.
                return false;
            }

            IEdmFile5 pdmFile = null;
            IEdmFolder5 pdmFolder = null;
            bool wasCheckedOutByThisOperation = false;
            long parentWndHandle = GetSolidWorksWindowHandle();
            string currentPdmUserName = null;

            // Get current PDM user name
            try
            {
                IEdmVault7 vault7 = vault as IEdmVault7; // Try to cast to IEdmVault7 or higher
                if (vault7 != null)
                {
                    IEdmUserMgr5 userMgr = vault7.CreateUtility(EdmUtility.EdmUtil_UserMgr) as IEdmUserMgr5;
                    if (userMgr != null)
                    {
                        IEdmUser5 pdmUser = userMgr.GetLoggedInUser();
                        if (pdmUser != null)
                        {
                            currentPdmUserName = pdmUser.Name;
                            Logger.Info($"SavePdmSharedSettings: Current PDM User: {currentPdmUserName}");
                        }
                        else { Logger.Warning("SavePdmSharedSettings: Could not retrieve IEdmUser5 object for current PDM user."); }
                    }
                    else { Logger.Warning("SavePdmSharedSettings: Could not create IEdmUserMgr5 utility from vault7."); }
                }
                else
                {
                    // Removed the direct call to vault.GetLoggedInUser() as it's not on IEdmVault5
                    Logger.Warning("SavePdmSharedSettings: Could not cast pdmVault to IEdmVault7 (or newer). Unable to get PDM user name via CreateUtility.");
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"SavePdmSharedSettings: Error getting current PDM user name: {ex.Message}");
            }


            try
            {
                pdmFile = vault.GetFileFromPath(PdmPropertyStandardsAndVersionPath, out pdmFolder);

                if (pdmFile == null)
                {
                    // File doesn't exist, so it will be added on check-in.
                    // We need to ensure the local directory exists to write the file before adding.
                    string directory = Path.GetDirectoryName(PdmPropertyStandardsAndVersionPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    Logger.Info($"PDM shared settings file does not exist in vault. It will be created locally at '{PdmPropertyStandardsAndVersionPath}' and added.");
                    // No need to checkout if file doesn't exist in PDM yet.
                }
                else
                {
                    // File exists, check it out
                    if (!pdmFile.IsLocked)
                    {
                        // parentWndHandle needs to be int for LockFile
                        pdmFile.LockFile(pdmFolder.ID, (int)parentWndHandle, (int)EdmLockFlag.EdmLock_Simple);
                        wasCheckedOutByThisOperation = true;
                        Logger.Info($"Checked out PDM shared settings file: {PdmPropertyStandardsAndVersionPath}");
                    }
                    // else if (pdmFile.IsLockedByMe) // CS1061: IEdmFile5 has no IsLockedByMe
                    else if (pdmFile.IsLocked && !string.IsNullOrEmpty(currentPdmUserName) && pdmFile.LockedByUser.Name.Equals(currentPdmUserName, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Info($"PDM shared settings file '{PdmPropertyStandardsAndVersionPath}' is already locked by the current user ('{currentPdmUserName}').");
                    }
                    else
                    {
                        string lockedByUserName = pdmFile.IsLocked ? pdmFile.LockedByUser.Name : "Unknown User";
                        Logger.Error($"PDM shared settings file '{PdmPropertyStandardsAndVersionPath}' is locked by another user: {lockedByUserName}. Current user: '{currentPdmUserName ?? "Unknown"}'. Cannot save.");
                        MessageBox.Show($"Settings file '{PdmPropertyStandardsAndVersionPath}' is locked by user '{lockedByUserName}'. Cannot save changes.", "File Locked", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return false;
                    }
                }
                
                // Serialize and write the file locally (PDM GetFileCopy on load should have put it in the right place)
                PdmSharedSettings sharedData = new PdmSharedSettings
                {
                    SettingsVersion = settingsVersion,
                    PropertyStandards = standards ?? new List<PropertyStandardSetting>()
                };

                XmlSerializer serializer = new XmlSerializer(typeof(PdmSharedSettings));
                using (FileStream stream = new FileStream(PdmPropertyStandardsAndVersionPath, FileMode.Create, FileAccess.Write)) // Create/Overwrite with Write access
                {
                    serializer.Serialize(stream, sharedData);
                }
                Logger.Info($"Successfully wrote shared settings to local file: {PdmPropertyStandardsAndVersionPath}");

                // Check in the file
                if (pdmFile == null && pdmFolder != null) // File was newly created locally and needs to be added to PDM
                {
                     // parentWndHandle needs to be int for AddFile
                     pdmFolder.AddFile((int)parentWndHandle, PdmPropertyStandardsAndVersionPath, "Added initial property standards and version file.");
                     Logger.Info($"Added new shared settings file to PDM: {PdmPropertyStandardsAndVersionPath}");
                     // After adding, get the file object to check it in if AddFile doesn't do it automatically or if further check-in comments are desired.
                     // This step might need refinement based on EPDM API specifics for AddFile + CheckIn.
                     // For now, we assume AddFile makes it available. If it needs explicit check-in:
                     pdmFile = vault.GetFileFromPath(PdmPropertyStandardsAndVersionPath, out pdmFolder); // Re-get the file
                     if (pdmFile != null) {
                         // parentWndHandle needs to be int for UnlockFile
                         pdmFile.UnlockFile((int)parentWndHandle, "Initial check-in of property standards and version file.");
                         Logger.Info($"Checked in newly added shared settings file: {PdmPropertyStandardsAndVersionPath}");
                     }
                }
                // else if (pdmFile != null && pdmFile.IsLockedByMe)
                else if (pdmFile != null && pdmFile.IsLocked && !string.IsNullOrEmpty(currentPdmUserName) && pdmFile.LockedByUser.Name.Equals(currentPdmUserName, StringComparison.OrdinalIgnoreCase))
                {
                    // parentWndHandle needs to be int for UnlockFile
                    pdmFile.UnlockFile((int)parentWndHandle, "Updated property standards and version.");
                    wasCheckedOutByThisOperation = false; // No longer checked out by this op
                    Logger.Info($"Checked in PDM shared settings file: {PdmPropertyStandardsAndVersionPath}");
                }
                return true;
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                Logger.Error($"PDM COMException during save/checkout/checkin for '{PdmPropertyStandardsAndVersionPath}': {comEx.Message} (ErrorCode: {comEx.ErrorCode:X})", comEx);
                 MessageBox.Show($"A PDM error occurred while saving settings: {comEx.Message}", "PDM Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // TODO: Potentially undo checkout if save failed and wasCheckedOutByThisOperation is true
                // if (wasCheckedOutByThisOperation && pdmFile != null) pdmFile.UndoLockFile(parentWndHandle);
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"Error saving PDM shared settings to '{PdmPropertyStandardsAndVersionPath}': {ex.Message}", ex);
                MessageBox.Show($"An error occurred while saving settings: {ex.Message}", "Save Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // TODO: Potentially undo checkout if save failed
                // if (wasCheckedOutByThisOperation && pdmFile != null && pdmFile.IsLockedByMe) pdmFile.UndoLockFile(parentWndHandle);
                return false;
            }
            finally
            {
                // Ensure file is unlocked if an unexpected error occurred during write & PDM check-in wasn't reached,
                // but only if this operation actually checked it out.
                // if (wasCheckedOutByThisOperation && pdmFile != null && pdmFile.IsLockedByMe)
                if (wasCheckedOutByThisOperation && pdmFile != null && pdmFile.IsLocked && !string.IsNullOrEmpty(currentPdmUserName) && pdmFile.LockedByUser.Name.Equals(currentPdmUserName, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        Logger.Warning($"Attempting to undo checkout for {PdmPropertyStandardsAndVersionPath} due to an error or incomplete save.");
                        // parentWndHandle needs to be int for UndoLockFile
                        pdmFile.UndoLockFile((int)parentWndHandle);
                    }
                    catch (Exception undoEx) { Logger.Error($"Failed to auto-undo PDM checkout: {undoEx.Message}"); }
                }
            }
        }
        
        private static long GetSolidWorksWindowHandle()
        {
            try
            {
                if (SwAppForPDM != null)
                {
                    Frame swFrame = SwAppForPDM.Frame() as Frame;
                    if (swFrame != null)
                    {
                        return (long)swFrame.GetHWnd();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.DebugLog($"Error getting SolidWorks main window handle: {ex.Message}. Falling back to 0.");
            }
            return 0; // Fallback if SW window can't be obtained
        }

        public static void SavePropertyMappings(List<PropertyMapping> mappings)
        {
            try
            {
                string filePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RoesleinAddInMappings.xml");
                using (FileStream stream = new FileStream(filePath, FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<PropertyMapping>));
                    serializer.Serialize(stream, mappings);
                }
            }
            catch (Exception)
            {
                // Handle exceptions
            }
        }

        public static List<PropertyMapping> LoadPropertyMappings()
        {
            try
            {
                string filePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RoesleinAddInMappings.xml");
                if (File.Exists(filePath))
                {
                    using (FileStream stream = new FileStream(filePath, FileMode.Open))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(List<PropertyMapping>));
                        return (List<PropertyMapping>)serializer.Deserialize(stream);
                    }
                }
            }
            catch (Exception)
            {
                // Handle exceptions
            }
            return new List<PropertyMapping>();
        }
        
        public static void SaveMaterialMappings(List<MaterialMapping> mappings)
        {
            try
            {
                // Ensure rows are numbered sequentially
                if (mappings != null && mappings.Count > 0)
                {
                    mappings = mappings.OrderBy(m => m.RowNumber).ToList();
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        mappings[i].RowNumber = i + 1;
                    }
                }
                
                string filePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RoesleinAddInMaterialMappings.xml");
                using (FileStream stream = new FileStream(filePath, FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<MaterialMapping>));
                    serializer.Serialize(stream, mappings);
                }
            }
            catch (Exception)
            {
                // Handle exceptions
            }
        }

        public static List<MaterialMapping> LoadMaterialMappings()
        {
            try
            {
                string filePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RoesleinAddInMaterialMappings.xml");
                if (File.Exists(filePath))
                {
                    using (FileStream stream = new FileStream(filePath, FileMode.Open))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(List<MaterialMapping>));
                        var mappings = (List<MaterialMapping>)serializer.Deserialize(stream);
                        
                        // Force renumbering of the rows starting at 1
                        if (mappings != null && mappings.Count > 0)
                        {
                            mappings = mappings.OrderBy(m => m.RowNumber).ToList();
                            for (int i = 0; i < mappings.Count; i++)
                            {
                                mappings[i].RowNumber = i + 1;
                            }
                        }
                        
                        return mappings;
                    }
                }
            }
            catch (Exception)
            {
                // Handle exceptions
            }
            
            // Return default material mappings if no saved mappings exist
            return new List<MaterialMapping>
            {
                // Keep existing default mappings
                new MaterialMapping { RowNumber = 1, SwMaterial = "ASTM A1008 Steel- Cold Rolled Sheet", DxfMaterial = "ASTM A1008 Steel- Cold Rolled Sheet" },
                new MaterialMapping { RowNumber = 2, SwMaterial = "AISI 304 Stainless Steel Sheet", DxfMaterial = "AISI 304 Stainless Steel Sheet" },
                new MaterialMapping { RowNumber = 3, SwMaterial = "ASTM A36 Steel- Hot Rolled Sheet", DxfMaterial = "ASTM A36 Steel- Hot Rolled Sheet" },
                new MaterialMapping { RowNumber = 4, SwMaterial = "ASTM A572 Steel- Hot Rolled plate", DxfMaterial = "ASTM A572 Steel- Hot Rolled plate" },
                
                // Add new mappings
                new MaterialMapping { RowNumber = 5, SwMaterial = "Sheet, 14GA, HR", DxfMaterial = "ASTM A1008 Steel- Cold Rolled Sheet" },
                new MaterialMapping { RowNumber = 6, SwMaterial = "Sheet, 12GA, CR", DxfMaterial = "ASTM A1008 Steel- Cold Rolled Sheet" },
                new MaterialMapping { RowNumber = 7, SwMaterial = "Sheet, 16GA, CR", DxfMaterial = "ASTM A1008 Steel- Cold Rolled Sheet" },
                new MaterialMapping { RowNumber = 8, SwMaterial = "Sheet, 18GA, CR", DxfMaterial = "ASTM A1008 Steel- Cold Rolled Sheet" },
                new MaterialMapping { RowNumber = 9, SwMaterial = "Sheet, 10GA, HR", DxfMaterial = "ASTM A1008 Steel- Cold Rolled Sheet" },
                new MaterialMapping { RowNumber = 10, SwMaterial = "PLATE, .19 HR", DxfMaterial = "ASTM A572 Steel- Hot Rolled plate" },
                new MaterialMapping { RowNumber = 11, SwMaterial = "PLATE, .25 HR", DxfMaterial = "ASTM A572 Steel- Hot Rolled plate" },
                new MaterialMapping { RowNumber = 12, SwMaterial = "PLATE, .375 HR", DxfMaterial = "ASTM A572 Steel- Hot Rolled plate" },
                new MaterialMapping { RowNumber = 13, SwMaterial = "PLATE, .5 HR", DxfMaterial = "ASTM A572 Steel- Hot Rolled plate" },
                new MaterialMapping { RowNumber = 14, SwMaterial = "PLATE, .75 HR", DxfMaterial = "ASTM A572 Steel- Hot Rolled plate" },
                new MaterialMapping { RowNumber = 15, SwMaterial = "PLATE, 1.00 HR", DxfMaterial = "ASTM A572 Steel- Hot Rolled plate" },
                new MaterialMapping { RowNumber = 16, SwMaterial = "Sheet, 12GA, SS", DxfMaterial = "AISI 304 Stainless Steel Sheet" },
                new MaterialMapping { RowNumber = 17, SwMaterial = "Sheet, 14GA, SS", DxfMaterial = "AISI 304 Stainless Steel Sheet" },
                new MaterialMapping { RowNumber = 18, SwMaterial = "Sheet, 16GA, SS", DxfMaterial = "AISI 304 Stainless Steel Sheet" },
                new MaterialMapping { RowNumber = 19, SwMaterial = "Sheet, 18GA, SS", DxfMaterial = "AISI 304 Stainless Steel Sheet" },
                new MaterialMapping { RowNumber = 20, SwMaterial = "Sheet, 20GA, SS", DxfMaterial = "AISI 304 Stainless Steel Sheet" },
                new MaterialMapping { RowNumber = 21, SwMaterial = "Sheet, 7GA, SS", DxfMaterial = "AISI 304 Stainless Steel Sheet" },
                new MaterialMapping { RowNumber = 22, SwMaterial = "Cold Roll Steel 1018", DxfMaterial = "ASTM A1008 Steel- Cold Rolled Sheet" },
                new MaterialMapping { RowNumber = 23, SwMaterial = "Plain Carbon Steel", DxfMaterial = "ASTM A1008 Steel- Cold Rolled Sheet" }
            };
        }

        public static void SaveThicknessMappings(List<ThicknessMapping> mappings)
        {
            try
            {
                // Ensure rows are numbered sequentially
                if (mappings != null && mappings.Count > 0)
                {
                    mappings = mappings.OrderBy(m => m.RowNumber).ToList();
                    for (int i = 0; i < mappings.Count; i++)
                    {
                        mappings[i].RowNumber = i + 1;
                    }
                }
                
                string filePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RoesleinAddInThicknessMappings.xml");
                using (FileStream stream = new FileStream(filePath, FileMode.Create))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<ThicknessMapping>));
                    serializer.Serialize(stream, mappings);
                }
            }
            catch (Exception)
            {
                // Handle exceptions
            }
        }

        public static List<ThicknessMapping> LoadThicknessMappings()
        {
            try
            {
                string filePath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData), "RoesleinAddInThicknessMappings.xml");
                if (File.Exists(filePath))
                {
                    using (FileStream stream = new FileStream(filePath, FileMode.Open))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(List<ThicknessMapping>));
                        var mappings = (List<ThicknessMapping>)serializer.Deserialize(stream);
                        
                        // Force renumbering of the rows starting at 1
                        if (mappings != null && mappings.Count > 0)
                        {
                            mappings = mappings.OrderBy(m => m.RowNumber).ToList();
                            for (int i = 0; i < mappings.Count; i++)
                            {
                                mappings[i].RowNumber = i + 1;
                            }
                        }
                        
                        return mappings;
                    }
                }
            }
            catch (Exception)
            {
                // Handle exceptions
            }
            
            // Return default thickness mappings if no saved mappings exist
            return new List<ThicknessMapping>
            {
                new ThicknessMapping { RowNumber = 1, SwThickness = ".1196", DxfThickness = ".12" },
                new ThicknessMapping { RowNumber = 2, SwThickness = ".0897", DxfThickness = ".09" },
                new ThicknessMapping { RowNumber = 3, SwThickness = ".0598", DxfThickness = ".06" },
                new ThicknessMapping { RowNumber = 4, SwThickness = ".0478", DxfThickness = ".048" }
            };
        }

        // Static constructor to initialize ThicknessMappings
        static Settings()
        {
            // Initialize ThicknessMappings if needed
            if (ThicknessMappings == null || ThicknessMappings.Count == 0)
            {
                // Load thickness mappings from file
                var loadedMappings = LoadThicknessMappings();
                if (loadedMappings != null && loadedMappings.Count > 0)
                {
                    // Convert list to dictionary
                    foreach (var mapping in loadedMappings)
                    {
                        if (double.TryParse(mapping.SwThickness, out double swThickness) && 
                            double.TryParse(mapping.DxfThickness, out double dxfThickness))
                        {
                            if (!ThicknessMappings.ContainsKey(swThickness))
                            {
                                ThicknessMappings.Add(swThickness, dxfThickness);
                            }
                        }
                    }
                }
            }
        }

        public static void SaveThicknessMappings()
        {
            try
            {
                // Convert the dictionary to a list of ThicknessMapping objects
                var mappingsList = new List<ThicknessMapping>();
                int rowNum = 1;
                
                foreach (var pair in ThicknessMappings)
                {
                    mappingsList.Add(new ThicknessMapping
                    {
                        RowNumber = rowNum++,
                        SwThickness = pair.Key.ToString(),
                        DxfThickness = pair.Value.ToString()
                    });
                }
                
                SaveThicknessMappings(mappingsList);
            }
            catch (Exception)
            {
                // Handle exceptions
            }
        }

        // --- Methods for PropertyStandards (similar to PropertyMappings) ---
        private static void LoadDefaultPropertyStandardsStatic(List<PropertyStandardSetting> standardsList)
        {
            if (standardsList == null) standardsList = new List<PropertyStandardSetting>();
            standardsList.Clear(); // Clear existing before adding defaults
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Part Number",           UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Description",           UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Revision",              UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Status",                UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Shop Route",            UseDefaultValue = false, DefaultValueExpr = "FAB",                             IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true }); // Assuming FAB is the intent for Shop Route default
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Spare Part",            UseDefaultValue = false, DefaultValueExpr = "No",                              IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true }); // Assuming No is the intent
            standardsList.Add(new PropertyStandardSetting { PropertyName = "MFG Stocked Item",      UseDefaultValue = false, DefaultValueExpr = "No",                              IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });// Assuming No is the intent
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Weight",                UseDefaultValue = true,  DefaultValueExpr = "$PRP:SW-Mass",                 IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Material",              UseDefaultValue = true,  DefaultValueExpr = "$PRPMODEL:\"SW-Material\"",        IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Sheet Metal Thickness", UseDefaultValue = true,  DefaultValueExpr = "$PRPSHEET:\"Thickness\"",          IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Vendor",                UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Vendor Part Number",    UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = true,  UseOnDrawingFiles = true });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Raw Material Number",   UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Unit of Measurement",   UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Raw Mat Amount",        UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "legacy Part Number",    UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Legacy Unit of Measure",UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Legacy Raw Mat Amount", UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "NC Punch Programs",     UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "NC Punch Sheet Size",   UseDefaultValue = false, DefaultValueExpr = "",                                IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true,  UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "Raw Material Description", UseDefaultValue = false, DefaultValueExpr = "", IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true, UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
            standardsList.Add(new PropertyStandardSetting { PropertyName = "legacy Part Description",  UseDefaultValue = false, DefaultValueExpr = "", IsCustomProperty = true, IsConfigSpecific = true, UseOnPartFiles = true, UseOnAssemblyFiles = false, UseOnDrawingFiles = false });
        }

        public static List<RawMaterial> LoadRawMaterial()
        {
            // Updated to use the new PDM CSV loading logic
            // The specific path should ideally be configurable or a constant.
            string pdmCsvPath = @"C:\PCSVAULT\SolidWorks Settings\Roeslein SW AddIn\Raw Material SheetMetal.csv";
            Logger.Info($"LoadRawMaterial (static) is now attempting to load from PDM CSV: {pdmCsvPath}");
            List<RawMaterial> materials = LoadRawMaterialsFromPdmCsv(pdmCsvPath);

            if (materials == null || materials.Count == 0)
            {
                Logger.Warning($"LoadRawMaterial (static) did not find any materials in PDM CSV: {pdmCsvPath}. Returning empty list.");
                return new List<RawMaterial>(); // Return empty list instead of null
            }
            return materials;
        }

        public static List<RawMaterial> LoadRawMaterialsFromPdmCsv(string csvFilePath)
        {
            var rawMaterials = new List<RawMaterial>();
            if (!File.Exists(csvFilePath))
            {
                Logger.Error($"Raw material CSV file not found at: {csvFilePath}");
                return rawMaterials; // Return empty list, not null
            }

            try
            {
                var lines = File.ReadAllLines(csvFilePath);
                if (lines.Length <= 1) // Header + at least one data row
                {
                    Logger.Warning($"Raw material CSV file is empty or contains only a header: {csvFilePath}");
                    return rawMaterials; // Return empty list
                }

                // Assuming the first line is the header
                // "PartNumber,LegacyPartNumber,Material,Thickness,Description,TotalSqInch,SheetLength,SheetHeight,LastCost"
                // We'll map by known property names of RawMaterial class for flexibility,
                // but for this implementation, we'll assume a fixed order for simplicity matching the typical CSV export.
                // A more robust solution would map columns by header names.

                for (int i = 1; i < lines.Length; i++) // Skip header row
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue; // Skip empty lines

                    var values = line.Split(',');

                    if (values.Length < 9) // Ensure enough columns for critical data
                    {
                        Logger.Warning($"Skipping malformed line {i + 1} in CSV {csvFilePath}. Expected at least 9 columns, got {values.Length}. Line: {line}");
                        continue;
                    }

                    try
                    {
                        var material = new RawMaterial
                        {
                            PartNumber = values[0].Trim(),
                            LegacyPartNumber = values[1].Trim(),
                            Material = values[2].Trim(),
                            Thickness = double.TryParse(values[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double thk) ? thk : 0.0,
                            Description = values[4].Trim(),
                            TotalSqInch = double.TryParse(values[5].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double sqInch) ? sqInch : 0.0,
                            Length = double.TryParse(values[6].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double sLength) ? sLength : 0.0,
                            Width = double.TryParse(values[7].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double sHeight) ? sHeight : 0.0,
                            LastCost = decimal.TryParse(values[8].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal cost) ? cost : 0.0m,
                            UnitOfMeasure = "EA" // Default or determine from elsewhere if needed
                        };
                        rawMaterials.Add(material);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Error parsing line {i + 1} in CSV {csvFilePath}: {line}. Error: {ex.Message}");
                    }
                }
                Logger.Info($"Successfully loaded {rawMaterials.Count} raw materials from CSV: {csvFilePath}");
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to read or parse raw material CSV file {csvFilePath}. Error: {ex.Message}");
                // Return whatever was loaded before the error, or an empty list if the error was at the start.
            }
            return rawMaterials;
        }

        public class RawMaterial
        {
            public string PartNumber { get; set; }
            public string LegacyPartNumber { get; set; }
            public string Material { get; set; }
            public double Thickness { get; set; }
            public string Description { get; set; }
            public double TotalSqInch { get; set; }
            public double Length { get; set; }
            public double Width { get; set; }
            public decimal LastCost { get; set; }
            public string UnitOfMeasure { get; set; }

            public RawMaterial() // Add a parameterless constructor for XML serialization if ever needed directly
            {
                UnitOfMeasure = "EA"; // Default
            }
        }
    }

    // New PdmSharedSettings class definition
    [Serializable]
    public class PdmSharedSettings
    {
        public string SettingsVersion { get; set; }
        public List<PropertyStandardSetting> PropertyStandards { get; set; }

        public PdmSharedSettings()
        {
            SettingsVersion = "25.1"; // Default if file doesn't exist or version is missing
            PropertyStandards = new List<PropertyStandardSetting>();
        }
    }
} 