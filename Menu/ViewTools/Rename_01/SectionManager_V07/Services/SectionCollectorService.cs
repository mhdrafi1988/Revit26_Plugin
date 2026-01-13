using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager_V07.Models;

namespace Revit26_Plugin.SectionManager_V07.Services
{
    public class SectionCollectorService
    {
        public List<SectionInfo> Collect(UIDocument uiDoc)
        {
            return new FilteredElementCollector(uiDoc.Document)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .Select(v => new SectionInfo(v.Id, v.Name))
                .ToList();
        }
    }
}
