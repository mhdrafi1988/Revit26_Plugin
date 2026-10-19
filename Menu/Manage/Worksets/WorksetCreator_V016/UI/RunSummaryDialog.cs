// ==============================================
// File: RunSummaryDialog.cs
// Layer: UI
// Workset Creator V016 — carried from V015.
// Change vs V015: the counts block names the links the scan excluded, so a
// link that is missing from the window is explained in the dialog too.
// ==============================================

using Autodesk.Revit.UI;
using Revit26_Plugin_Revised.WorksetCreator.V016.Core.Models;
using System;

namespace Revit26_Plugin_Revised.WorksetCreator.V016.UI
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
                $"Linked files listed:\t{summary.LinksInModel}\n" +
                $"Selected for this run:\t{summary.LinksSelected}\n" +
                $"Worksets created:\t{summary.WorksetsCreated}\n" +
                $"Existing worksets used:\t{summary.WorksetsReused}\n" +
                $"Links assigned:\t\t{summary.LinksAssigned}  ({summary.InstancesAssigned} instance(s))\n" +
                $"Skipped:\t\t{summary.Skipped}\n" +
                $"Missed:\t\t\t{summary.Missed}\n" +
                $"Errors:\t\t\t{summary.Errors}";

            if (summary.ExcludedTotal > 0)
                counts += $"\nExcluded by the scan:\t{summary.ExcludedTotal}  " +
                          $"({summary.ExcludedIfc} IFC, {summary.ExcludedNested} nested)";

            if (summary.MissedUnloaded > 0)
                counts += $"\n\n{summary.MissedUnloaded} unloaded link(s) could not be re-assigned — " +
                          "load them and run again.";

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
