using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.PlanFromScopeBox.V003.Core.Models;

namespace Revit26_Plugin.PlanFromScopeBox.V003.Core.Services
{
    /// <summary>
    /// Finds views already cropped to each scope box (VIEWER_VOLUME_OF_INTEREST_CROP) and,
    /// for each such view, the sheet it's placed on (via Viewport), if any.
    /// Pure read-only Revit queries — safe to call before any transaction, same convention
    /// as ScopeBoxScanner.
    /// </summary>
    public sealed class ExistingUsageResolver
    {
        /// <summary>
        /// Returns a map of scope-box ElementId.Value -> ScopeBoxUsage. Scope boxes with no
        /// cropped views are simply absent from the dictionary (caller treats missing as
        /// "not used").
        /// </summary>
        public Dictionary<long, ScopeBoxUsage> Resolve(Document doc)
        {
            var usageByScopeBox = new Dictionary<long, ScopeBoxUsage>();

            // Map every placed view -> its sheet number, via Viewport (View has no direct
            // SheetId property; the sheet relationship only exists on the Viewport instance).
            var sheetNumberByViewId = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Select(vp => new { vp.ViewId, Sheet = doc.GetElement(vp.SheetId) as ViewSheet })
                .Where(x => x.Sheet != null)
                .ToDictionary(x => x.ViewId.Value, x => x.Sheet.SheetNumber);

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate);

            foreach (View view in views)
            {
                Parameter sbParam = view.get_Parameter(BuiltInParameter.VIEWER_VOLUME_OF_INTEREST_CROP);
                if (sbParam == null) continue;

                ElementId sbId = sbParam.AsElementId();
                if (sbId == null || sbId == ElementId.InvalidElementId) continue;

                string entry = sheetNumberByViewId.TryGetValue(view.Id.Value, out string sheetNum)
                    ? sheetNum
                    : "(unplaced)";

                if (!usageByScopeBox.TryGetValue(sbId.Value, out ScopeBoxUsage usage))
                {
                    usage = new ScopeBoxUsage();
                    usageByScopeBox[sbId.Value] = usage;
                }
                usage.Entries.Add(entry);
            }

            return usageByScopeBox;
        }
    }
}
