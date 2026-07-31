using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Services;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.Infrastructure.ExternalEvents
{
    /// <summary>
    /// Requests this tool's orchestrator handler can execute. Set on the
    /// handler by the ViewModel immediately before calling ExternalEvent.Raise() —
    /// per Revit API threading rules, only one Execute() runs at a time, so
    /// there is no race between setting Request and it being read.
    /// </summary>
    public enum SmartViewToSheetPlacerRequest
    {
        LoadViews,
        PlaceViews,
        OpenSheets
    }

    /// <summary>
    /// Single shared orchestrator for all Revit-API-touching actions in this
    /// tool (per our orchestrator-pattern convention: one tool window with
    /// several distinct Revit-side actions uses one handler routed by an
    /// enum, instead of a separate IExternalEventHandler/ExternalEvent pair
    /// per action). Covers:
    ///   - LoadViews:   read all project views + titleblock types (Stage 1)
    ///   - PlaceViews:  create sheets + place viewports (Stage 3)
    ///   - OpenSheets:  set ActiveView to user-selected created sheets (Stage 4)
    /// Stage 2 (packing) is pure calculation and does not touch Revit, so it
    /// has no case here — see GreedyRowPackingService.
    /// V204: moved from Handlers/ to Infrastructure/ExternalEvents/, matching
    /// the project's vertical-slice folder convention.
    /// </summary>
    public class SmartViewToSheetPlacerHandler : IExternalEventHandler
    {
        private readonly UIDocument _uiDoc;

        public SmartViewToSheetPlacerRequest Request { get; set; }

        // ---- Inputs (set by ViewModel before Raise()) ----
        public double MarginTopMm { get; set; }
        public double MarginBottomMm { get; set; }
        public double MarginLeftMm { get; set; }
        public double MarginRightMm { get; set; }
        public List<SheetGroup> SheetsToPlace { get; set; } = new();
        public ElementId? TitleblockFamilySymbolId { get; set; }
        public List<ElementId> SheetIdsToOpen { get; set; } = new();

        // ---- Outputs (read by ViewModel after the handler signals completion) ----
        public List<ViewInfo> LoadedViews { get; private set; } = new();
        public List<TitleblockOption> LoadedTitleblocks { get; private set; } = new();
        public List<LogEntry> Logs { get; } = new();
        public bool LastRunSucceeded { get; private set; }

        /// <summary>Views successfully placed onto a sheet.</summary>
        public int PlacedCount { get; private set; }

        /// <summary>Views silently skipped (already placed elsewhere, view no longer exists, etc.) — Warning-only, no dialog.</summary>
        public int SkippedCount { get; private set; }

        /// <summary>
        /// V204 fix: previously this counted failed *sheets* (one increment per
        /// sheet whose ViewSheet.Create/setup threw), while the Stage 5 summary
        /// line displayed it as failed *views*, understating true view failures
        /// whenever a failed sheet had more than one queued placement. Now counts
        /// every placement that failed to place, whether the whole sheet failed
        /// or a single viewport failed independently.
        /// </summary>
        public int FailedCount { get; private set; }

        /// <summary>Sheets that failed to create entirely (0 = normal run). Reported separately from FailedCount (views) in the Stage 5 summary.</summary>
        public int FailedSheetCount { get; private set; }

        /// <summary>Raised on the UI thread after Execute() completes, so the
        /// ViewModel can safely read outputs and refresh bound collections.</summary>
        public event Action? RequestCompleted;

        public SmartViewToSheetPlacerHandler(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                switch (Request)
                {
                    case SmartViewToSheetPlacerRequest.LoadViews:
                        ExecuteLoadViews();
                        break;
                    case SmartViewToSheetPlacerRequest.PlaceViews:
                        ExecutePlaceViews();
                        break;
                    case SmartViewToSheetPlacerRequest.OpenSheets:
                        ExecuteOpenSheets();
                        break;
                }
            }
            catch (Exception ex)
            {
                Logs.Add(new LogEntry(LogLevel.Error, $"Unhandled error in {Request}: {ex.Message}"));
                LastRunSucceeded = false;
            }
            finally
            {
                RequestCompleted?.Invoke();
            }
        }

        public string GetName() => "SmartViewToSheetPlacer Orchestrator";

        // ─────────────────────────────────────────────────────────────
        // LOAD VIEWS (Stage 1)
        // ─────────────────────────────────────────────────────────────
        private void ExecuteLoadViews()
        {
            Logs.Add(new LogEntry(LogLevel.Info, "LoadViews: starting collection of project views."));
            var doc = _uiDoc.Document;

            LoadedViews = new List<ViewInfo>();
            LoadedTitleblocks = new List<TitleblockOption>();

            var views = new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate && v.CanBePrinted)
                .ToList();

            Logs.Add(new LogEntry(LogLevel.Info, $"LoadViews: {views.Count} candidate views collected (excluding templates, non-printable)."));

            foreach (var v in views)
            {
                try
                {
                    var (widthMm, heightMm) = ComputeSizeOnSheetMm(v);
                    var info = new ViewInfo(
                        viewId: v.Id,
                        name: v.Name,
                        revitViewType: v.ViewType,
                        viewTypeLabel: ViewTypeLabelHelper.Label(v.ViewType),
                        scale: SafeScale(v),
                        widthMm: widthMm,
                        heightMm: heightMm);
                    LoadedViews.Add(info);
                }
                catch (Exception ex)
                {
                    Logs.Add(new LogEntry(LogLevel.Warning, $"LoadViews: skipped view '{v.Name}' — {ex.Message}"));
                }
            }

            Logs.Add(new LogEntry(LogLevel.Info, $"LoadViews: {LoadedViews.Count} views ready for selection."));

            var titleblockSymbols = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .ToList();

            foreach (var ts in titleblockSymbols)
            {
                try
                {
                    var bbox = ts.get_BoundingBox(null);
                    double wMm = 0, hMm = 0;
                    if (bbox != null)
                    {
                        wMm = FeetToMm(bbox.Max.X - bbox.Min.X);
                        hMm = FeetToMm(bbox.Max.Y - bbox.Min.Y);
                    }
                    string name = $"{ts.Family.Name} - {ts.Name}";
                    LoadedTitleblocks.Add(new TitleblockOption(ts.Id, name, wMm, hMm));
                }
                catch (Exception ex)
                {
                    Logs.Add(new LogEntry(LogLevel.Warning, $"LoadViews: skipped titleblock '{ts.Name}' — {ex.Message}"));
                }
            }

            Logs.Add(new LogEntry(LogLevel.Info, $"LoadViews: {LoadedTitleblocks.Count} titleblock types loaded."));
            LastRunSucceeded = true;
        }

        // ─────────────────────────────────────────────────────────────
        // PLACE VIEWS (Stage 3) — single Transaction for the entire run
        // ─────────────────────────────────────────────────────────────
        private void ExecutePlaceViews()
        {
            var doc = _uiDoc.Document;
            PlacedCount = 0;
            SkippedCount = 0;
            FailedCount = 0;
            FailedSheetCount = 0;

            if (TitleblockFamilySymbolId == null)
            {
                Logs.Add(new LogEntry(LogLevel.Error, "PlaceViews: no titleblock selected. Aborting."));
                LastRunSucceeded = false;
                return;
            }

            Logs.Add(new LogEntry(LogLevel.Info, $"PlaceViews: starting placement of {SheetsToPlace.Count} sheet(s)."));

            using var tx = new Transaction(doc, "Smart View To Sheet Placer - Place Views");
            try
            {
                tx.Start();

                var titleblockSymbol = doc.GetElement(TitleblockFamilySymbolId) as FamilySymbol;
                if (titleblockSymbol != null && !titleblockSymbol.IsActive)
                    titleblockSymbol.Activate();

                foreach (var sheet in SheetsToPlace)
                {
                    try
                    {
                        var newSheet = ViewSheet.Create(doc, TitleblockFamilySymbolId);
                        newSheet.Name = sheet.GeneratedName;
                        // Sheet Number auto-increments by Revit's own numbering rules
                        // when left as the default assigned value; if a specific
                        // scheme is required, override newSheet.SheetNumber here
                        // using the last-used project sheet number + 1.

                        sheet.CreatedSheetId = newSheet.Id;
                        sheet.AssignedSheetNumber = newSheet.SheetNumber;

                        Logs.Add(new LogEntry(LogLevel.Info,
                            $"PlaceViews: created sheet {newSheet.SheetNumber} \"{sheet.GeneratedName}\"."));

                        // V204 fix: query the REAL titleblock instance bounding box
                        // on this specific sheet, instead of assuming (0,0) is its
                        // corner — see GetTitleblockBoundingBox and PlaceSingleViewport
                        // for the full explanation of why this was the root cause of
                        // views landing entirely outside the titleblock border.
                        var titleblockBBox = GetTitleblockBoundingBox(doc, newSheet);
                        XYZ? titleblockTopLeft = null;
                        if (titleblockBBox != null)
                        {
                            // Sheet space: Y increases upward, so the titleblock's
                            // visual TOP edge is at bbox.Max.Y, and LEFT edge at bbox.Min.X.
                            titleblockTopLeft = new XYZ(titleblockBBox.Min.X, titleblockBBox.Max.Y, 0);
                        }
                        else
                        {
                            Logs.Add(new LogEntry(LogLevel.Warning,
                                $"PlaceViews: could not find titleblock instance on sheet {newSheet.SheetNumber} — falling back to sheet origin (0,0), placement may be incorrect."));
                        }

                        foreach (var placement in sheet.Placements)
                        {
                            PlaceSingleViewport(doc, newSheet, placement, titleblockTopLeft);
                        }
                    }
                    catch (Exception ex)
                    {
                        // V204 fix: sheet creation itself failed — every queued
                        // placement on this sheet counts as a failed view, not
                        // a single generic failure, so FailedCount matches what
                        // the Stage 5 summary claims ("Z failed" = views).
                        FailedSheetCount++;
                        FailedCount += sheet.Placements.Count;
                        Logs.Add(new LogEntry(LogLevel.Error, $"PlaceViews: failed to create sheet for \"{sheet.GeneratedName}\" — {ex.Message} ({sheet.Placements.Count} view(s) not placed)."));
                    }
                }

                tx.Commit();
                LastRunSucceeded = true;
                Logs.Add(new LogEntry(LogLevel.Success,
                    $"PlaceViews complete: {SheetsToPlace.Count} sheet(s) attempted ({FailedSheetCount} failed) | {PlacedCount} view(s) placed | {SkippedCount} skipped | {FailedCount} failed."));
            }
            catch (Exception ex)
            {
                if (tx.HasStarted() && !tx.HasEnded())
                    tx.RollBack();
                LastRunSucceeded = false;
                Logs.Add(new LogEntry(LogLevel.Error, $"PlaceViews: transaction rolled back — {ex.Message}"));
            }
        }

        /// <summary>
        /// Three-pass viewport placement (Create default -> Read actual box
        /// outline via GetBoxOutline() -> Move to the real computed offset),
        /// since Viewport.Create() does not accept a target position directly
        /// and its initial placement does not respect our packed X/Y offset.
        /// Silent skip (Warning log, no dialog) if the view cannot be placed
        /// (e.g. already on another sheet), per our logging convention.
        /// </summary>
        /// <param name="titleblockOriginTopLeft">
        /// V204 fix: real sheet-space coordinates of the titleblock's own
        /// top-left corner (from GetTitleblockBoundingBox), replacing the
        /// previous incorrect assumption that (0,0) in Revit sheet space is
        /// always the titleblock's corner. Null falls back to (0,0) with a
        /// Warning, matching pre-fix behavior only as a last resort.
        /// </param>
        private void PlaceSingleViewport(Document doc, ViewSheet sheet, ViewPlacement placement, XYZ? titleblockOriginTopLeft)
        {
            var view = doc.GetElement(placement.View.ViewId) as View;
            if (view == null)
            {
                SkippedCount++;
                Logs.Add(new LogEntry(LogLevel.Warning, $"PlaceViews: view '{placement.View.Name}' no longer exists — skipped."));
                return;
            }

            if (!Viewport.CanAddViewToSheet(doc, sheet.Id, view.Id))
            {
                SkippedCount++;
                Logs.Add(new LogEntry(LogLevel.Warning, $"PlaceViews: view '{placement.View.Name}' cannot be placed on this sheet (already placed elsewhere?) — skipped."));
                return;
            }

            // Pass 1: Create at a default point near the sheet origin.
            var defaultPoint = new XYZ(0, 0, 0);
            Viewport vp;
            try
            {
                vp = Viewport.Create(doc, sheet.Id, view.Id, defaultPoint);
            }
            catch (Exception ex)
            {
                FailedCount++;
                Logs.Add(new LogEntry(LogLevel.Warning, $"PlaceViews: could not create viewport for '{placement.View.Name}' — {ex.Message}"));
                return;
            }

            // Pass 2: Read actual box outline to know the viewport's real
            // extents (title block/label offsets vary the actual bounding
            // box vs. the raw view crop size).
            var outline = vp.GetBoxOutline();
            var currentCenter = (outline.MinimumPoint + outline.MaximumPoint) / 2.0;

            // Pass 3: Move to the real computed target position.
            //
            // V204 fix — three compounding bugs corrected here (two from the
            // prior patch, plus the real root cause this time):
            //
            // (1) WRONG ORIGIN (this fix — the actual root cause of views
            //     landing entirely outside the titleblock border): the
            //     previous code assumed Revit sheet coordinate (0,0) is
            //     always the titleblock's usable-area top-left corner. That
            //     is only true if the titleblock family happens to be
            //     inserted exactly at sheet origin — for any titleblock
            //     inserted elsewhere, every placement was off by that same
            //     offset, which is exactly what put views far outside the
            //     border. titleblockOriginTopLeft is now queried from the
            //     REAL titleblock instance's bounding box on this specific
            //     sheet (see GetTitleblockBoundingBox), and all offsets are
            //     anchored to that real point instead of an assumption.
            //
            // (2) CORNER vs CENTER: placement.OffsetXMm/OffsetYMm are the
            //     view's TOP-LEFT corner (packing is left-to-right, top-to-
            //     bottom from a 0,0 origin in the packer's own frame), but
            //     ElementTransformUtils.MoveElement moves based on center-to-
            //     center translation — so the corner offset is converted to
            //     a center offset by adding half the view's WidthMm/HeightMm.
            //
            // (3) MARGIN OFFSET: OffsetXMm/OffsetYMm are measured from the
            //     USABLE AREA's top-left (already inset by Margin Top/Left —
            //     GreedyRowPackingService packs against titleblock.Usable*Mm,
            //     and the Stage 2 preview canvas draws the same inset). That
            //     margin is added on top of the real titleblock origin from
            //     fix (1) before converting to Revit's coordinate frame.
            double originXFeet, originYFeet;
            if (titleblockOriginTopLeft != null)
            {
                originXFeet = titleblockOriginTopLeft.X;
                originYFeet = titleblockOriginTopLeft.Y;
            }
            else
            {
                // Last-resort fallback only — logged once per sheet by the caller.
                originXFeet = 0;
                originYFeet = 0;
            }

            double topLeftXMm = MarginLeftMm + placement.OffsetXMm;
            double topLeftYMm = MarginTopMm + placement.OffsetYMm;

            double centerXMm = topLeftXMm + (placement.View.WidthMm / 2.0);
            double centerYMm = topLeftYMm + (placement.View.HeightMm / 2.0);

            // Sheet usable-area origin is top-left with Y increasing downward
            // in our packing/margin model, but Revit's sheet space Y increases
            // upward — invert the Y contribution before adding to the real
            // titleblock origin (which is already in Revit's native Y-up frame).
            double targetXFeet = originXFeet + MmToFeet(centerXMm);
            double targetYFeet = originYFeet - MmToFeet(centerYMm);
            var targetCenter = new XYZ(targetXFeet, targetYFeet, 0);

            var translation = targetCenter - currentCenter;
            ElementTransformUtils.MoveElement(doc, vp.Id, translation);

            PlacedCount++;
            Logs.Add(new LogEntry(LogLevel.Info, $"PlaceViews: placed '{placement.View.Name}' at {placement.Position} on sheet {sheet.SheetNumber}."));
        }

        // ─────────────────────────────────────────────────────────────
        // OPEN SHEETS (Stage 4)
        // ─────────────────────────────────────────────────────────────
        private void ExecuteOpenSheets()
        {
            var doc = _uiDoc.Document;
            Logs.Add(new LogEntry(LogLevel.Info, $"OpenSheets: opening {SheetIdsToOpen.Count} selected sheet(s)."));

            foreach (var id in SheetIdsToOpen)
            {
                try
                {
                    var view = doc.GetElement(id) as View;
                    if (view != null)
                        _uiDoc.ActiveView = view;
                }
                catch (Exception ex)
                {
                    Logs.Add(new LogEntry(LogLevel.Warning, $"OpenSheets: could not open sheet {id} — {ex.Message}"));
                }
            }

            LastRunSucceeded = true;
        }

        // ─────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────
        private static double FeetToMm(double feet) => feet * 304.8;
        private static double MmToFeet(double mm) => mm / 304.8;

        /// <summary>
        /// V204 fix: finds the actual titleblock FamilyInstance Revit placed on
        /// the given sheet (ViewSheet.Create auto-places one instance of the
        /// chosen FamilySymbol), and returns its real bounding box in sheet
        /// space. Previous code assumed the titleblock's usable area started
        /// at sheet coordinate (0,0), which is wrong whenever the titleblock
        /// family isn't inserted exactly at that point — this was the root
        /// cause of views landing far outside the titleblock border entirely.
        /// Returns null if no titleblock instance is found (falls back to the
        /// old (0,0)-origin assumption with a Warning log at the call site).
        /// </summary>
        private static BoundingBoxXYZ? GetTitleblockBoundingBox(Document doc, ViewSheet sheet)
        {
            var titleblockInstance = new FilteredElementCollector(doc, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .FirstOrDefault();

            return titleblockInstance?.get_BoundingBox(sheet);
        }

        private (double widthMm, double heightMm) ComputeSizeOnSheetMm(View v)
        {
            var cropBox = v.CropBox;
            double widthFeet = cropBox.Max.X - cropBox.Min.X;
            double heightFeet = cropBox.Max.Y - cropBox.Min.Y;

            int scale = SafeScale(v);
            double scaleFactor = scale > 0 ? scale : 1;

            // Size on sheet = crop box size (model space) / view scale, converted to mm.
            double widthMm = FeetToMm(widthFeet) / scaleFactor;
            double heightMm = FeetToMm(heightFeet) / scaleFactor;
            return (widthMm, heightMm);
        }

        private static int SafeScale(View v)
        {
            try
            {
                if (v.ViewType == ViewType.ThreeD || v.ViewType == ViewType.Legend || v.ViewType == ViewType.Schedule)
                    return 0;
                return v.Scale;
            }
            catch
            {
                return 0;
            }
        }
    }
}
