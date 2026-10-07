using Revit26_Plugin.SheetViewArrange.V001.Core.Layout;
using Revit26_Plugin.SheetViewArrange.V001.Core.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.SheetViewArrange.V001.Core.Services
{
    /// <summary>One viewport and the footprint it should end up at.</summary>
    public sealed class PlannedMove
    {
        /// <summary>The viewport as read from the sheet.</summary>
        public ViewportSnapshot Viewport { get; init; }

        /// <summary>Target footprint in sheet feet.</summary>
        public LayoutRect Target { get; init; }

        /// <summary>0-based row.</summary>
        public int Row { get; init; }

        /// <summary>False when the target lies outside the usable area.</summary>
        public bool Fits { get; init; }

        /// <summary>Translation from the current footprint to the target (feet).</summary>
        public double DeltaX => Target.MinX - Viewport.Footprint.MinX;

        /// <summary>Translation from the current footprint to the target (feet).</summary>
        public double DeltaY => Target.MinY - Viewport.Footprint.MinY;

        /// <summary>True when the viewport actually has to move.</summary>
        public bool NeedsMove => Math.Abs(DeltaX) > ArrangePlanner.MoveTolerance || Math.Abs(DeltaY) > ArrangePlanner.MoveTolerance;
    }

    /// <summary>The full arrangement for one sheet: what moves where, and whether it can be applied.</summary>
    public sealed class ArrangePlan
    {
        /// <summary>The sheet the plan was made from.</summary>
        public SheetSnapshot Sheet { get; init; }

        /// <summary>Usable area (title block inset by margins), or null when there is none.</summary>
        public LayoutRect? Area { get; init; }

        /// <summary>Participating viewports in reading order.</summary>
        public List<PlannedMove> Moves { get; init; } = new();

        /// <summary>Viewports left where they are.</summary>
        public List<ViewportSnapshot> Skipped { get; init; } = new();

        /// <summary>Grid rows: participating views in order, then skipped ones.</summary>
        public List<ArrangeRow> Rows { get; init; } = new();

        /// <summary>Number of layout rows.</summary>
        public int RowCount { get; init; }

        /// <summary>Why the plan cannot be applied, or null when it can.</summary>
        public string Blocker { get; init; }

        /// <summary>Non-blocking issues worth telling the user about.</summary>
        public List<string> Warnings { get; init; } = new();

        /// <summary>Viewports that will actually move.</summary>
        public int MoveCount => Moves.Count(m => m.NeedsMove);

        /// <summary>True when Apply would do something and nothing blocks it.</summary>
        public bool CanApply => Blocker == null && MoveCount > 0;

        /// <summary>
        /// Every move as "id:dx,dy". Two plans with the same signature move the same viewports by
        /// the same amounts — used to refuse an Apply when the sheet changed after the preview.
        /// </summary>
        public string Signature
        {
            get
            {
                var sb = new StringBuilder();
                foreach (var m in Moves.Where(m => m.NeedsMove))
                    sb.Append(m.Viewport.ViewportId?.Value.ToString(CultureInfo.InvariantCulture))
                      .Append(':').Append(Math.Round(m.DeltaX, 5).ToString(CultureInfo.InvariantCulture))
                      .Append(',').Append(Math.Round(m.DeltaY, 5).ToString(CultureInfo.InvariantCulture))
                      .Append(';');
                return sb.ToString();
            }
        }
    }

    /// <summary>
    /// Turns a <see cref="SheetSnapshot"/> plus settings into an <see cref="ArrangePlan"/>.
    /// No Revit API calls — used for both the live preview and the Apply step, so the
    /// preview always shows exactly what Apply does.
    /// </summary>
    public static class ArrangePlanner
    {
        /// <summary>Moves smaller than this (feet, ≈ 0.003 mm) are treated as "already in place".</summary>
        public const double MoveTolerance = 1e-5;

        private const double MmToFeet = 1.0 / 304.8;
        private const double FeetToMm = 304.8;

        /// <summary>Builds the plan.</summary>
        public static ArrangePlan Build(SheetSnapshot sheet, SheetViewArrangeSettings settings)
        {
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            var (participants, skipped) = SplitParticipants(sheet, settings.MovePinned);
            var warnings = CollectWarnings(sheet, participants, skipped);
            var (area, blocker) = UsableArea(sheet, settings);

            var moves = new List<PlannedMove>();
            int rowCount = 0;
            if (area is LayoutRect usable && participants.Count > 0)
            {
                var layout = ReadingTableLayout.Compute(
                    participants.Select(v => (v.Footprint.Width, v.Footprint.Height)).ToList(),
                    usable,
                    settings.MinHorizontalGapMm * MmToFeet,
                    settings.MinVerticalGapMm * MmToFeet,
                    settings.LastRowMode);
                rowCount = layout.RowCount;

                foreach (var slot in layout.Slots)
                {
                    var vp = participants[slot.Index];
                    moves.Add(new PlannedMove
                    {
                        Viewport = vp,
                        Target = new LayoutRect(slot.MinX, slot.MinY, slot.MinX + vp.Footprint.Width, slot.MinY + vp.Footprint.Height),
                        Row = slot.Row,
                        Fits = slot.Fits
                    });
                }

                if (!layout.AllFit)
                    blocker = "Does not fit: " + layout.Problem + " Reduce margins or gaps, or move some views to another sheet.";
            }
            else if (blocker == null)
            {
                blocker = sheet.Viewports.Count == 0
                    ? "There are no views on this sheet."
                    : "Every view on this sheet is skipped — nothing to arrange.";
            }

            return new ArrangePlan
            {
                Sheet = sheet,
                Area = area,
                Moves = moves,
                Skipped = skipped.Select(s => s.Viewport).ToList(),
                Rows = BuildRows(participants, moves, skipped),
                RowCount = rowCount,
                Blocker = blocker,
                Warnings = warnings
            };
        }

        /// <summary>Participants in reading order (detail number, then view name), plus skipped views with the reason.</summary>
        private static (List<ViewportSnapshot> Participants, List<(ViewportSnapshot Viewport, string Why)> Skipped)
            SplitParticipants(SheetSnapshot sheet, bool movePinned)
        {
            var participants = new List<ViewportSnapshot>();
            var skipped = new List<(ViewportSnapshot, string)>();
            foreach (var vp in sheet.Viewports)
            {
                if (vp.LockReason != null)
                    skipped.Add((vp, vp.LockReason));
                else if (vp.IsPinned && !movePinned)
                    skipped.Add((vp, "Pinned"));
                else
                    participants.Add(vp);
            }

            participants = participants
                .OrderBy(v => v.DetailNumber, DetailNumberComparer.Instance)
                .ThenBy(v => v.ViewName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            skipped = skipped
                .OrderBy(s => s.Item1.DetailNumber, DetailNumberComparer.Instance)
                .ToList();
            return (participants, skipped);
        }

        private static List<string> CollectWarnings(
            SheetSnapshot sheet,
            List<ViewportSnapshot> participants,
            List<(ViewportSnapshot Viewport, string Why)> skipped)
        {
            var warnings = new List<string>();

            int blank = participants.Count(v => string.IsNullOrWhiteSpace(v.DetailNumber));
            if (blank > 0)
                warnings.Add($"{blank} view(s) have no detail number — placed last, ordered by view name.");

            var duplicates = participants
                .Where(v => !string.IsNullOrWhiteSpace(v.DetailNumber))
                .GroupBy(v => v.DetailNumber, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicates.Count > 0)
                warnings.Add($"Duplicate detail number(s): {string.Join(", ", duplicates)} — ordered by view name.");

            if (skipped.Count > 0)
                warnings.Add($"{skipped.Count} view(s) stay where they are ({string.Join("; ", skipped.Select(s => s.Why).Distinct())}); arranged views may overlap them.");

            if (sheet.ScheduleCount > 0)
                warnings.Add($"{sheet.ScheduleCount} schedule(s) on the sheet are not moved; arranged views may overlap them.");

            if (sheet.UnreadableCount > 0)
                warnings.Add($"{sheet.UnreadableCount} viewport(s) could not be read and are not moved; arranged views may overlap them.");

            return warnings;
        }

        /// <summary>Title block frame inset by the margins, or the reason there is no usable area.</summary>
        private static (LayoutRect? Area, string Blocker) UsableArea(SheetSnapshot sheet, SheetViewArrangeSettings settings)
        {
            if (sheet.TitleBlockFrame is not LayoutRect frame)
                return (null, sheet.FrameProblem ?? "No usable title block on this sheet.");

            var inset = new LayoutRect(
                frame.MinX + Math.Max(0, settings.MarginLeftMm) * MmToFeet,
                frame.MinY + Math.Max(0, settings.MarginBottomMm) * MmToFeet,
                frame.MaxX - Math.Max(0, settings.MarginRightMm) * MmToFeet,
                frame.MaxY - Math.Max(0, settings.MarginTopMm) * MmToFeet);

            return inset.IsValid
                ? (inset, null)
                : (null, "The margins leave no usable area inside the title block — reduce them.");
        }

        /// <summary>Grid rows: participants in reading order (with their slot when laid out), then skipped views.</summary>
        private static List<ArrangeRow> BuildRows(
            List<ViewportSnapshot> participants,
            List<PlannedMove> moves,
            List<(ViewportSnapshot Viewport, string Why)> skipped)
        {
            var rows = new List<ArrangeRow>();
            for (int i = 0; i < participants.Count; i++)
            {
                var vp = participants[i];
                PlannedMove m = moves.Count > 0 ? moves[i] : null; // moves are in participant order
                ArrangeStatus status = m == null || !m.Fits ? ArrangeStatus.DoesNotFit
                                     : m.NeedsMove ? ArrangeStatus.Move
                                     : ArrangeStatus.InPlace;
                string note = m == null ? "No usable area"
                            : !m.Fits ? "Outside the usable area"
                            : !m.NeedsMove ? ""
                            : vp.IsPinned ? "Stays pinned" : "";

                rows.Add(NewRow(vp, i + 1, m == null ? null : m.Row + 1, status, note));
            }

            foreach (var (vp, why) in skipped)
                rows.Add(NewRow(vp, null, null, ArrangeStatus.Skipped, why));

            return rows;
        }

        private static ArrangeRow NewRow(ViewportSnapshot vp, int? order, int? row, ArrangeStatus status, string note) => new()
        {
            Order = order,
            DetailNumber = vp.DetailNumber,
            ViewName = vp.ViewName,
            ViewTypeName = vp.ViewTypeName,
            SizeText = $"{vp.Footprint.Width * FeetToMm:F0} × {vp.Footprint.Height * FeetToMm:F0}",
            Row = row,
            Status = status,
            Note = note
        };
    }
}
