using Autodesk.Revit.DB;
using Revit26_Plugin.ParaManager.V002.Models;
using System;
using System.Collections.Generic;
using System.IO;

namespace Revit26_Plugin.ParaManager.V002.Services
{
    /// <summary>
    /// Per-row outcome, reported back to the ViewModel so it can update the
    /// grid's BindingTypeDisplay and drive the Activity Log line-by-line.
    /// </summary>
    public enum RowOutcome { Assigned, SkippedDuplicate, Failed }

    public class RowResult
    {
        public ParameterAssignmentRow Row { get; }
        public RowOutcome Outcome { get; }
        public string Message { get; }

        public RowResult(ParameterAssignmentRow row, RowOutcome outcome, string message)
        {
            Row = row;
            Outcome = outcome;
            Message = message;
        }
    }

    /// <summary>
    /// Executes the bulk parameter-to-category binding.
    /// Called from within Execute(IExternalEventHandler) on the Revit API thread —
    /// this class assumes it is already inside that context; it opens exactly one
    /// Transaction for the whole batch per suite convention (never open/commit in a loop).
    /// </summary>
    public class ParameterAssignmentService
    {
        private readonly Document _doc;
        private readonly string _sharedParamFilePath;

        public ParameterAssignmentService(Document doc, string sharedParamFilePath)
        {
            _doc = doc;
            _sharedParamFilePath = sharedParamFilePath;
        }

        /// <summary>
        /// Runs the whole queued batch inside ONE Transaction. On any unexpected
        /// (non-per-row) exception the transaction is rolled back entirely — per-row
        /// duplicate skips do NOT roll back the transaction, they're just omitted.
        /// </summary>
        public List<RowResult> RunBatch(IReadOnlyList<ParameterAssignmentRow> rows, BindingChoice binding)
        {
            var results = new List<RowResult>();

            // Point the app's shared parameter file at ours for the duration of this call.
            // Restored in `finally` so we don't leave the user's global Revit setting changed.
            var app = _doc.Application;
            var previousSharedParamFile = app.SharedParametersFilename;

            using (var tg = new TransactionGroup(_doc, "ParaManager — Bulk Parameter Assignment"))
            {
                tg.Start();
                try
                {
                    app.SharedParametersFilename = _sharedParamFilePath;
                    DefinitionFile defFile = app.OpenSharedParameterFile();

                    if (defFile == null)
                        throw new InvalidOperationException("Could not open shared parameter file — it may be missing or locked.");

                    using (var t = new Transaction(_doc, "Assign Shared Parameters"))
                    {
                        t.Start();
                        try
                        {
                            foreach (var row in rows)
                            {
                                var rowResult = AssignSingleRow(defFile, row, binding);
                                results.Add(rowResult);
                            }

                            t.Commit();
                        }
                        catch (Exception ex)
                        {
                            // Critical/transaction-level failure — roll back everything in this Transaction.
                            if (t.HasStarted() && !t.HasEnded())
                                t.RollBack();

                            results.Add(new RowResult(null, RowOutcome.Failed,
                                $"Transaction rolled back — {ex.Message}"));
                        }
                    }

                    tg.Assimilate();
                }
                catch (Exception ex)
                {
                    if (tg.HasStarted())
                        tg.RollBack();

                    results.Add(new RowResult(null, RowOutcome.Failed,
                        $"Assignment aborted — {ex.Message}"));
                }
                finally
                {
                    // Restore whatever shared parameter file the user had configured before we ran.
                    try { app.SharedParametersFilename = previousSharedParamFile; }
                    catch { /* best-effort restore; not worth failing the run over */ }
                }
            }

            return results;
        }

