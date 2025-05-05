using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Windows.Forms;

namespace Roeslein_Addin
{
    public class PropertyMapping
    {
        public string PropertyName { get; set; }
        public string PropertyValue { get; set; }

        public PropertyMapping()
        {
            PropertyName = string.Empty;
            PropertyValue = string.Empty;
        }

        public PropertyMapping(string propertyName, string propertyValue)
        {
            PropertyName = propertyName;
            PropertyValue = propertyValue;
        }
    }

    public class MaterialMapping
    {
        public string SwMaterial { get; set; }
        public string DxfMaterial { get; set; }

        public MaterialMapping()
        {
            SwMaterial = string.Empty;
            DxfMaterial = string.Empty;
        }

        public MaterialMapping(string swMaterial, string dxfMaterial)
        {
            SwMaterial = swMaterial;
            DxfMaterial = dxfMaterial;
        }
    }

    public class AppSettings
    {
        // ... existing code ...

        private List<MaterialMapping> _materialMappings;
        private const string MaterialMappingsFileName = "MaterialMappings.xml";

        // ... existing code ...

        /// <summary>
        /// Create default material mappings
        /// </summary>
        private void CreateDefaultMaterialMappings()
        {
            _materialMappings = new List<MaterialMapping>
            {
                new MaterialMapping("ASTM A1008 Steel- Cold Rolled Sheet", "ASTM A1008 Steel- Cold Rolled Sheet"),
                new MaterialMapping("AISI 304 Stainless Steel Sheet", "AISI 304 Stainless Steel Sheet"),
                new MaterialMapping("ASTM A36 Steel- Hot Rolled Sheet", "ASTM A36 Steel- Hot Rolled Sheet"),
                new MaterialMapping("ASTM A572 Steel- Hot Rolled plate", "ASTM A572 Steel- Hot Rolled plate")
            };
        }

        /// <summary>
        /// Load the material mappings from the XML file
        /// </summary>
        /// <returns>The list of material mappings</returns>
        public List<MaterialMapping> LoadMaterialMappings()
        {
            if (_materialMappings != null)
            {
                return _materialMappings;
            }

            try
            {
                string filePath = Path.Combine(SettingsDirectory, MaterialMappingsFileName);
                if (File.Exists(filePath))
                {
                    using (StreamReader reader = new StreamReader(filePath))
                    {
                        XmlSerializer serializer = new XmlSerializer(typeof(List<MaterialMapping>));
                        _materialMappings = (List<MaterialMapping>)serializer.Deserialize(reader);
                    }
                }
                else
                {
                    CreateDefaultMaterialMappings();
                    SaveMaterialMappings(_materialMappings);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading material mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                CreateDefaultMaterialMappings();
            }

            return _materialMappings;
        }

        /// <summary>
        /// Save the material mappings to the XML file
        /// </summary>
        /// <param name="mappings">The material mappings to save</param>
        /// <returns>True if the save was successful</returns>
        public bool SaveMaterialMappings(List<MaterialMapping> mappings)
        {
            _materialMappings = mappings;

            try
            {
                EnsureDirectoryExists();
                string filePath = Path.Combine(SettingsDirectory, MaterialMappingsFileName);
                
                using (StreamWriter writer = new StreamWriter(filePath))
                {
                    XmlSerializer serializer = new XmlSerializer(typeof(List<MaterialMapping>));
                    serializer.Serialize(writer, mappings);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving material mappings: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        // ... existing code ...
    }
} 