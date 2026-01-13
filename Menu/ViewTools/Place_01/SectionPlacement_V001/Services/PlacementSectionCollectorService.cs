using Autodesk.Revit.DB;
using Revit26_Plugin.SectionPlacement_V07.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionPlacement_V07.Services
{
    /// <summary>
    /// Collects only unplaced section views for sheet placement workflows.
    /// Feature-scoped to SectionPlacement.
    /// </summary>
    public class PlacementSectionCollectorService
    {
        private readonly Document _doc;

        public PlacementSectionCollectorService(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Returns section views that are not yet placed on any sheet.
        /// </summary>
        public IList<SectionItem> GetUnplacedSections()
        {
            var placedViewIds = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(vp => vp.ViewId)
                .ToHashSet();

            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v =>
                    !v.IsTemplate &&
                    v.ViewType == ViewType.Section &&
                    !placedViewIds.Contains(v.Id))
                .OrderBy(v => v.Name)
                .Select(v => new SectionItem(v))
                .ToList();
        }
    }
}
