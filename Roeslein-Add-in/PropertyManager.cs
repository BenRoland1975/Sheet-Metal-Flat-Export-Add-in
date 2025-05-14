using System;
using System.Windows.Forms;

namespace RoesleinAddin
{
    public class PropertyManager
    {
        private void SetPropertyValue(ICustomPropertyManager customPropertyManager, string propertyName, string value, bool useDefaultValue, int docType)
        {
            try
            {
                // Check if property exists
                string[] existingProps = customPropertyManager.GetNames() ?? new string[0];
                bool propertyExists = existingProps.Contains(propertyName, StringComparer.OrdinalIgnoreCase);

                // If property exists and we're not using default value, skip it
                if (propertyExists && !useDefaultValue)
                {
                    return;
                }

                // Get the correct expression based on property name and docType
                string expression = GetPropertyExpression(propertyName, docType);
                
                // If using default value, use the expression, otherwise use the provided value
                string valueToSet = useDefaultValue && !string.IsNullOrEmpty(expression) ? expression : value;

                if (propertyExists)
                {
                    // Update existing property
                    customPropertyManager.Set2(propertyName, valueToSet);
                }
                else
                {
                    // Only add if valueToSet is not blank, or if you want blank properties
                    if (!string.IsNullOrEmpty(valueToSet))
                    {
                        customPropertyManager.Add3(propertyName, (int)swCustomInfoType_e.swCustomInfoText, valueToSet, (int)swCustomPropertyAddOption_e.swCustomPropertyDeleteAndAdd);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error setting property {propertyName}: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private string GetPropertyExpression(string propertyName, int docType)
        {
            // docType: (int)swDocumentTypes_e.swDocPART or (int)swDocumentTypes_e.swDocASSEMBLY
            string suffix = docType == 2 ? "@*.SLDASM" : "@*.SLDPRT"; // 2 = Assembly, 1 = Part
            switch (propertyName.ToLower())
            {
                case "weight":
                    return $"SW-Mass{suffix}";
                case "material":
                    return $"SW-Material{suffix}";
                case "thickness":
                    return $"Thickness{suffix}";
                case "description":
                    return "$PRP:\"Description\"";
                case "revision":
                    return "$PRP:\"Revision\"";
                case "part number":
                    return "$PRP:\"Part Number\"";
                case "vendor":
                    return "$PRP:\"Vendor\"";
                case "cost":
                    return "$PRP:\"Cost\"";
                case "notes":
                    return "$PRP:\"Notes\"";
                default:
                    return "";
            }
        }
    }
} 