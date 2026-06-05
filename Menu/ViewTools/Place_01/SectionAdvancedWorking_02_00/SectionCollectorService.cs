using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit22_Plugin.SectionPlacer.Services
{
    /// <summary>
    /// Collects section views that are NOT placed on any sheet.
    /// </summary>
    public class SectionCollectorService
    {
        private readonly Document _doc;
        public SectionCollectorService(Document doc) => _doc = doc;

        /// <summary>
        /// Collect all section views that are unplaced and not templates.
        /// </summary>
        public List<ViewSection> CollectUnplacedSections()
        {
            // Collect all section views (not templates)
            var sections = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v =>
                    !v.IsTemplate &&                   // ignore view templates
                    v.ViewType == ViewType.Section)    // only true section views
                .ToList();

            // Filter out placed sections (already on sheets)
            var unplaced = sections
                .Where(v => !IsPlaced(v))
                .OrderBy(v => v.Name) // optional sorting for UI clarity
                .ToList();

            return unplaced;
        }

        /// <summary>
        /// Checks whether the given section is already placed on a sheet.
        /// </summary>
        private bool IsPlaced(ViewSection section)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Any(vp => vp.ViewId == section.Id);
        }
    }
}