        private RowResult AssignSingleRow(DefinitionFile defFile, ParameterAssignmentRow row, BindingChoice binding)
        {
            try
            {
                Definition definition = FindDefinition(defFile, row.Parameter.Guid);
                if (definition == null)
                {
                    return new RowResult(row, RowOutcome.Failed,
                        $"Definition '{row.ParameterName}' not found in shared parameter file.");
                }

                var categorySet = new CategorySet();
                bool anyNewBinding = false;

                foreach (var cat in row.Categories)
                {
                    Category revitCategory = Category.GetCategory(_doc, cat.BuiltInCategory);
                    if (revitCategory == null) continue;

                    if (IsAlreadyBound(definition, revitCategory))
                    {
                        // Duplicate (parameter + category) pair — per spec: skip silently, log as Warning only.
                        continue;
                    }

                    categorySet.Insert(revitCategory);
                    anyNewBinding = true;
                }

                if (!anyNewBinding)
                {
                    return new RowResult(row, RowOutcome.SkippedDuplicate,
                        $"'{row.ParameterName}' already bound to all selected categories — skipped.");
                }

                Binding newBinding = binding == BindingChoice.Instance
                    ? (Binding)_doc.Application.Create.NewInstanceBinding(categorySet)
                    : _doc.Application.Create.NewTypeBinding(categorySet);

                BindingMap map = _doc.ParameterBindings;
                bool inserted = map.Insert(definition, newBinding, GroupTypeIdFor(row.ParameterGroup));

                if (!inserted)
                {
                    // Insert returns false if a binding already exists for this definition —
                    // fall back to ReInsert to extend categories on the existing binding.
                    inserted = map.ReInsert(definition, newBinding, GroupTypeIdFor(row.ParameterGroup));
                }

                if (!inserted)
                {
                    return new RowResult(row, RowOutcome.Failed,
                        $"Revit rejected binding for '{row.ParameterName}'.");
                }

                row.BindingTypeDisplay = binding.ToString();
                return new RowResult(row, RowOutcome.Assigned,
                    $"'{row.ParameterName}' bound to {row.CategoryDisplay} as {binding}.");
            }
            catch (Exception ex)
            {
                // Per-row failure — logged as Error but does NOT roll back the whole transaction;
                // only a transaction-level exception (caught in RunBatch) triggers full rollback.
                return new RowResult(row, RowOutcome.Failed, $"'{row.ParameterName}' failed — {ex.Message}");
            }
        }

        private bool IsAlreadyBound(Definition definition, Category category)
        {
            if (!_doc.ParameterBindings.Contains(definition)) return false;

            Binding existing = _doc.ParameterBindings.get_Item(definition);
            if (existing is not ElementBinding elementBinding) return false;

            foreach (Category boundCat in elementBinding.Categories)
            {
                if (boundCat.Id == category.Id) return true;
            }
            return false;
        }

        private Definition FindDefinition(DefinitionFile defFile, string guid)
        {
            foreach (DefinitionGroup group in defFile.Groups)
            {
                foreach (ExternalDefinition def in group.Definitions)
                {
                    if (def.GUID.ToString() == guid)
                        return def;
                }
            }
            return null;
        }

        /// <summary>
        /// Revit 2026 API uses ForgeTypeId-based parameter groups (GroupTypeId.*) rather than
        /// the legacy BuiltInParameterGroup enum. Maps our file's group name to the closest
        /// standard GroupTypeId, defaulting to GroupTypeId.Data when no match is found.
        /// FLAGGED ASSUMPTION: this mapping is heuristic (string match) — if your shared
        /// parameter file's group names don't match Revit's standard group names exactly,
        /// some parameters may land in "Data" instead of their intended group. Confirm this
        /// is acceptable, or provide an explicit name→GroupTypeId mapping table.
        /// </summary>
        private ForgeTypeId GroupTypeIdFor(string groupName)
        {
            return groupName?.Trim().ToLowerInvariant() switch
            {
                "text" => GroupTypeId.Text,
                "dimensions" => GroupTypeId.Geometry,
                "identity data" => GroupTypeId.IdentityData,
                "general" => GroupTypeId.General,
                "graphics" => GroupTypeId.Graphics,
                "materials and finishes" => GroupTypeId.Materials,
                "construction" => GroupTypeId.Construction,
                "structural" => GroupTypeId.Structural,
                "mechanical" => GroupTypeId.Mechanical,
                "electrical" => GroupTypeId.Electrical,
                "energy analysis" => GroupTypeId.EnergyAnalysis,
                _ => GroupTypeId.Data
            };
        }
    }
}
