using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.SheetAutoRearrange.V002.Core.Engine;
using Revit26_Plugin.SheetAutoRearrange.V002.Core.Models;
using Revit26_Plugin.SheetAutoRearrange.V002.Core.Services;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Revit26_Plugin.SheetAutoRearrange.V002.Infrastructure.ExternalEvents
{
    /// <summary>Routes for the single external event pair, per suite orchestrator convention.</summary>
    public enum SheetAutoRearrangeAction
    {
        LoadViewsOnSheet,
        RunRearrange
    }

    /// <summary>
    /// Single IExternalEventHandler for Sheet Auto Rearrange. The ViewModel
    /// sets Action + the required request fields, then calls
    /// ExternalEvent.Raise(); Execute() dispatches based on Action.
    /// </summary>
    public class SheetAutoRearrangeEventHandler : IExternalEventHandler
    {
        public SheetAutoRearrangeAction Action { get; set; }

        // ── LoadViewsOnSheet request/response ──────────────────────────
        public ViewSheet? TargetSheet { get; set; }
        public List<ViewOnSheetItem>? LoadedItems { get; private set; }

        /// <summary>Titleblock usable-area bounds, returned alongside LoadedItems so a single
        /// Raise() populates both the grid and the preview's packing bounds — avoids a
        /// second Raise() racing the first before Execute() has run.</summary>
        public XYZ? UsableAreaMinFeet { get; private set; }
        public XYZ? UsableAreaMaxFeet { get; private set; }

        // ── RunRearrange request/response ──────────────────────────────
        public List<ViewOnSheetItem>? ItemsToProcess { get; set; }
        public RearrangeAlgorithm Algorithm { get; set; }
        public OverflowHandlingMode OverflowHandlingMode { get; set; }
        public GapSettings? GapSettings { get; set; }
        public double RowToleranceMm { get; set; }
        public RowAlignment RowAlignment { get; set; }
        public BlockAlignmentH BlockAlignmentH { get; set; }
        public BlockAlignmentV BlockAlignmentV { get; set; }
        public ObservableCollection<LogEntry>? Log { get; set; }
        public RunResult? LastRunResult { get; private set; }

        /// <summary>Raised on the UI thread after Execute() completes, regardless of Action.</summary>
        public event Action? Completed;

        private readonly ViewportCollectorService _collector = new();
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
                return;
            }

            LoadedItems = _collector.CollectViewsOnSheet(doc, TargetSheet);

            var (usableMin, usableMax) = GetSheetUsableArea(TargetSheet);
            UsableAreaMinFeet = usableMin;
            UsableAreaMaxFeet = usableMax;
        }

        private void ExecuteRun(Document doc)
        {
            if (TargetSheet == null || ItemsToProcess == null || GapSettings == null || Log == null)
            {
                LastRunResult = new RunResult { Success = false, ErrorMessage = "Missing required Run parameters." };
                return;
            }

            var (usableMin, usableMax) = GetSheetUsableArea(TargetSheet);

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
                usableMin,
                usableMax,
                Log);
        }

        /// <summary>
        /// ASSUMPTION: usable area is derived from the sheet's own BoundingBox
        /// rather than a titleblock-specific frame lookup — flagged for review,
        /// since the titleblock border/frame may sit inside this box with
        /// additional non-printable margin beyond GapSettings' user-set margins.
        /// </summary>
        private (XYZ min, XYZ max) GetSheetUsableArea(ViewSheet sheet)
        {
            var bbox = sheet.get_BoundingBox(null);
            XYZ min = bbox?.Min ?? XYZ.Zero;
            XYZ max = bbox?.Max ?? new XYZ(1, 1, 0);
            return (min, max);
        }

        public string GetName() => "Sheet Auto Rearrange Event Handler";
    }
}
