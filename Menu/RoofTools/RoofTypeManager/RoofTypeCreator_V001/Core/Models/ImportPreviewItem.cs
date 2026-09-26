using System.Collections.Generic;

namespace Revit26_Plugin.RoofTypeCreator.V001.Core.Models
{
    public class ImportPreviewItem
    {
        public string           SheetName        { get; set; }
        public string           TypeName         { get; set; }
        public int              LayerCount       { get; set; }
        public double           TotalThicknessMm { get; set; }
        public ImportStatus     Status           { get; set; }
        public List<LayerData>  Layers           { get; set; } = new List<LayerData>();
        public string           WrapsAtInserts   { get; set; } = "NoInsertWrap";
        public string           WrapsAtEnds      { get; set; } = "NoWrap";
    }

    public enum ImportStatus { New, Dup }

    public class LayerData
    {
        public string MaterialName  { get; set; }
        public double ThicknessMm   { get; set; }
        public string LayerFunction { get; set; }
        public int    Priority      { get; set; } = 1;
        public bool   IsVariable    { get; set; }
    }
}
