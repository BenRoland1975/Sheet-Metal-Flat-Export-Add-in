using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace RoesleinAddIn
{
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
        
        // Mark instance dictionary with XmlIgnore to prevent serialization errors
        [System.Xml.Serialization.XmlIgnore]
        public Dictionary<double, double> ThicknessMapping { get; set; }
        
        // Mark static dictionary with XmlIgnore to prevent serialization errors
        [System.Xml.Serialization.XmlIgnore]
        public static Dictionary<double, double> ThicknessMappings { get; set; } = new Dictionary<double, double>();
        
        private const string RegistryKeyPath = @"Software\Roeslein\SolidWorksAddIn";
        private static readonly string DefaultSettingsPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
            @"Roeslein\Settings\RoesleinAddInSettings.xml");
        
        private static readonly string DefaultLogPath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.CommonApplicationData),
            @"Roeslein\Logs\RoesleinAddIn.log");

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
        }

        [System.Xml.Serialization.XmlIgnore]
        public List<PropertyMapping> PropertyMappings { get; set; }
        
        [System.Xml.Serialization.XmlIgnore]
        public List<MaterialMapping> MaterialMappings { get; set; }

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
                    settings = new Settings();

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
                                settings.LoggingEnabled = Convert.ToInt32(loggingEnabled) == 1;
                            }

                            object debugLoggingEnabled = key.GetValue("DebugLoggingEnabled");
                            if (debugLoggingEnabled != null)
                            {
                                settings.DebugLoggingEnabled = Convert.ToInt32(debugLoggingEnabled) == 1;
                            }

                            object logFilePath = key.GetValue("LogFilePath");
                            if (logFilePath != null)
                            {
                                settings.LogFilePath = logFilePath.ToString();
                            }

                            object debugLogFilePath = key.GetValue("DebugLogFilePath");
                            if (debugLogFilePath != null)
                            {
                                settings.DebugLogFilePath = debugLogFilePath.ToString();
                            }

                            object templatePath = key.GetValue("DrawingTemplatePath");
                            if (templatePath != null)
                            {
                                settings.DrawingTemplatePath = templatePath.ToString();
                            }
                        }
                    }
                }
                
                // Initialize or update instance ThicknessMapping from static ThicknessMappings
                if (settings != null)
                {
                    // Make sure the instance has a dictionary
                    if (settings.ThicknessMapping == null)
                    {
                        settings.ThicknessMapping = new Dictionary<double, double>();
                    }
                    
                    // If the static dictionary has mappings, copy to instance
                    if (ThicknessMappings != null && ThicknessMappings.Count > 0)
                    {
                        settings.ThicknessMapping.Clear();
                        foreach (var mapping in ThicknessMappings)
                        {
                            settings.ThicknessMapping[mapping.Key] = mapping.Value;
                        }
                    }
                }
            }
            catch (Exception ex) // Catch specific exception
            {
                // Log the exception that occurred during settings load
                Debug.WriteLine($"[DEBUG] CRITICAL ERROR loading settings: {ex.Message}\nStackTrace: {ex.StackTrace}");
                // Attempt to use the main logger, but be cautious as it might also fail
                try { Logger.Error("Failed to load application settings.", ex); } catch { }
                
                // If any error occurs, return a new settings object with defaults
                settings = new Settings();
            }

            return settings ?? new Settings();
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
    }

    [Serializable]
    public class ThicknessMapping
    {
        public string SwThickness { get; set; }
        public string DxfThickness { get; set; }
        public int RowNumber { get; set; }
    }
} 