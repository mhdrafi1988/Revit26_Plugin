using Autodesk.Revit.DB;
using Revit26_Plugin.SectionManager_V07.Models;
using Revit26_Plugin.SectionPlacement_V07.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionManager_V07.Services
{
    /// <summary>
    /// Collects section views for Section Manager workflows.
    /// Feature-scoped to SectionManager to avoid cross-module collisions.
    /// </summary>
    public class SectionManagerSectionCollectorService
    {
        private readonly Document _doc;

        public SectionManagerSectionCollectorService(Document doc)
        {
            _doc = doc;
        }

        /// <summary>
        /// Returns all non-template section views in the document.
        /// </summary>
        public IList<SectionItem> GetAllSections()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v =>
                    !v.IsTemplate &&
                    v.ViewType == ViewType.Section)
                .OrderBy(v => v.Name)
                .Select(v => new SectionItem(v))
                .ToList();
        }
    }
}
