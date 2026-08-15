using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SectionManager.V008.Models;

namespace Revit26_Plugin.SectionManager.V008.Services
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
