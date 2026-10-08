using Autodesk.Revit.DB;
using Revit26_Plugin.BulkRename.V001.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.BulkRename.V001.Core.Services
{
    /// <summary>Counts of one Bulk Rename run.</summary>
    public sealed class RenameResult
    {
        /// <summary>Items renamed.</summary>
        public int Renamed { get; set; }

        /// <summary>Items skipped (changed since listing, now owned by another user, or Revit refused the name).</summary>
        public int Skipped { get; set; }

        /// <inheritdoc/>
        public override string ToString() => $"{Renamed} renamed, {Skipped} skipped.";
    }

    /// <summary>
    /// Renames items in the model. Every kind of item is renamed the same way, by setting the element's
    /// <c>Name</c>, so one service covers line styles, line patterns, arrowheads and fill patterns.
    /// </summary>
    public sealed class RenameService
    {
        private const string TempPrefix = "__bulk_rename_";

        private readonly Action<LogEntry> _log;

        /// <summary>Creates the service; <paramref name="log"/> receives progress and warnings.</summary>
        public RenameService(Action<LogEntry> log)
        {
            _log = log ?? (_ => { });
        }

        /// <summary>
        /// Renames <paramref name="items"/> to their <see cref="RenameItem.NewName"/> in one transaction, so
        /// one Ctrl+Z undoes the whole run. Each item is re-checked first: one that no longer exists, was
        /// renamed outside the window, or is now owned by another user is skipped and left unchanged. A name
        /// Revit refuses skips only that item. Swaps, chains and case-only changes go through a temporary
        /// name (see <see cref="RenamePlanner"/>). Throws on any other error; the transaction's using block
        /// then rolls everything back. Must run in a Revit API context.
        /// </summary>
        public RenameResult Apply(Document doc, IReadOnlyList<RenameItem> items, string transactionName)
        {
            var result = new RenameResult();

            var work = new List<(RenameItem Item, string Original, string Target)>();
            foreach (var item in items)
            {
                var element = doc.GetElement(item.Id);
                if (element == null)
                {
                    Skip(result, item.CurrentName, "it no longer exists.");
                    continue;
                }

                string original = element.Name;
                if (!string.Equals(original, item.CurrentName, StringComparison.Ordinal))
                {
                    Skip(result, item.CurrentName, $"it is now called '{original}'. Reload the list and try again.");
                    continue;
                }

                string owner = WorksharingGuard.OwnerLock(doc, item.Id);
                if (owner != null)
                {
                    Skip(result, item.CurrentName, owner);
                    continue;
                }

                work.Add((item, original, item.NewName.Trim()));
            }

            if (work.Count == 0) return result;

            var changes = work.Select((w, i) => new RenameChange(i, w.Original, w.Target)).ToList();
            var steps = RenamePlanner.Plan(changes, () => TempPrefix + Guid.NewGuid().ToString("N"));

            using var t = new Transaction(doc, transactionName);
            t.Start();

            var failed = new HashSet<int>();
            var onTempName = new HashSet<int>();

            foreach (var step in steps)
            {
                if (failed.Contains(step.Key)) continue;

                var (item, original, target) = work[step.Key];
                var element = doc.GetElement(item.Id);

                try
                {
                    element.Name = step.Name;

                    if (step.IsTemporary)
                    {
                        onTempName.Add(step.Key);
                    }
                    else
                    {
                        result.Renamed++;
                        _log(new LogEntry(LogLevel.Success, $"Renamed '{original}' to '{target}'."));
                    }
                }
                catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ArgumentException
                                              || ex is Autodesk.Revit.Exceptions.InvalidOperationException)
                {
                    failed.Add(step.Key);
                    Skip(result, original, $"Revit refused the name '{step.Name}': {ex.Message}");

                    // Put an item that already moved to its temporary name back where it was.
                    if (onTempName.Remove(step.Key))
                        RestoreName(element, original, ex);
                }
            }

            if (result.Renamed == 0)
            {
                t.RollBack();
                return result;
            }

            var status = t.Commit();
            if (status != TransactionStatus.Committed)
                throw new InvalidOperationException($"Revit did not commit the renames (status: {status}).");

            return result;
        }

        // If the original name cannot be restored the model would be left half-renamed, so give up:
        // the exception unwinds past the transaction's using block and rolls everything back.
        private static void RestoreName(Element element, string original, Exception cause)
        {
            try
            {
                element.Name = original;
            }
            catch (Exception ex) when (ex is Autodesk.Revit.Exceptions.ArgumentException
                                          || ex is Autodesk.Revit.Exceptions.InvalidOperationException)
            {
                throw new InvalidOperationException(
                    $"Could not put '{original}' back after Revit refused its new name ({cause.Message}). " +
                    "Nothing was renamed.", ex);
            }
        }

        private void Skip(RenameResult result, string name, string reason)
        {
            result.Skipped++;
            _log(new LogEntry(LogLevel.Warning, $"Skipped '{name}': {reason}"));
        }
    }
}
