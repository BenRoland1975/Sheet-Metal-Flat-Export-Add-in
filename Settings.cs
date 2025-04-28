using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Diagnostics;

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
        public string LogFilePath { get; set; }
        public string DrawingTemplatePath { get; set; }
        
        private const string RegistryKeyPath = @"Software\Roeslein\SolidWorksAddIn";
        private static readonly string DefaultSettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Roeslein\Settings\RoesleinAddInSettings.xml");
        
        private static readonly string DefaultLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Roeslein\Logs\RoesleinAddIn.log");

        public Settings()
        {
            // Initialize with default values
            LastExportFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Roeslein", "DXF Exports");
            
            LogFilePath = DefaultLogPath;
            DrawingTemplatePath = "";

            ShowBendLines = true;
            TextLayerName = "TextNotes";
            BendLineLayerName = "BendLines";
            TextLocation = "Centered";

            UseCustomScale = false;
            DrawingScale = 1.0;
            AddDimensions = true;
            ImportModelDimensions = true;
            ExportHiddenGeometry = false;
            
            AddTitleBlockInfo = true;
            TitleBlockText = "Roeslein & Associates - Sheet Metal Flat Pattern";
            
            LoggingEnabled = true;
        }

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
                        key.SetValue("LoggingEnabled", LoggingEnabled ? 1 : 0);
                        key.SetValue("DrawingTemplatePath", DrawingTemplatePath);
                    }
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

                            object loggingEnabled = key.GetValue("LoggingEnabled");
                            if (loggingEnabled != null)
                            {
                                settings.LoggingEnabled = Convert.ToInt32(loggingEnabled) == 1;
                            }

                            object templatePath = key.GetValue("DrawingTemplatePath");
                            if (templatePath != null)
                            {
                                settings.DrawingTemplatePath = templatePath.ToString();
                            }
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
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoesleinAddInMappings.xml");
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
                string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RoesleinAddInMappings.xml");
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
    }
} 