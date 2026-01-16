using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_301.Services
{
    public class SectionCollectorService
    {
        private readonly Document _doc;
        public SectionCollectorService(Document doc) => _doc = doc;

        public List<ViewSection> CollectUnplacedSections()
        {
            var placed = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(v => v.ViewId)
                .ToHashSet();

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate && !placed.Contains(v.Id))
                .OrderBy(v => v.Name)
                .ToList();
        }
    }
}
