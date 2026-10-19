// ==============================================
// File: RunSummaryDialog.cs
// Layer: UI
// NEW in Workset Creator V015 — the post-run message output.
// Shows one Revit TaskDialog per run: headline + the six counts, the full
// per-link report under "Show details", and a command link that copies the
// report to the clipboard. Same TaskDialog shape ToolGuard.Report uses.
// ==============================================

using Autodesk.Revit.UI;
using Revit26_Plugin_Revised.WorksetCreator.V015.Core.Models;
using System;

namespace Revit26_Plugin_Revised.WorksetCreator.V015.UI
{
    internal static class RunSummaryDialog
    {
        /// <summary>
        /// Shows <paramref name="summary"/> to the user. Must be called on the UI
        /// thread, after the document transaction(s) have finished.
        /// </summary>
        public static void Show(RunSummary summary)
        {
            if (summary == null) return;

            string counts =
                $"Linked files in model:\t{summary.LinksInModel}\n" +
                $"Selected for this run:\t{summary.LinksSelected}\n" +
                $"Worksets created:\t{summary.WorksetsCreated}\n" +
                $"Existing worksets used:\t{summary.WorksetsReused}\n" +
                $"Links assigned:\t\t{summary.LinksAssigned}  ({summary.InstancesAssigned} instance(s))\n" +
                $"Skipped:\t\t{summary.Skipped}\n" +
                $"Missed:\t\t\t{summary.Missed}\n" +
                $"Errors:\t\t\t{summary.Errors}";

            try
            {
                var dialog = new TaskDialog(WorksetCreatorTool.Name)
                {
                    MainIcon = summary.Errors > 0
                        ? TaskDialogIcon.TaskDialogIconError
                        : (summary.Missed + summary.Skipped) > 0
                            ? TaskDialogIcon.TaskDialogIconWarning
                            : TaskDialogIcon.TaskDialogIconInformation,
                    MainInstruction = summary.HeadlineText(),
                    MainContent     = counts,
                    ExpandedContent = summary.ToReportText(),
                    FooterText      = $"{WorksetCreatorTool.Title} · {summary.Mode} · {summary.Label}",
                    CommonButtons   = TaskDialogCommonButtons.Close,
                    DefaultButton   = TaskDialogResult.Close
                };

                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                    "Copy full report to clipboard",
                    "Counts plus one line per linked file.");

                if (dialog.Show() == TaskDialogResult.CommandLink1)
                    CopyReport(summary);
            }
            catch (Exception)
            {
                // The report is still on the window card and in the log —
                // failing to show the dialog must never break the run.
            }
        }

        /// <summary>Puts the full report on the clipboard. Safe to call from the card's Copy button.</summary>
        public static void CopyReport(RunSummary summary)
        {
            if (summary == null) return;
            try { System.Windows.Clipboard.SetText(summary.ToReportText()); }
            catch (Exception) { /* clipboard busy (another app holds it) — nothing to recover */ }
        }
    }
}
