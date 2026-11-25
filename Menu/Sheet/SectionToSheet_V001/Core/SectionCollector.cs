#nullable enable
using Autodesk.Revit.DB;
using Revit26_Plugin.AutomatedSectionPlacer.V003.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionToSheet.V001.Core
{
    /// <summary>One section found in the plan view, with where it is placed today.</summary>
    public sealed class SectionRecord
    {
        /// <summary>Packing model shared with Section Placer V003.</summary>
        public ViewInfo Info { get; init; } = null!;
        /// <summary>Sheet number the section is on now, or null when not placed.</summary>
        public string? CurrentSheetNumber { get; init; }
        /// <summary>Position of the section marker in plan (model X/Y), used for reading order.</summary>
        public XYZ? PlanAnchor { get; init; }
    }

    /// <summary>A sheet offered as a start / overflow target.</summary>
    public sealed class SheetChoice
    {
        /// <summary>Sheet id; <see cref="ElementId.InvalidElementId"/> for "New sheet".</summary>
        public ElementId Id { get; init; } = ElementId.InvalidElementId;
        /// <summary>Text shown in the list.</summary>
        public string Label { get; init; } = string.Empty;
        /// <summary>True for the "New sheet" entry.</summary>
        public bool IsNew => Id == ElementId.InvalidElementId;
        /// <inheritdoc/>
        public override string ToString() => Label;
    }

    /// <summary>
    /// Reads the plan view: section views whose markers are visible in it (same rule as
    /// Section Placer V003), titleblock types and existing sheets.
    /// </summary>
    public static class SectionCollector
    {
        private const double MmPerFoot = 304.8;

        /// <summary>
        /// Section views visible in <paramref name="plan"/>, in plan reading order (top to bottom,
        /// then left to right). Templates, non-printable views and dependent segments are excluded.
        /// </summary>
        public static List<SectionRecord> CollectSections(Document doc, View plan, Action<string> warn)
        {
            var sheetOfView = new Dictionary<ElementId, string>();
            foreach (var vp in new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>())
            {
                if (doc.GetElement(vp.SheetId) is ViewSheet s)
                    sheetOfView[vp.ViewId] = s.SheetNumber;
            }

            var sections = new FilteredElementCollector(doc, plan.Id)
                .OfCategory(BuiltInCategory.OST_Viewers)
                .WhereElementIsNotElementType()
                .ToElements()
                .OfType<ViewSection>()
                .Where(v => v.ViewType == ViewType.Section
                         && !v.IsTemplate
                         && v.CanBePrinted
                         && v.GetPrimaryViewId() == ElementId.InvalidElementId)
                .GroupBy(v => v.Id).Select(g => g.First())
                .ToList();

            var records = new List<SectionRecord>();
            foreach (var v in sections)
            {
                try
                {
                    int scale = v.Scale > 0 ? v.Scale : 1;
                    var crop = v.CropBox;
                    double wFt = crop.Max.X - crop.Min.X;
                    double hFt = crop.Max.Y - crop.Min.Y;
                    sheetOfView.TryGetValue(v.Id, out var sheetNo);

                    var info = new ViewInfo(
                        viewId: v.Id,
                        name: v.Name,
                        revitViewType: v.ViewType,
                        viewTypeLabel: "Section",
                        scale: scale,
                        widthMm: wFt * MmPerFoot / scale,
                        heightMm: hFt * MmPerFoot / scale,
                        cropWidthFeet: wFt,
                        cropHeightFeet: hFt,
                        isAlreadyPlaced: sheetNo != null,
                        isSelected: true);

                    if (!v.CropBoxActive)
                        warn($"'{v.Name}' has no active crop box — its size on sheet may be wrong.");

                    records.Add(new SectionRecord { Info = info, CurrentSheetNumber = sheetNo, PlanAnchor = v.Origin });
                }
                catch (Exception ex)
                {
                    warn($"Skipped '{v.Name}' — {ex.Message}");
                }
            }

            return SortReadingOrder(records);
        }

        /// <summary>Top-to-bottom rows (1 m tolerance), left to right inside a row; unknown anchors last.</summary>
        private static List<SectionRecord> SortReadingOrder(List<SectionRecord> records)
        {
            const double rowToleranceFt = 1000.0 / MmPerFoot;
            var known = records.Where(r => r.PlanAnchor != null).OrderByDescending(r => r.PlanAnchor!.Y).ToList();
            var result = new List<SectionRecord>();
            int i = 0;
            while (i < known.Count)
            {
                double rowTop = known[i].PlanAnchor!.Y;
                var row = known.Skip(i).TakeWhile(r => rowTop - r.PlanAnchor!.Y <= rowToleranceFt).ToList();
                result.AddRange(row.OrderBy(r => r.PlanAnchor!.X));
                i += row.Count;
            }
            result.AddRange(records.Where(r => r.PlanAnchor == null));
            return result;
        }

        /// <summary>Titleblock types with their paper size (from the type's bounding box).</summary>
        public static List<TitleblockOption> CollectTitleblocks(Document doc)
        {
            var list = new List<TitleblockOption>();
            foreach (var ts in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_TitleBlocks)
                         .WhereElementIsElementType().Cast<FamilySymbol>())
            {
                var bb = ts.get_BoundingBox(null);
                double w = bb == null ? 0 : (bb.Max.X - bb.Min.X) * MmPerFoot;
                double h = bb == null ? 0 : (bb.Max.Y - bb.Min.Y) * MmPerFoot;
                list.Add(new TitleblockOption(ts.Id, $"{ts.Family.Name} - {ts.Name}", w, h));
            }
            return list.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Existing (non-placeholder) sheets, ordered by number.</summary>
        public static List<SheetChoice> CollectSheets(Document doc)
            => new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber, StringComparer.OrdinalIgnoreCase)
                .Select(s => new SheetChoice { Id = s.Id, Label = $"{s.SheetNumber} - {s.Name}" })
                .ToList();
    }
}
