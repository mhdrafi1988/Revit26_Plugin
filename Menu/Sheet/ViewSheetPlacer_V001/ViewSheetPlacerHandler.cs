using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    /// <summary>
    /// Single external-event handler for the tool. Reads sizes via a scratch
    /// sheet, packs, then creates sheets and viewports. All work is wrapped in a
    /// TransactionGroup so the whole run is one undo step; each logical operation
    /// is its own Transaction inside that group.
    /// </summary>
    public sealed class ViewSheetPlacerHandler : IExternalEventHandler
    {
        private const double MmToFt = 1.0 / 304.8;

        public PlacementRequest? Request { get; set; }

        // Cached "no title" viewport type for the current run.
        private ElementId _noTitleTypeId = ElementId.InvalidElementId;

        public string GetName() => "ViewSheetPlacer.Handler";

        public void Execute(UIApplication app)
        {
            var req = Request;
            if (req == null) return;

            Document doc = app.ActiveUIDocument.Document;
            int placed = 0, skipped = 0, failed = 0;
            _noTitleTypeId = ElementId.InvalidElementId;

            void Log(LogLevel level, string msg) =>
                req.Log?.Invoke(new LogEntry(level, msg));

            try
            {
                // Resolve which views actually get placed.
                // Already-placed + SkipAlreadyPlaced => leave in place (skip).
                // Already-placed + not skipping => move onto a new sheet.
                var toPlace = new List<ViewInfo>();
                foreach (var v in req.SelectedViews)
                {
                    if (v.IsPlaced && req.SkipAlreadyPlaced)
                    {
                        Log(LogLevel.Warning,
                            $"Skipped \"{v.ViewName}\" — already on {v.PlacedSheet}.");
                        skipped++;
                        continue;
                    }
                    toPlace.Add(v);
                }

                if (toPlace.Count == 0)
                {
                    Log(LogLevel.Warning, "No views to place.");
                    req.OnComplete?.Invoke(0, 0, 0);
                    return;
                }

                // Group.
                var groups = Group(toPlace, req.Grouping);
                Log(LogLevel.Info,
                    $"Grouped {toPlace.Count} views into {groups.Count} " +
                    $"{(req.Grouping == GroupMode.Discipline ? "disciplines" : "view types")} " +
                    $"({string.Join(", ", groups.Keys)})");

                // --- Pass 1: one rolled-back probe on a scratch sheet derives the usable
                // rectangle from the titleblock AND measures every view's viewport size with
                // a single regeneration. Rolling back leaves the model untouched, so Dry Run
                // has no side effects and a real run stays atomic under the TransactionGroup.
                if (!TryProbe(doc, req.TitleblockTypeId, toPlace, req.SheetMarginMm, req.TitleStripMm,
                        out UV usableMin, out UV usableMax,
                        out var sizes, out int oversizeSkipped, Log))
                {
                    Log(LogLevel.Error, "Could not resolve titleblock size. Aborting.");
                    req.OnComplete?.Invoke(0, 0, toPlace.Count);
                    return;
                }
                skipped += oversizeSkipped;

                double gapFt = req.ViewportGapMm * MmToFt;

                // --- Pass 2: pack per group, count sheets needed ---
                var plan = new List<(string GroupKey, PackedSheet Sheet)>();
                foreach (var kv in groups)
                {
                    var items = kv.Value
                        .Where(v => sizes.ContainsKey(v.ViewId))
                        .Select(v => sizes[v.ViewId])
                        .ToList();

                    var packed = BinPacker.Pack(items, usableMin, usableMax, gapFt);
                    foreach (var ps in packed)
                        plan.Add((kv.Key, ps));
                }

                Log(LogLevel.Info, $"Estimated sheet count: {plan.Count}");

                if (req.DryRun)
                {
                    int willPlace = sizes.Count;
                    Log(LogLevel.Success,
                        $"Dry run — {willPlace} views would fill {plan.Count} sheets" +
                        (oversizeSkipped > 0 ? $" ({oversizeSkipped} skipped, too large)." : "."));
                    req.OnComplete?.Invoke(willPlace, oversizeSkipped, 0);
                    return;
                }

                // --- Pass 3: real placement ---
                using var group = new TransactionGroup(doc, "View Sheet Placer");
                group.Start();

                // Remove existing viewports for views being moved.
                var movedViewIds = new HashSet<ElementId>();
                var toMove = toPlace
                    .Where(v => v.IsPlaced && v.ExistingViewportId != ElementId.InvalidElementId)
                    .ToList();

                if (toMove.Count > 0)
                {
                    using var tRemove = new Transaction(doc, "Remove existing viewports");
                    tRemove.Start();
                    foreach (var v in toMove)
                    {
                        try
                        {
                            doc.Delete(v.ExistingViewportId);
                            movedViewIds.Add(v.ViewId);
                        }
                        catch (Exception ex)
                        {
                            Log(LogLevel.Warning,
                                $"Could not detach \"{v.ViewName}\" from {v.PlacedSheet}: {ex.Message}");
                        }
                    }
                    tRemove.Commit();
                }

                int sheetSeq = NextSequence(doc, req.SheetNamePrefix);

                foreach (var (groupKey, packedSheet) in plan)
                {
                    using var tSheet = new Transaction(doc, "Create & place sheet");
                    tSheet.Start();

                    ViewSheet sheet;
                    string sheetNumber;
                    try
                    {
                        sheet = ViewSheet.Create(doc, req.TitleblockTypeId);

                        // Find a free number, skipping any already taken by another tool/run.
                        int attempts = 0;
                        while (true)
                        {
                            sheetNumber = $"{req.SheetNamePrefix}{sheetSeq:000}";
                            sheetSeq++;
                            try
                            {
                                sheet.SheetNumber = sheetNumber;
                                break;
                            }
                            catch (Exception) when (attempts++ < 50)
                            {
                                // Number in use — try the next one.
                            }
                        }

                        // Overflow sheets within one group share this name (distinct numbers).
                        sheet.Name = groupKey;
                        Log(LogLevel.Info, $"Created sheet {sheetNumber} ({groupKey})");
                    }
                    catch (Exception ex)
                    {
                        Log(LogLevel.Error, $"Failed to create sheet: {ex.Message}");
                        failed += packedSheet.Items.Count;
                        tSheet.RollBack();
                        continue;
                    }

                    foreach (var item in packedSheet.Items)
                    {
                        try
                        {
                            if (!Viewport.CanAddViewToSheet(doc, sheet.Id, item.ViewId))
                            {
                                Log(LogLevel.Warning,
                                    $"Skipped \"{item.ViewName}\" — cannot be added to a sheet.");
                                skipped++;
                                continue;
                            }

                            var vp = Viewport.Create(doc, sheet.Id, item.ViewId, XYZ.Zero);
                            vp.SetBoxCenter(item.TargetCenter);

                            if (!req.ShowViewportTitles)
                            {
                                ElementId noTitle = EnsureNoTitleType(doc, vp, Log);
                                if (noTitle != ElementId.InvalidElementId)
                                    vp.ChangeTypeId(noTitle);
                            }

                            if (movedViewIds.Contains(item.ViewId))
                                Log(LogLevel.Info,
                                    $"\"{item.ViewName}\" moved onto {sheetNumber}");
                            else
                                Log(LogLevel.Info,
                                    $"Placed \"{item.ViewName}\" on {sheetNumber}");

                            placed++;
                        }
                        catch (Exception ex)
                        {
                            Log(LogLevel.Error,
                                $"Failed to place \"{item.ViewName}\": {ex.Message}");
                            failed++;
                        }
                    }

                    tSheet.Commit();
                }

                group.Assimilate();

                Log(LogLevel.Success,
                    $"Run complete — {placed} placed | {skipped} skipped | {failed} failed");
            }
            catch (Exception ex)
            {
                Log(LogLevel.Error, $"Unhandled error: {ex.Message}");
            }
            finally
            {
                req.OnComplete?.Invoke(placed, skipped, failed);
            }
        }

        /// <summary>
        /// Returns a viewport type whose title is hidden (VIEWPORT_ATTR_SHOW_LABEL = 0).
        /// Reuses an existing such type, else duplicates the given viewport's type once.
        /// NOTE: the resulting type is applied to every placed viewport regardless of its
        /// original type, so any type-driven graphics (e.g. boundary line weight) follow it.
        /// Must be called inside an open transaction.
        /// </summary>
        private ElementId EnsureNoTitleType(Document doc, Viewport vp, Action<LogLevel, string> log)
        {
            if (_noTitleTypeId != ElementId.InvalidElementId) return _noTitleTypeId;

            // Any existing viewport type already set to "no title".
            var types = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Viewports)
                .WhereElementIsElementType()
                .Cast<ElementType>();

            foreach (var et in types)
            {
                var p = et.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);
                if (p != null && p.AsInteger() == 0)
                {
                    _noTitleTypeId = et.Id;
                    return _noTitleTypeId;
                }
            }

            // None found: duplicate the viewport's current type and hide its title.
            try
            {
                if (doc.GetElement(vp.GetTypeId()) is ElementType current)
                {
                    var dup = current.Duplicate("No Title (VSP)");
                    var p = dup.get_Parameter(BuiltInParameter.VIEWPORT_ATTR_SHOW_LABEL);
                    p?.Set(0);
                    _noTitleTypeId = dup.Id;
                    return _noTitleTypeId;
                }
            }
            catch (Exception ex)
            {
                log(LogLevel.Warning, $"Could not create a no-title viewport type: {ex.Message}");
            }

            return ElementId.InvalidElementId;
        }

        // ---- helpers ----------------------------------------------------------

        private static SortedDictionary<string, List<ViewInfo>> Group(
            IEnumerable<ViewInfo> views, GroupMode mode)
        {
            var result = new SortedDictionary<string, List<ViewInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var v in views)
            {
                string key = mode == GroupMode.Discipline
                    ? (string.IsNullOrWhiteSpace(v.Discipline) ? "Unassigned" : v.Discipline)
                    : (string.IsNullOrWhiteSpace(v.ViewType) ? "Other" : v.ViewType);

                if (!result.TryGetValue(key, out var list))
                {
                    list = new List<ViewInfo>();
                    result[key] = list;
                }
                list.Add(v);
            }
            return result;
        }

        /// <summary>
        /// One rolled-back probe on a scratch sheet: derives the usable rectangle
        /// from the titleblock and measures every view's on-sheet viewport size with a
        /// single regeneration. The transaction is rolled back, so the model is left
        /// unchanged while the measured values (already copied to primitives) are returned.
        /// </summary>
        private static bool TryProbe(
            Document doc, ElementId titleblockTypeId, IReadOnlyList<ViewInfo> views,
            double marginMm, double titleStripMm,
            out UV usableMin, out UV usableMax,
            out Dictionary<ElementId, PackItem> sizes, out int oversizeSkipped,
            Action<LogLevel, string> log)
        {
            usableMin = UV.Zero;
            usableMax = UV.Zero;
            sizes = new Dictionary<ElementId, PackItem>();
            oversizeSkipped = 0;

            using var t = new Transaction(doc, "Probe titleblock and view sizes");
            t.Start();

            ViewSheet scratch;
            try
            {
                scratch = ViewSheet.Create(doc, titleblockTypeId);
            }
            catch (Exception ex)
            {
                log(LogLevel.Error, $"Could not create scratch sheet: {ex.Message}");
                t.RollBack();
                return false;
            }

            // One measurement viewport per not-yet-placed view; already-placed views are
            // read from their live viewports (no creation needed).
            var created = new List<(ElementId VpId, ViewInfo View)>();
            var existingReads = new List<ViewInfo>();

            foreach (var v in views)
            {
                if (v.ExistingViewportId != ElementId.InvalidElementId &&
                    doc.GetElement(v.ExistingViewportId) is Viewport)
                {
                    existingReads.Add(v);
                    continue;
                }
                if (!Viewport.CanAddViewToSheet(doc, scratch.Id, v.ViewId))
                    continue; // cannot be sheeted; silently omitted
                var vp = Viewport.Create(doc, scratch.Id, v.ViewId, XYZ.Zero);
                created.Add((vp.Id, v));
            }

            doc.Regenerate(); // single regeneration covers all measurement viewports

            // Usable rectangle from the titleblock (post-regen), with fallback.
            if (!TryUsableRect(doc, scratch, marginMm, titleStripMm, out usableMin, out usableMax))
            {
                t.RollBack();
                return false;
            }
            double usableW = usableMax.U - usableMin.U;
            double usableH = usableMax.V - usableMin.V;

            // Local copies — out parameters cannot be captured by a local function
            // (CS8175), so Classify closes over these instead and we assign the
            // out params back once, right before returning.
            var localSizes = new Dictionary<ElementId, PackItem>();
            int localOversizeSkipped = 0;

            void Classify(ViewInfo v, Outline o)
            {
                double w = o.MaximumPoint.X - o.MinimumPoint.X;
                double h = o.MaximumPoint.Y - o.MinimumPoint.Y;
                if (w > usableW || h > usableH)
                {
                    log(LogLevel.Warning, $"Skipped \"{v.ViewName}\" — larger than the sheet area.");
                    localOversizeSkipped++;
                    return;
                }
                localSizes[v.ViewId] = new PackItem
                {
                    ViewId = v.ViewId, ViewName = v.ViewName, Width = w, Height = h
                };
            }

            foreach (var v in existingReads)
                if (doc.GetElement(v.ExistingViewportId) is Viewport evp)
                    Classify(v, evp.GetBoxOutline());

            foreach (var (vpId, v) in created)
                if (doc.GetElement(vpId) is Viewport cvp)
                    Classify(v, cvp.GetBoxOutline());

            sizes = localSizes;
            oversizeSkipped = localOversizeSkipped;

            t.RollBack(); // discard scratch sheet + all measurement viewports
            return true;
        }

        /// <summary>Usable rectangle = titleblock bbox inset by margin, right strip reserved.</summary>
        private static bool TryUsableRect(
            Document doc, ViewSheet sheet, double marginMm, double titleStripMm,
            out UV usableMin, out UV usableMax)
        {
            usableMin = UV.Zero; usableMax = UV.Zero;

            var tb = new FilteredElementCollector(doc, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .FirstElement();

            double margin = marginMm * MmToFt;
            double strip = titleStripMm * MmToFt;

            BoundingBoxXYZ? bb = tb?.get_BoundingBox(sheet);
            if (bb != null)
            {
                usableMin = new UV(bb.Min.X + margin, bb.Min.Y + margin);
                usableMax = new UV(bb.Max.X - margin - strip, bb.Max.Y - margin);
            }
            else
            {
                // Fallback: read the titleblock's own sheet-size parameters.
                double w = ReadLength(tb, BuiltInParameter.SHEET_WIDTH);
                double h = ReadLength(tb, BuiltInParameter.SHEET_HEIGHT);
                usableMin = new UV(margin, margin);
                usableMax = new UV(w - margin - strip, h - margin);
            }

            return usableMax.U > usableMin.U && usableMax.V > usableMin.V;
        }

        private static double ReadLength(Element? e, BuiltInParameter bip)
        {
            var p = e?.get_Parameter(bip);
            return p != null ? p.AsDouble() : 0.0;
        }

        /// <summary>Next free numeric suffix for the given prefix.</summary>
        private static int NextSequence(Document doc, string prefix)
        {
            int max = 0;
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>();

            foreach (var s in sheets)
            {
                string num = s.SheetNumber ?? string.Empty;
                if (!string.IsNullOrEmpty(prefix) &&
                    num.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string tail = num.Substring(prefix.Length);
                    if (int.TryParse(tail, out int n) && n > max) max = n;
                }
            }
            return max + 1;
        }
    }
}
