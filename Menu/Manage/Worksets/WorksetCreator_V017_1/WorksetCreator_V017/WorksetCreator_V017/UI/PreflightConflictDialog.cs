// ==============================================
// File: PreflightConflictDialog.cs
// Layer: UI
// Workset Creator V017 — this file was introduced in V016.
//
// The blocking dialog for a failed pre-flight check. This is the one dialog in
// the tool that stops a run outright, which is deliberate: the two rules are
// one workset per linked file and every linked file gets one, and a run that
// would break either must not reach the TransactionGroup at all.
//
// Nothing has been changed by the time this appears — the check runs before the
// first transaction opens — so the message says so plainly rather than leaving
// the user wondering what landed in the model.
// ==============================================

using Autodesk.Revit.UI;
using Revit26_Plugin.WorksetCreator.V017.Core.Services;
using System;
using System.Linq;

namespace Revit26_Plugin.WorksetCreator.V017.UI
{
    internal static class PreflightConflictDialog
    {
        /// <summary>
        /// Shows the conflicts that blocked a run. UI thread only, and only ever
        /// before a transaction — never mid-run.
        /// </summary>
        public static void Show(PreflightResult result)
        {
            if (result == null || result.CanRun) return;

            int count = result.Conflicts.Count;

            // At most six in the dialog body — the full list is one click away in the
            // expanded content and on the clipboard.
            string body = string.Join("\n\n", result.Conflicts
                .Take(6)
                .Select(c => $"{c.Title}\n    {c.Detail}"));

            if (count > 6)
                body += $"\n\n…and {count - 6} more — see the full list below.";

            try
            {
                var dialog = new TaskDialog(WorksetCreatorTool.Name)
                {
                    MainIcon        = TaskDialogIcon.TaskDialogIconError,
                    MainInstruction = $"Cannot run — {count} conflict(s) would create duplicate worksets",
                    MainContent     = "The run was stopped before anything was changed. " +
                                      "Resolve these rows, then press Create again.\n\n" + body,
                    ExpandedContent = result.ToReportText(),
                    FooterText      = $"{WorksetCreatorTool.Title} · pre-flight check · nothing was committed",
                    CommonButtons   = TaskDialogCommonButtons.Close,
                    DefaultButton   = TaskDialogResult.Close
                };

                dialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1,
                    "Copy the conflict list to clipboard",
                    "Every conflict, plus anything left without a workset.");

                if (dialog.Show() == TaskDialogResult.CommandLink1)
                    CopyReport(result);
            }
            catch (Exception)
            {
                // The conflicts are already in the window log — failing to show the
                // dialog must never be worse than not running.
            }
        }

        /// <summary>Puts the conflict report on the clipboard.</summary>
        public static void CopyReport(PreflightResult result)
        {
            if (result == null) return;
            try { System.Windows.Clipboard.SetText(result.ToReportText()); }
            catch (Exception) { /* clipboard held by another app — nothing to recover */ }
        }
    }
}
