using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Models;

namespace Revit26_Plugin.RefSectionHeadPlacer.V001.Core.Services
{
    /// <summary>
    /// Loads document data for the grids/selectors:
    ///   • Plan views (host)        -> Grid 1
    ///   • Link instances (host)    -> "Linked Files" checkbox list
    ///   • Section types (host)     -> "Section type" ComboBox (via GeometryHelper)
    ///   • Drafting views (host)    -> Grid 3 mapping targets
    ///   • Element types            -> Grid 2:
    ///         Roofs = HOST document, scoped to selected plan views
    ///         Doors / Plumbing / Walls = EACH selected linked file, scoped to
    ///           500mm of a selected host roof's bounding box. Grouped PER LINK —
    ///           two links both having e.g. "Floor Drain 100mm" produce TWO
    ///           separate Grid 2 rows, never merged (confirmed with Rafi).
    /// </summary>
    public class ElementCollectorService
    {
        private static readonly BuiltInCategory[] HostCategories = { BuiltInCategory.OST_Roofs };

        private static readonly BuiltInCategory[] LinkCategories =
        {
            BuiltInCategory.OST_Doors,
            BuiltInCategory.OST_Walls,
            BuiltInCategory.OST_PlumbingFixtures
        };

        private const string HostSourceLabel = "Host";
        private const double BufferMm = 500.0;

        private readonly Document _hostDoc;
        private readonly double _bufferFeet;

        public ElementCollectorService(Document hostDoc)
        {
            _hostDoc = hostDoc;
            _bufferFeet = UnitUtils.ConvertToInternalUnits(BufferMm, UnitTypeId.Millimeters);
        }

        public List<PlanViewRow> LoadPlanViews()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate)
                .Select(v => new PlanViewRow(v, v.ViewType.ToString(), v.GenLevel?.Name ?? "-"))
                .ToList();
        }

        public List<DraftingViewOption> LoadDraftingViews()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .Where(v => !v.IsTemplate)
                .OrderBy(v => v.Name)
                .Select(v => new DraftingViewOption(v))
                .ToList();
        }

        /// <summary>All loaded Revit links, for the "Linked Files" checkbox list.</summary>
        public List<LinkRow> LoadLinkInstances()
        {
            return new FilteredElementCollector(_hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Select(li => new LinkRow(li))
                .Where(r => r.LinkDocument != null) // skip unloaded links
                .OrderBy(r => r.Name)
                .ToList();
        }

        /// <summary>
        /// Builds Grid 2. Host roofs are scoped to the selected plan views; for EACH
        /// checked link, doors/plumbing/walls within 500mm of a selected roof's
        /// bounding box are collected into that link's OWN rows (never merged).
        /// </summary>
        public List<ElementTypeRow> CollectElementTypes(
            IEnumerable<PlanViewRow> selectedViews,
            IEnumerable<LinkRow> selectedLinks)
        {
            var rows = new List<ElementTypeRow>();
            var identity = Transform.Identity;

            // ── Host categories (roofs), scoped by selected views ──
            var hostRoofElements = new List<Element>();

            foreach (var bic in HostCategories)
            {
                // De-dupe elements that appear in more than one selected view.
                var byId = new Dictionary<ElementId, Element>();
                foreach (var viewRow in selectedViews)
                    foreach (var e in Collect(_hostDoc, viewRow.RevitView.Id, bic))
                        byId[e.Id] = e;

                var elems = byId.Values.ToList();
                if (bic == BuiltInCategory.OST_Roofs) hostRoofElements.AddRange(elems);
                rows.AddRange(GroupToRows(elems, bic, HostSourceLabel, _hostDoc, identity, false));
            }

            if (hostRoofElements.Count == 0)
                return SortRows(rows);

            // Expanded roof bounding boxes (HOST coords), computed once.
            var expandedBoxes = hostRoofElements
                .Select(r => r.get_BoundingBox(null))
                .Where(b => b != null)
                .Select(b => (
                    Min: b.Min - new XYZ(_bufferFeet, _bufferFeet, _bufferFeet),
                    Max: b.Max + new XYZ(_bufferFeet, _bufferFeet, _bufferFeet)))
                .ToList();

            // ── Each selected link independently — never merged across links ──
            foreach (var linkRow in selectedLinks)
            {
                if (linkRow.LinkDocument == null) continue;

                var linkDoc = linkRow.LinkDocument;
                var toHost = linkRow.Instance.GetTotalTransform();

                foreach (var bic in LinkCategories)
                {
                    var inScope = Collect(linkDoc, null, bic)
                        .Where(e =>
                        {
                            var p = GetRepresentativePointInHost(e, toHost);
                            return p != null && expandedBoxes.Any(b => IsInside(p, b.Min, b.Max));
                        })
                        .ToList();

                    rows.AddRange(GroupToRows(inScope, bic, linkRow.Name, linkDoc, toHost, true));
                }
            }

            return SortRows(rows);
        }

        // ── helpers ───────────────────────────────────────────────────

        private static FilteredElementCollector Collect(Document doc, ElementId viewId, BuiltInCategory bic)
        {
            var collector = viewId == null
                ? new FilteredElementCollector(doc)
                : new FilteredElementCollector(doc, viewId);
            return collector.OfCategory(bic).WhereElementIsNotElementType();
        }

        /// <summary>
        /// Groups elements of a SINGLE category by type name into rows. All elements
        /// share the same BuiltInCategory (bic) and source, so those are stamped on
        /// every row; the display Category name is read from the elements.
        /// </summary>
        private static IEnumerable<ElementTypeRow> GroupToRows(
            IReadOnlyCollection<Element> elements, BuiltInCategory bic, string sourceLabel,
            Document doc, Transform toHost, bool isFromLink)
        {
            var groups = new Dictionary<string, List<ElementId>>();
            string categoryName = bic.ToString();

            foreach (var e in elements)
            {
                categoryName = e.Category?.Name ?? categoryName;
                var type = GetTypeName(e);
                if (!groups.TryGetValue(type, out var ids))
                {
                    ids = new List<ElementId>();
                    groups[type] = ids;
                }
                ids.Add(e.Id); // collector never yields duplicates within one query
            }

            var cat = categoryName;
            return groups.Select(kvp =>
                new ElementTypeRow(sourceLabel, cat, kvp.Key, bic, kvp.Value, doc, toHost, isFromLink));
        }

        private static List<ElementTypeRow> SortRows(List<ElementTypeRow> rows) =>
            rows.OrderBy(r => r.SourceLabel).ThenBy(r => r.Category).ThenBy(r => r.TypeName).ToList();

        private static XYZ GetRepresentativePointInHost(Element e, Transform toHost)
        {
            XYZ localPoint = e.Location switch
            {
                LocationPoint lp => lp.Point,
                LocationCurve lc => lc.Curve.Evaluate(0.5, true),
                _ => null
            };
            return localPoint == null ? null : toHost.OfPoint(localPoint);
        }

        private static bool IsInside(XYZ point, XYZ min, XYZ max) =>
            point.X >= min.X && point.X <= max.X &&
            point.Y >= min.Y && point.Y <= max.Y &&
            point.Z >= min.Z && point.Z <= max.Z;

        private static string GetTypeName(Element elem)
        {
            var typeId = elem.GetTypeId();
            if (typeId == ElementId.InvalidElementId) return elem.Name;
            return elem.Document.GetElement(typeId)?.Name ?? elem.Name;
        }
    }
}
