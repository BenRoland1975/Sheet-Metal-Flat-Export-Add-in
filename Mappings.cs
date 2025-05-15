using System;

namespace RoesleinAddIn
{
    [Serializable]
    public class PropertyMapping
    {
        public string DxfPropertyName { get; set; }
        public string SwCustomProperty { get; set; }
        public int RowNumber { get; set; }
    }

    [Serializable]
    public class MaterialMapping
    {
        public string SwMaterial { get; set; }
        public string DxfMaterial { get; set; }
        public int RowNumber { get; set; }
    }

    [Serializable]
    public class ThicknessMapping
    {
        public string SwThickness { get; set; }
        public string DxfThickness { get; set; }
        public int RowNumber { get; set; }
    }
} 