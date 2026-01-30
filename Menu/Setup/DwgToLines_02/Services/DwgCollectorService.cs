using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgSymbolicConverter_V02.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
{
    public static class DwgCollectorService
    {
        public static IList<DwgItemModel> Collect(Document doc)
        {
            var results = new List<DwgItemModel>();

            if (doc == null)
                return results;

            // 1?? Active document DWGs
            results.AddRange(CollectFromDocument(doc, "[Active]"));

            // 2?? Linked document DWGs
            var links = new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>();

            foreach (var link in links)
            {
                Document linkDoc = link.GetLinkDocument();
                if (linkDoc == null) continue;

                results.AddRange(CollectFromDocument(
                    linkDoc,
                    $"[Link] {linkDoc.Title}"
                ));
            }

            return results;
        }

        private static IEnumerable<DwgItemModel> CollectFromDocument(
            Document doc,
            string sourceLabel)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ImportInstance))
                .Cast<ImportInstance>()
                .Where(i =>
                    i.Category != null &&
                    i.Category.Id.Value ==
                    (int)BuiltInCategory.OST_ImportObjectStyles)
                .Select(i => new DwgItemModel(i, sourceLabel));
        }
    }
}
