using System;

namespace RoesleinAddIn
{
    [Serializable]
    public class PropertyStandardSetting
    {
        public int ID { get; set; }
        public bool IsRequired { get; set; }
        public string PropertyName { get; set; }
        public string DefaultValueOrExpression { get; set; }
        public bool IsCustomPropertyTarget { get; set; }
        public bool IsConfigurationSpecificTarget { get; set; }
        public bool UseOnPartFiles { get; set; }
        public bool UseOnAssemblyFiles { get; set; }
        public bool UseOnDrawingFiles { get; set; }

        public PropertyStandardSetting()
        {
            IsRequired = true;
            PropertyName = string.Empty;
            DefaultValueOrExpression = string.Empty;
            IsCustomPropertyTarget = true;
            IsConfigurationSpecificTarget = false;
            UseOnPartFiles = true;
            UseOnAssemblyFiles = true;
            UseOnDrawingFiles = true;
        }

        public PropertyStandardSetting(int id, bool isRequired, string propertyName, string defaultValue, bool isCustomProp, bool isConfigSpecific, bool useOnPartFiles = true, bool useOnAssemblyFiles = true, bool useOnDrawingFiles = true)
        {
            ID = id;
            IsRequired = isRequired;
            PropertyName = propertyName;
            DefaultValueOrExpression = defaultValue;
            IsCustomPropertyTarget = isCustomProp;
            IsConfigurationSpecificTarget = isConfigSpecific;
            UseOnPartFiles = useOnPartFiles;
            UseOnAssemblyFiles = useOnAssemblyFiles;
            UseOnDrawingFiles = useOnDrawingFiles;
        }
    }
} 