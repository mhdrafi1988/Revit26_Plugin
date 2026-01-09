using Autodesk.Revit.DB;
using Revit22_Plugin.AutoRoofSections.Models;
using System;

namespace Revit22_Plugin.AutoRoofSections.Services
{
    public class SectionNaming
    {
        private readonly SectionSettings _settings;

        public SectionNaming(SectionSettings settings)
        {
            _settings = settings;
        }

        public string BuildName(Element roof, int serial)
        {
            string levelName = GetRoofLevel(roof);
            string prefix = _settings.Prefix ?? "";
            string id = roof.Id.IntegerValue.ToString();
            string s = serial.ToString("000");

            string name = $"{prefix}ROOF_{levelName}_{id}_{s}";

            if (_settings.IncludeTimestamp)
                name += "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            return name;
        }

        private string GetRoofLevel(Element roof)
        {
            Parameter p = roof.get_Parameter(BuiltInParameter.ROOF_BASE_LEVEL_PARAM);
            if (p != null && p.AsElementId() != ElementId.InvalidElementId)
            {
                Level lvl = roof.Document.GetElement(p.AsElementId()) as Level;
                if (lvl != null) return lvl.Name;
            }
            return "NOLEVEL";
        }
    }
}
