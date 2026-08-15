using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP.V019.Services
{
    public static class ViewSheetLookupService
    {
        public static Dictionary<ElementId, List<string>> Build(Document doc)
        {
            var result = new Dictionary<ElementId, List<string>>();

            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>();

            foreach (var vp in viewports)
            {
                if (doc.GetElement(vp.SheetId) is not ViewSheet sheet)
                    continue;

                if (!result.TryGetValue(vp.ViewId, out var list))
                {
                    list = new List<string>();
                    result[vp.ViewId] = list;
                }

                if (!list.Contains(sheet.SheetNumber))
                    list.Add(sheet.SheetNumber);
            }

            return result;
        }

        /// <summary>
        /// Same viewport scan as <see cref="Build"/>, but also captures the
        /// per-placement Detail Number (VIEWPORT_DETAIL_NUMBER) alongside the
        /// sheet number. Used for the drafting-view combo dropdown columns
        /// (Name | Detail # | Sheet). Kept separate from Build() so the
        /// existing section/elevation sheet-lookup path (ViewItemViewModel)
        /// is untouched.
        /// </summary>
        public static Dictionary<ElementId, List<(string SheetNumber, string DetailNumber)>> BuildWithDetailNumbers(Document doc)
        {
            var result = new Dictionary<ElementId, List<(string, string)>>();

            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>();

            foreach (var vp in viewports)
            {
                if (doc.GetElement(vp.SheetId) is not ViewSheet sheet)
                    continue;

                var detailNumber = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)?.AsString() ?? string.Empty;

                if (!result.TryGetValue(vp.ViewId, out var list))
                {
                    list = new List<(string, string)>();
                    result[vp.ViewId] = list;
                }

                list.Add((sheet.SheetNumber, detailNumber));
            }

            return result;
        }
    }
}
