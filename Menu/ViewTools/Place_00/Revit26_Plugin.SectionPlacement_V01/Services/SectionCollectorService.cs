using Autodesk.Revit.DB;
using Revit26_Plugin.SectionPlacement_V07.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionPlacement_V07.Services
{
    public class SectionCollectorService
    {
        private readonly Document _doc;

        public SectionCollectorService(Document doc)
        {
            _doc = doc;
        }

        public IList<SectionItem> GetUnplacedSections()
        {
            var placedIds = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(v => v.ViewId)
                .ToHashSet();

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate &&
                            v.ViewType == ViewType.Section &&
                            !placedIds.Contains(v.Id))
                .OrderBy(v => v.Name)
                .Select(v => new SectionItem(v))
                .ToList();
        }
    }
}
