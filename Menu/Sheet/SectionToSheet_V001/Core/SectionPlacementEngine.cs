#nullable enable
using Autodesk.Revit.DB;
using Revit26_Plugin.AutomatedSectionPlacer.V003.Models;
using Revit26_Plugin.AutomatedSectionPlacer.V003.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SectionToSheet.V001.Core
{
    /// <summary>Everything the engine needs from the setup window.</summary>
    public sealed class PlacementRequest
    {
        /// <summary>Titleblock type for new sheets (paper size and id).</summary>
        public TitleblockOption NewSheetTitleblock { get; init; } = null!;
        /// <summary>First target sheet; <see cref="ElementId.InvalidElementId"/> = a new sheet.</summary>
        public ElementId StartSheetId { get; init; } = ElementId.InvalidElementId;
        /// <summary>Overflow mode.</summary>
        public OverflowMode Overflow { get; init; }
        /// <summary>Existing sheets to fill, in order, when <see cref="Overflow"/> is ExistingSheets.</summary>
        public IReadOnlyList<ElementId> OverflowSheetIds { get; init; } = Array.Empty<ElementId>();
        /// <summary>What to do with sheets that already hold views.</summary>
        public ExistingContentMode ExistingContent { get; init; }
        /// <summary>New sheet number prefix.</summary>
        public string SheetNumberPrefix { get; init; } = string.Empty;
        /// <summary>New sheet start number text (digit count = zero padding).</summary>
        public string SheetNumberStart { get; init; } = "1";
        /// <summary>New sheet name.</summary>
        public string SheetName { get; init; } = string.Empty;
        /// <summary>Layout algorithm.</summary>
        public RowFillStrategy Strategy { get; init; } = RowFillStrategy.MaxFill;
        /// <summary>Margins, mm.</summary>
        public double MarginTopMm { get; init; }
        /// <summary>Margins, mm.</summary>
        public double MarginBottomMm { get; init; }
        /// <summary>Margins, mm.</summary>
        public double MarginLeftMm { get; init; }
        /// <summary>Margins, mm.</summary>
        public double MarginRightMm { get; init; }
        /// <summary>Gap between views, mm.</summary>
        public double GapHorizontalMm { get; init; }
        /// <summary>Gap between views, mm.</summary>
        public double GapVerticalMm { get; init; }
    }

    /// <summary>Counts reported after a run.</summary>
    public sealed class PlacementResult
    {
        /// <summary>True when the transaction committed.</summary>
        public bool Succeeded { get; set; }
        /// <summary>Sections placed that were not on a sheet before.</summary>
        public int Placed { get; set; }
        /// <summary>Sections moved from another sheet.</summary>
        public int Moved { get; set; }
        /// <summary>Sections skipped (oversized, owned by others, missing).</summary>
        public int Skipped { get; set; }
        /// <summary>Sheet numbers that received sections, in order.</summary>
        public List<string> SheetsUsed { get; } = new();
    }

    /// <summary>
    /// Places sections on sheets: start sheet first, then overflow sheets, creating new sheets as
    /// needed. Already-placed sections are moved (old viewport deleted, viewport type kept).
    /// Layout per sheet uses Section Placer V003's <see cref="ReadingOrderPackingService.RepackSingleSheet"/>.
    /// The whole run is one transaction: any error rolls everything back.
    /// </summary>
    public sealed class SectionPlacementEngine
    {
        private const double MmPerFoot = 304.8;
        private const int MaxSheets = 500;

        private readonly Document _doc;
        private readonly PlacementRequest _req;
        private readonly Action<LogLevel, string> _log;
        private readonly HashSet<string> _usedNumbers;
        private int _nextNumber;
        private readonly int _numberDigits;

        /// <summary>Creates an engine for one run.</summary>
        public SectionPlacementEngine(Document doc, PlacementRequest request, Action<LogLevel, string> log)
        {
            _doc = doc;
            _req = request;
            _log = log;
            _usedNumbers = new HashSet<string>(
                new FilteredElementCollector(doc).OfClass(typeof(ViewSheet)).Cast<ViewSheet>().Select(s => s.SheetNumber),
                StringComparer.OrdinalIgnoreCase);
            string start = (request.SheetNumberStart ?? "1").Trim();
            _nextNumber = int.TryParse(start, out int n) && n >= 0 ? n : 1;
            _numberDigits = Math.Max(1, start.Length);
        }

        /// <summary>Places <paramref name="sections"/> (in the given order) and returns the counts.</summary>
        public PlacementResult Run(IReadOnlyList<ViewInfo> sections)
        {
            var result = new PlacementResult();
            var toPlace = Validate(sections, result);
            if (toPlace.Count == 0)
            {
                _log(LogLevel.Warning, "Nothing to place.");
                return result;
            }

            using var tx = new Transaction(_doc, "Section To Sheet");
            try
            {
                tx.Start();
                var oldTypes = RemoveOldViewports(toPlace);
                _doc.Regenerate();

                var queue = new List<ViewInfo>(toPlace);
                var placedNow = new List<ViewInfo>();
                foreach (var target in SheetSequence())
                {
                    var placed = FillSheet(target, queue, oldTypes, result);
                    placedNow.AddRange(placed);
                    var placedIds = new HashSet<ElementId>(placed.Select(p => p.ViewId));
                    queue = queue.Where(v => !placedIds.Contains(v.ViewId)).ToList();
                    if (queue.Count == 0) break;
                }

                if (queue.Count > 0)
                    throw new InvalidOperationException($"{queue.Count} section(s) could not be placed on any sheet.");

                result.Moved = placedNow.Count(v => v.IsAlreadyPlaced);
                result.Placed = placedNow.Count - result.Moved;
                tx.Commit();
                result.Succeeded = true;
                _log(LogLevel.Success,
                    $"Done: {result.Placed} placed, {result.Moved} moved, {result.Skipped} skipped, on {result.SheetsUsed.Count} sheet(s): {string.Join(", ", result.SheetsUsed)}.");
            }
            catch (Exception ex)
            {
                if (tx.HasStarted() && !tx.HasEnded()) tx.RollBack();
                result.Succeeded = false;
                result.Placed = result.Moved = 0;
                result.SheetsUsed.Clear();
                _log(LogLevel.Error, $"Nothing was changed (rolled back): {ex.Message}");
                if (ex is not InvalidOperationException)
                    Revit26_Plugin.Shared.Services.ToolGuard.Report(typeof(SectionPlacementEngine), ex);
            }
            return result;
        }

        // ── Validation ──────────────────────────────────────────────────────

        private List<ViewInfo> Validate(IReadOnlyList<ViewInfo> sections, PlacementResult result)
        {
            var tb = _req.NewSheetTitleblock;
            tb.ApplyMargins(_req.MarginTopMm, _req.MarginBottomMm, _req.MarginLeftMm, _req.MarginRightMm);

            var ok = new List<ViewInfo>();
            foreach (var s in sections)
            {
                if (_doc.GetElement(s.ViewId) is not View view)
                {
                    Skip(result, $"'{s.Name}' no longer exists — skipped.");
                    continue;
                }
                if (IsOwnedByOther(view.Id))
                {
                    Skip(result, $"'{s.Name}' is checked out by another user — skipped.");
                    continue;
                }
                var vp = FindViewport(view.Id);
                if (vp != null && IsOwnedByOther(vp.SheetId))
                {
                    Skip(result, $"'{s.Name}' is on a sheet checked out by another user — skipped.");
                    continue;
                }
                if (s.WidthMm <= 0 || s.HeightMm <= 0)
                {
                    Skip(result, $"'{s.Name}' has no size — skipped.");
                    continue;
                }
                if (s.WidthMm > tb.UsableWidthMm || s.HeightMm > tb.UsableHeightMm)
                {
                    Skip(result, $"'{s.Name}' ({s.WidthMm:0} x {s.HeightMm:0} mm) is larger than an empty sheet " +
                                 $"({tb.UsableWidthMm:0} x {tb.UsableHeightMm:0} mm usable) — skipped. Change its scale or crop.");
                    continue;
                }
                ok.Add(s);
            }
            return ok;
        }

        private void Skip(PlacementResult result, string message)
        {
            result.Skipped++;
            _log(LogLevel.Warning, message);
        }

        private bool IsOwnedByOther(ElementId id)
            => _doc.IsWorkshared && id != ElementId.InvalidElementId
               && WorksharingUtils.GetCheckoutStatus(_doc, id) == CheckoutStatus.OwnedByOtherUser;

        private Viewport? FindViewport(ElementId viewId)
            => new FilteredElementCollector(_doc).OfClass(typeof(Viewport)).Cast<Viewport>()
                .FirstOrDefault(v => v.ViewId == viewId);

        /// <summary>Deletes the current viewport of every already-placed section; returns view → old viewport type.</summary>
        private Dictionary<ElementId, ElementId> RemoveOldViewports(List<ViewInfo> toPlace)
        {
            var types = new Dictionary<ElementId, ElementId>();
            foreach (var s in toPlace.Where(v => v.IsAlreadyPlaced))
            {
                var vp = FindViewport(s.ViewId);
                if (vp == null) continue;
                string from = (_doc.GetElement(vp.SheetId) as ViewSheet)?.SheetNumber ?? "?";
                types[s.ViewId] = vp.GetTypeId();
                _doc.Delete(vp.Id);
                _log(LogLevel.Info, $"'{s.Name}' taken off sheet {from} (will be moved).");
            }
            return types;
        }

        // ── Sheet sequence ──────────────────────────────────────────────────

        /// <summary>Start sheet, then picked overflow sheets, then new sheets (Invalid id = new).</summary>
        private IEnumerable<ElementId> SheetSequence()
        {
            yield return _req.StartSheetId;
            if (_req.Overflow == OverflowMode.ExistingSheets)
            {
                foreach (var id in _req.OverflowSheetIds.Where(i => i != _req.StartSheetId).Distinct())
                    yield return id;
                if (_req.OverflowSheetIds.Count > 0)
                    _log(LogLevel.Warning, "Picked sheets are full — continuing on new sheets.");
            }
            for (int i = 0; i < MaxSheets; i++)
                yield return ElementId.InvalidElementId;
        }

        private ViewSheet CreateSheet()
        {
            var sheet = ViewSheet.Create(_doc, _req.NewSheetTitleblock.FamilySymbolId);
            string number;
            do
            {
                number = _req.SheetNumberPrefix + _nextNumber.ToString().PadLeft(_numberDigits, '0');
                _nextNumber++;
            } while (_usedNumbers.Contains(number));
            _usedNumbers.Add(number);
            sheet.SheetNumber = number;
            if (!string.IsNullOrWhiteSpace(_req.SheetName)) sheet.Name = _req.SheetName;
            _doc.Regenerate();
            _log(LogLevel.Info, $"Created sheet {number} \"{sheet.Name}\".");
            return sheet;
        }

        // ── One sheet ───────────────────────────────────────────────────────

        private sealed class Frame
        {
            public TitleblockOption Titleblock = null!;
            public XYZ Origin = XYZ.Zero; // usable-area top-left, sheet space
        }

        /// <summary>Usable area of a sheet from its titleblock instance, pushed down by <paramref name="extraTopMm"/>.</summary>
        private Frame? BuildFrame(ViewSheet sheet, double extraTopMm)
        {
            var tbInst = new FilteredElementCollector(_doc, sheet.Id).OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType().FirstOrDefault();
            var bb = tbInst?.get_BoundingBox(sheet);
            if (bb == null) return null;

            var tb = new TitleblockOption(tbInst!.GetTypeId(), sheet.SheetNumber,
                (bb.Max.X - bb.Min.X) * MmPerFoot, (bb.Max.Y - bb.Min.Y) * MmPerFoot);
            double top = _req.MarginTopMm + extraTopMm;
            tb.ApplyMargins(top, _req.MarginBottomMm, _req.MarginLeftMm, _req.MarginRightMm);
            return new Frame
            {
                Titleblock = tb,
                Origin = new XYZ(bb.Min.X + _req.MarginLeftMm / MmPerFoot, bb.Max.Y - top / MmPerFoot, 0)
            };
        }

        /// <summary>Places as many queued sections on the target as fit; returns the ones placed.</summary>
        private List<ViewInfo> FillSheet(ElementId targetId, List<ViewInfo> queue,
            Dictionary<ElementId, ElementId> oldTypes, PlacementResult result)
        {
            bool isNew = targetId == ElementId.InvalidElementId;
            ViewSheet sheet;
            if (isNew)
            {
                sheet = CreateSheet();
            }
            else
            {
                if (_doc.GetElement(targetId) is not ViewSheet existing)
                {
                    _log(LogLevel.Warning, "A picked sheet no longer exists — skipped.");
                    return new List<ViewInfo>();
                }
                if (IsOwnedByOther(existing.Id))
                {
                    _log(LogLevel.Warning, $"Sheet {existing.SheetNumber} is checked out by another user — skipped.");
                    return new List<ViewInfo>();
                }
                sheet = existing;
            }

            var existingVps = new FilteredElementCollector(_doc, sheet.Id).OfClass(typeof(Viewport)).Cast<Viewport>().ToList();
            var mode = existingVps.Count == 0 ? ExistingContentMode.StackBelow : _req.ExistingContent;

            if (mode == ExistingContentMode.SkipSheet)
            {
                _log(LogLevel.Info, $"Sheet {sheet.SheetNumber} already has views — skipped (setting: skip to next sheet).");
                return new List<ViewInfo>();
            }
            if (mode == ExistingContentMode.RearrangeAll && existingVps.Any(v => v.Pinned))
            {
                _log(LogLevel.Warning, $"Sheet {sheet.SheetNumber} has pinned views — stacking below them instead of rearranging.");
                mode = ExistingContentMode.StackBelow;
            }

            var gaps = new ViewGroupGapSettings(ViewType.Section, "Section")
            {
                HorizontalGapMm = _req.GapHorizontalMm,
                VerticalGapMm = _req.GapVerticalMm
            };

            if (mode == ExistingContentMode.RearrangeAll)
            {
                var frame = BuildFrame(sheet, 0);
                if (frame == null) return NoTitleblock(sheet, isNew);

                var existingInfos = existingVps.Select(ToInfo).Where(i => i != null).Cast<ViewInfo>().ToList();
                var candidates = existingInfos.Concat(queue).ToList();
                var placed = Pack(candidates, frame.Titleblock, gaps);
                var placedIds = new HashSet<ElementId>(placed.Select(p => p.View.ViewId));
                if (existingInfos.All(e => placedIds.Contains(e.ViewId)))
                {
                    var vpByView = existingVps.ToDictionary(v => v.ViewId);
                    var news = new List<ViewInfo>();
                    foreach (var (view, x, y) in placed)
                    {
                        var center = CenterOf(frame, view, x, y);
                        if (vpByView.TryGetValue(view.ViewId, out var vp)) MoveTo(vp, center);
                        else { CreateAt(sheet, view, center, oldTypes); news.Add(view); }
                    }
                    _log(LogLevel.Info, $"Sheet {sheet.SheetNumber}: rearranged {existingInfos.Count} existing view(s) and added {news.Count} section(s).");
                    if (news.Count > 0) result.SheetsUsed.Add(sheet.SheetNumber);
                    return news;
                }
                _log(LogLevel.Warning, $"Sheet {sheet.SheetNumber}: its existing views don't all fit when rearranged — stacking below them instead.");
                mode = ExistingContentMode.StackBelow;
            }

            // Stack below (or empty sheet)
            var baseFrame = BuildFrame(sheet, 0);
            if (baseFrame == null) return NoTitleblock(sheet, isNew);
            double extra = 0;
            if (existingVps.Count > 0)
            {
                double lowestY = existingVps.Select(v => v.GetBoxOutline().MinimumPoint.Y).Min();
                extra = Math.Max(0, (baseFrame.Origin.Y - lowestY) * MmPerFoot + _req.GapVerticalMm);
            }
            var stackFrame = extra > 0 ? BuildFrame(sheet, extra)! : baseFrame;
            if (stackFrame.Titleblock.UsableHeightMm <= 0)
            {
                _log(LogLevel.Info, $"Sheet {sheet.SheetNumber} has no free space below its views — going to the next sheet.");
                return new List<ViewInfo>();
            }

            var stackPlaced = Pack(queue, stackFrame.Titleblock, gaps);
            if (stackPlaced.Count == 0)
            {
                if (isNew)
                    throw new InvalidOperationException($"Could not fit any section on the new sheet {sheet.SheetNumber}.");
                _log(LogLevel.Info, $"No room on sheet {sheet.SheetNumber} — going to the next sheet.");
                return new List<ViewInfo>();
            }

            foreach (var (view, x, y) in stackPlaced)
                CreateAt(sheet, view, CenterOf(stackFrame, view, x, y), oldTypes);

            _log(LogLevel.Info, $"Sheet {sheet.SheetNumber}: {stackPlaced.Count} section(s) placed" + (extra > 0 ? " below its existing views." : "."));
            result.SheetsUsed.Add(sheet.SheetNumber);
            return stackPlaced.Select(p => p.View).ToList();
        }

        /// <summary>
        /// Runs Section Placer's packer and keeps only placements fully inside the usable area,
        /// so a view never lands past the sheet edge; dropped views go to the next sheet.
        /// </summary>
        private List<(ViewInfo View, double OffsetXMm, double OffsetYMm)> Pack(
            List<ViewInfo> candidates, TitleblockOption tb, ViewGroupGapSettings gaps)
        {
            const double tol = 0.5; // mm
            var (placed, _) = ReadingOrderPackingService.RepackSingleSheet(candidates, tb, gaps, _req.Strategy);
            return placed.Where(p => p.OffsetXMm >= -tol && p.OffsetYMm >= -tol
                                     && p.OffsetXMm + p.View.WidthMm <= tb.UsableWidthMm + tol
                                     && p.OffsetYMm + p.View.HeightMm <= tb.UsableHeightMm + tol)
                         .ToList();
        }

        private List<ViewInfo> NoTitleblock(ViewSheet sheet, bool isNew)
        {
            if (isNew)
                throw new InvalidOperationException($"New sheet {sheet.SheetNumber} has no titleblock.");
            _log(LogLevel.Warning, $"Sheet {sheet.SheetNumber} has no titleblock — skipped.");
            return new List<ViewInfo>();
        }

        /// <summary>Packing model for a viewport already on the sheet (size = its box outline).</summary>
        private ViewInfo? ToInfo(Viewport vp)
        {
            try
            {
                var o = vp.GetBoxOutline();
                var view = _doc.GetElement(vp.ViewId) as View;
                return new ViewInfo(vp.ViewId, view?.Name ?? "?", view?.ViewType ?? ViewType.Undefined, "Existing",
                    view != null && view.Scale > 0 ? view.Scale : 1,
                    (o.MaximumPoint.X - o.MinimumPoint.X) * MmPerFoot,
                    (o.MaximumPoint.Y - o.MinimumPoint.Y) * MmPerFoot);
            }
            catch
            {
                return null;
            }
        }

        private static XYZ CenterOf(Frame frame, ViewInfo view, double offsetXMm, double offsetYMm)
            => new XYZ(frame.Origin.X + (offsetXMm + view.WidthMm / 2.0) / MmPerFoot,
                       frame.Origin.Y - (offsetYMm + view.HeightMm / 2.0) / MmPerFoot, 0);

        private void MoveTo(Viewport vp, XYZ center)
        {
            var o = vp.GetBoxOutline();
            var current = (o.MinimumPoint + o.MaximumPoint) / 2.0;
            var delta = center - current;
            if (!delta.IsZeroLength()) ElementTransformUtils.MoveElement(_doc, vp.Id, delta);
        }

        private void CreateAt(ViewSheet sheet, ViewInfo view, XYZ center, Dictionary<ElementId, ElementId> oldTypes)
        {
            if (!Viewport.CanAddViewToSheet(_doc, sheet.Id, view.ViewId))
                throw new InvalidOperationException($"'{view.Name}' cannot be added to sheet {sheet.SheetNumber}.");
            var vp = Viewport.Create(_doc, sheet.Id, view.ViewId, XYZ.Zero);
            if (oldTypes.TryGetValue(view.ViewId, out var typeId) && typeId != ElementId.InvalidElementId && vp.GetTypeId() != typeId)
                vp.ChangeTypeId(typeId);
            _doc.Regenerate();
            MoveTo(vp, center);
            _log(LogLevel.Info, $"'{view.Name}' → sheet {sheet.SheetNumber}" + (view.IsAlreadyPlaced ? " (moved)." : "."));
        }
    }
}
