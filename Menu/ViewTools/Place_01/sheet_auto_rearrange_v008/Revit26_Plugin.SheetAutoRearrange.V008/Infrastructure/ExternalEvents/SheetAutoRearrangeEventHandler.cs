using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SheetAutoRearrange.V008.Core.Engine;
using Revit26_Plugin.SheetAutoRearrange.V008.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V008.Core.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.SheetAutoRearrange.V008.Infrastructure.ExternalEvents
{
    public enum SheetAutoRearrangeAction
    {
        LoadViewsOnSheet,
        RunRearrange,
        RedetectRegion
    }

    /// <summary>
    /// Single IExternalEventHandler for Sheet Auto Rearrange.
    ///
    /// V008 CHANGE: TitleBlockDetectionService.Detect now requires GapSettings
    /// (margins are applied at detection time, not just packing time — see
    /// V008 rewrite of TitleBlockDetectionService). GapSettings must
    /// therefore be set by the ViewModel BEFORE raising LoadViewsOnSheet or
    /// RedetectRegion, not just before RunRearrange as in V006/V007 — if
    /// null at detection time, ExecuteDetectRegion falls back to Undetected
    /// rather than crash or silently skip margins.
    /// </summary>
    public class SheetAutoRearrangeEventHandler : IExternalEventHandler
    {
        public SheetAutoRearrangeAction Action { get; set; }

        public ViewSheet? TargetSheet { get; set; }
        public List<ViewOnSheetItem>? LoadedItems { get; private set; }

        public PlaceableRegion? Region { get; private set; }
        public bool MultipleTitleBlocksFound { get; private set; }
        public bool NoTitleBlockFound { get; private set; }

        public (double minXMm, double minYMm, double maxXMm, double maxYMm)? ManualRegionOverride { get; set; }

        public List<ViewOnSheetItem>? ItemsToProcess { get; set; }
        public RearrangeAlgorithm Algorithm { get; set; }
        public OverflowHandlingMode OverflowHandlingMode { get; set; }
        public GapSettings? GapSettings { get; set; }
        public double RowToleranceMm { get; set; }
        public RowAlignment RowAlignment { get; set; }
        public BlockAlignmentH BlockAlignmentH { get; set; }
        public BlockAlignmentV BlockAlignmentV { get; set; }

        public TallWideDetectionSettings? TallSettings { get; set; }
        public TallWideDetectionSettings? WideSettings { get; set; }

        public ObservableCollection<LogEntry>? Log { get; set; }
        public RunResult? LastRunResult { get; private set; }

        public event Action? Completed;

        private readonly ViewportCollectorService _collector = new();
        private readonly TitleBlockDetectionService _detectionService = new();
        private readonly RearrangeEngine _engine = new();

        public void Execute(UIApplication app)
        {
            var doc = app.ActiveUIDocument.Document;

            try
            {
                switch (Action)
                {
                    case SheetAutoRearrangeAction.LoadViewsOnSheet:
                        ExecuteLoad(doc);
                        break;

                    case SheetAutoRearrangeAction.RedetectRegion:
                        ExecuteDetectRegion(doc);
                        break;

                    case SheetAutoRearrangeAction.RunRearrange:
                        ExecuteRun(doc);
                        break;
                }
            }
            finally
            {
                Completed?.Invoke();
            }
        }

        private void ExecuteLoad(Document doc)
        {
            if (TargetSheet == null)
            {
                LoadedItems = new List<ViewOnSheetItem>();
                Region = null;
                return;
            }

            LoadedItems = _collector.CollectViewsOnSheet(doc, TargetSheet);
            ExecuteDetectRegion(doc);
        }

        private void ExecuteDetectRegion(Document doc)
        {
            if (TargetSheet == null)
            {
                Region = null;
                return;
            }

            if (ManualRegionOverride.HasValue)
            {
                var m = ManualRegionOverride.Value;
                Region = _detectionService.BuildManualRegion(m.minXMm, m.minYMm, m.maxXMm, m.maxYMm);
                MultipleTitleBlocksFound = false;
                NoTitleBlockFound = false;
                return;
            }

            if (GapSettings == null)
            {
                // Margins are required for V008 detection (title block bbox
                // inset by margins). If the caller hasn't set GapSettings yet,
                // treat as undetected rather than guess at zero margins —
                // surfaces the real problem (a ViewModel call site missing
                // the GapSettings assignment) instead of masking it.
                Region = null;
                NoTitleBlockFound = false;
                MultipleTitleBlocksFound = false;
                return;
            }

            var detection = _detectionService.Detect(doc, TargetSheet, GapSettings);
            MultipleTitleBlocksFound = detection.MultipleTitleBlocksFound;
            NoTitleBlockFound = detection.NoTitleBlockFound;
            Region = detection.Region;
        }

        private void ExecuteRun(Document doc)
        {
            if (TargetSheet == null || ItemsToProcess == null || GapSettings == null || Log == null
                || TallSettings == null || WideSettings == null)
            {
                LastRunResult = new RunResult { Success = false, ErrorMessage = "Missing required Run parameters." };
                return;
            }

            ExecuteDetectRegion(doc);

            if (MultipleTitleBlocksFound)
            {
                LastRunResult = new RunResult
                {
                    Success = false,
                    ErrorMessage = "Sheet has multiple title blocks — Run skipped, no changes made."
                };
                return;
            }

            LastRunResult = _engine.Run(
                doc,
                TargetSheet,
                ItemsToProcess,
                Algorithm,
                OverflowHandlingMode,
                GapSettings,
                RowToleranceMm,
                RowAlignment,
                BlockAlignmentH,
                BlockAlignmentV,
                TallSettings,
                WideSettings,
                Region,
                Log);
        }

        public string GetName() => "Sheet Auto Rearrange Event Handler";
    }
}
