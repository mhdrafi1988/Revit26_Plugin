using Autodesk.Revit.DB;
using Revit26_Plugin.DeleteWorkset.V001.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.DeleteWorkset.V001.Core.Services
{
    /// <summary>
    /// Encapsulates workset enumeration and deletion logic, operating inside
    /// caller-supplied transactions/groups.
    /// </summary>
    public class DeleteWorksetService
    {
        private readonly Action<LogEntry> _log;

        public DeleteWorksetService(Action<LogEntry> log) => _log = log;

        // ── Enumeration ───────────────────────────────────────────────────────

        /// <summary>
        /// Returns one <see cref="WorksetRow"/> for every user workset in the document.
        /// </summary>
        public List<WorksetRow> LoadWorksets(Document doc)
        {
            var allUser = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .ToList();

            // Count how many are open/editable — we cannot delete the last one.
            int editableCount = allUser.Count(ws => ws.IsEditable);

            var rows = new List<WorksetRow>();

            foreach (var ws in allUser)
            {
                int elementCount = CountElements(doc, ws.Id);
                string owner = ws.Owner ?? string.Empty;
                bool isOpen = ws.IsOpen;

                bool isDeletable = true;
                string reason = null;

                if (!ws.IsEditable)
                {
                    isDeletable = false;
                    reason = "Built-in workset — cannot be deleted.";
                }
                else if (editableCount == 1)
                {
                    isDeletable = false;
                    reason = "This is the only editable workset; Revit requires at least one.";
                }

                rows.Add(new WorksetRow(ws.Id, ws.Name, elementCount, owner, isOpen, isDeletable, reason));
            }

            return rows.OrderBy(r => r.Name).ToList();
        }

        // ── Deletion ──────────────────────────────────────────────────────────

        /// <summary>
        /// Deletes <paramref name="toDelete"/> worksets, migrating their elements
        /// to <paramref name="targetId"/> where possible.
        /// Returns a <see cref="DeletionResult"/> with counts.
        /// Must be called inside a <see cref="TransactionGroup"/>.
        /// </summary>
        public DeletionResult DeleteWorksets(
            Document doc,
            IReadOnlyList<WorksetRow> toDelete,
            WorksetId targetId,
            bool hardDeleteUnmigratable,
            Action<WorksetRow> confirmCallback)
        {
            int deleted = 0;
            int migrated = 0;
            int hardDeleted = 0;
            int skipped = 0;

            foreach (var row in toDelete)
            {
                confirmCallback?.Invoke(row);

                using var tx = new Transaction(doc, $"Delete workset '{row.Name}'");
                tx.Start();
                try
                {
                    // 1. Collect all elements on this workset.
                    var allIds = new FilteredElementCollector(doc)
                        .WhereElementIsNotElementType()
                        .Where(e => e.WorksetId.IntegerValue == row.WorksetId.IntegerValue)
                        .Select(e => e.Id)
                        .ToList();

                    // 2. Separate migratable from un-migratable elements.
                    var (canMigrate, cannotMigrate) = PartitionElements(doc, allIds, targetId);

                    // 3. Migrate migratable elements.
                    foreach (var id in canMigrate)
                    {
                        var el = doc.GetElement(id);
                        if (el == null) continue;
                        var param = el.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                        if (param != null && !param.IsReadOnly)
                        {
                            param.Set(targetId.IntegerValue);
                            migrated++;
                        }
                        else
                        {
                            _log(new LogEntry(LogLevel.Warning,
                                $"  Skipped element {id.Value} ('{GetElementDescription(doc, id)}'): workset parameter is read-only."));
                            skipped++;
                        }
                    }

                    if (canMigrate.Count > 0)
                        _log(new LogEntry(LogLevel.Info,
                            $"  Migrated {canMigrate.Count} elements → '{GetWorksetName(doc, targetId)}'."));

                    // 4. Handle un-migratable elements.
                    if (cannotMigrate.Count > 0)
                    {
                        if (hardDeleteUnmigratable)
                        {
                            doc.Delete(new System.Collections.Generic.List<ElementId>(cannotMigrate));
                            hardDeleted += cannotMigrate.Count;
                            _log(new LogEntry(LogLevel.Warning,
                                $"  Hard-deleted {cannotMigrate.Count} un-migratable element(s)."));
                        }
                        else
                        {
                            skipped += cannotMigrate.Count;
                            _log(new LogEntry(LogLevel.Warning,
                                $"  Skipped {cannotMigrate.Count} un-migratable element(s) (option not enabled)."));
                        }
                    }

                    // 5. Delete the workset itself.
                    // At this point all migratable elements have been moved and
                    // un-migratable elements have been deleted or skipped.
                    // Use MoveElementsToWorkset so any remaining elements (skipped ones)
                    // are at least moved to the target rather than silently deleted by Revit.
                    var settings = new DeleteWorksetSettings(
                        DeleteWorksetOption.MoveElementsToWorkset, targetId);
                    WorksetTable.DeleteWorkset(doc, row.WorksetId, settings);
                    deleted++;
                    _log(new LogEntry(LogLevel.Success, $"Deleted workset '{row.Name}'."));

                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    _log(new LogEntry(LogLevel.Error,
                        $"Failed to delete workset '{row.Name}': {ex.Message}"));
                    skipped++;
                }
            }

            return new DeletionResult(deleted, migrated, hardDeleted, skipped);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static int CountElements(Document doc, WorksetId wsId)
            => new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .Count(e => e.WorksetId.IntegerValue == wsId.IntegerValue);

        /// <summary>
        /// Returns (canMigrate, cannotMigrate) split based on whether the
        /// element's workset parameter is settable and whether a valid target exists.
        /// </summary>
        private static (List<ElementId> canMigrate, List<ElementId> cannotMigrate) PartitionElements(
            Document doc, IEnumerable<ElementId> ids, WorksetId targetId)
        {
            var canMigrate    = new List<ElementId>();
            var cannotMigrate = new List<ElementId>();

            foreach (var id in ids)
            {
                var el = doc.GetElement(id);
                if (el == null) continue;

                // View-specific elements (annotations, tags placed in views) live on their
                // view's workset and cannot be reassigned.
                if (el.OwnerViewId != null && el.OwnerViewId != ElementId.InvalidElementId)
                {
                    cannotMigrate.Add(id);
                    continue;
                }

                var param = el.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (param == null || param.IsReadOnly)
                {
                    cannotMigrate.Add(id);
                    continue;
                }

                canMigrate.Add(id);
            }

            return (canMigrate, cannotMigrate);
        }

        private static string GetWorksetName(Document doc, WorksetId id)
            => doc.GetWorksetTable().GetWorkset(id)?.Name ?? id.IntegerValue.ToString();

        private static string GetElementDescription(Document doc, ElementId id)
        {
            var el = doc.GetElement(id);
            if (el == null) return id.Value.ToString();
            string cat = el.Category?.Name ?? el.GetType().Name;
            return string.IsNullOrEmpty(el.Name) ? cat : $"{cat} '{el.Name}'";
        }
    }

    /// <summary>Outcome counters from a deletion run.</summary>
    public sealed class DeletionResult
    {
        public int Deleted     { get; }
        public int Migrated    { get; }
        public int HardDeleted { get; }
        public int Skipped     { get; }

        public DeletionResult(int deleted, int migrated, int hardDeleted, int skipped)
        {
            Deleted     = deleted;
            Migrated    = migrated;
            HardDeleted = hardDeleted;
            Skipped     = skipped;
        }

        public override string ToString()
            => $"{Deleted} deleted  |  {Migrated} elements migrated  |  {HardDeleted} hard-deleted  |  {Skipped} skipped";
    }
}
