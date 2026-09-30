using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Models;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Services;

namespace Revit26_Plugin.ScheduleExportImport.V001.Infrastructure.ExternalEvents
{
    public enum ScheduleImportRequest
    {
        LoadSchedules,
        ExportSchedule,
        AnalyzeImport,
        ApplyChanges
    }

    /// <summary>
    /// All Revit-API work for this tool. AnalyzeImport is read-only (no transaction) and
    /// builds the preview; ApplyChanges writes only the rows the user left ticked.
    /// </summary>
    public class ScheduleImportEventHandler : IExternalEventHandler
    {
        private readonly Document _doc;

        public ScheduleImportRequest Request { get; set; }

        // ---- Inputs ----
        public ElementId TargetScheduleId { get; set; }
        public ExcelImportFile ImportFile { get; set; }
        public List<ImportChange> ChangesToApply { get; set; } = new List<ImportChange>();

        // ---- Outputs ----
        public List<ScheduleViewInfo> LoadedSchedules { get; private set; } = new List<ScheduleViewInfo>();
        public List<string> ExportedHeaders { get; private set; } = new List<string>();
        public List<ScheduleRow> ExportedRows { get; private set; } = new List<ScheduleRow>();
        public HashSet<string> EditableHeaders { get; private set; } = new HashSet<string>();
        public ImportAnalysis Analysis { get; private set; } = new ImportAnalysis();
        public int AppliedElementCount { get; private set; }
        public string ErrorMessage { get; private set; } = string.Empty;
        public bool LastRunSucceeded { get; private set; }

        public event Action RequestCompleted;

        public ScheduleImportEventHandler(Document doc)
        {
            _doc = doc;
        }

        public void Execute(UIApplication app)
        {
            ErrorMessage = string.Empty;
            try
            {
                switch (Request)
                {
                    case ScheduleImportRequest.LoadSchedules: ExecuteLoadSchedules(); break;
                    case ScheduleImportRequest.ExportSchedule: ExecuteExportSchedule(); break;
                    case ScheduleImportRequest.AnalyzeImport: ExecuteAnalyzeImport(); break;
                    case ScheduleImportRequest.ApplyChanges: ExecuteApplyChanges(); break;
                }
                LastRunSucceeded = true;
            }
            catch (Exception ex)
            {
                LastRunSucceeded = false;
                ErrorMessage = ex.Message;
            }
            finally
            {
                RequestCompleted?.Invoke();
            }
        }

        public string GetName() => "Schedule Export/Import V001";

        // ── Load all schedule views ────────────────────────────────────────────
        private void ExecuteLoadSchedules()
        {
            LoadedSchedules = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(vs => !vs.IsTemplate && !vs.IsTitleblockRevisionSchedule)
                .OrderBy(vs => vs.Name)
                .Select(vs =>
                {
                    var def = vs.Definition;
                    int visibleFields = Enumerable.Range(0, def.GetFieldCount()).Count(i => !def.GetField(i).IsHidden);
                    int rowCount = 0;
                    try { rowCount = new FilteredElementCollector(_doc, vs.Id).WhereElementIsNotElementType().GetElementCount(); }
                    catch { }

                    return new ScheduleViewInfo
                    {
                        ViewId = vs.Id,
                        Name = vs.Name,
                        CategoryName = def.CategoryId != ElementId.InvalidElementId
                            ? Category.GetCategory(_doc, def.CategoryId)?.Name ?? "Unknown"
                            : "Multi-Category",
                        RowCount = rowCount,
                        FieldCount = visibleFields
                    };
                })
                .ToList();
        }

        // ── Export ─────────────────────────────────────────────────────────────
        private void ExecuteExportSchedule()
        {
            var schedule = GetSchedule();
            var def = schedule.Definition;

            var headers = new List<string>();
            var calculated = new HashSet<string>();
            for (int i = 0; i < def.GetFieldCount(); i++)
            {
                var field = def.GetField(i);
                if (field.IsHidden) continue;

                // Parameter name, not ColumnHeading: import matches Excel headers back to
                // parameters by name, so a renamed heading ("W" for "Width") would never match.
                var header = field.GetName();
                var unique = header;
                int dup = 1;
                while (headers.Contains(unique))
                    unique = $"{header} ({dup++})";
                headers.Add(unique);

                if (field.IsCalculatedField || field.FieldType == ScheduleFieldType.Formula)
                    calculated.Add(unique);
            }

            var rows = new List<ScheduleRow>();
            var editable = new HashSet<string>();

            foreach (var elem in CollectScheduleElements(schedule))
            {
                var row = new ScheduleRow { ElementId = elem.Id.Value };
                foreach (var header in headers)
                {
                    string value = string.Empty;
                    try
                    {
                        var param = elem.LookupParameter(header);
                        if (param != null && !param.IsReadOnly && !calculated.Contains(header))
                            editable.Add(header);
                        param ??= GetTypeParameter(elem, header);
                        if (param != null)
                            value = GetParameterDisplayValue(param);
                    }
                    catch { }
                    row.Values[header] = value;
                }
                rows.Add(row);
            }

            ExportedHeaders = headers;
            ExportedRows = rows;
            EditableHeaders = editable;
        }

        // ── Analyze (read-only) ────────────────────────────────────────────────
        private void ExecuteAnalyzeImport()
        {
            var file = ImportFile ?? throw new InvalidOperationException("No import file was loaded.");
            var analysis = new ImportAnalysis { RowsInFile = file.Rows.Count };

            var dedup = ImportRowValidator.Deduplicate(file.Rows);
            analysis.DuplicateRowsMerged = dedup.MergedDuplicateRows;
            analysis.Items.AddRange(dedup.Conflicts);

            foreach (var row in dedup.UniqueRows)
            {
                var elem = _doc.GetElement(new ElementId(row.ElementId));
                if (elem == null)
                {
                    analysis.Items.Add(new ImportChange
                    {
                        ElementId = row.ElementId,
                        Status = ImportChangeStatus.NotFound,
                        Message = "No element with this ID in the model (deleted, or file from another model)."
                    });
                    continue;
                }

                analysis.ElementsMatched++;
                foreach (var header in file.Headers)
                {
                    if (!row.Values.TryGetValue(header, out var newValue)) continue;

                    var param = elem.LookupParameter(header);
                    if (param == null)
                    {
                        var typeParam = GetTypeParameter(elem, header);
                        if (typeParam != null && !SameValue(GetParameterDisplayValue(typeParam), newValue))
                            analysis.Items.Add(NewItem(row.ElementId, header, typeParam, newValue, ImportChangeStatus.TypeParameter,
                                "Type parameter — edit the type instead; changing it here would affect every instance."));
                        continue;
                    }

                    var oldValue = GetParameterDisplayValue(param);
                    if (SameValue(oldValue, newValue))
                    {
                        analysis.UnchangedValues++;
                        continue;
                    }

                    analysis.Items.Add(param.IsReadOnly
                        ? NewItem(row.ElementId, header, param, newValue, ImportChangeStatus.ReadOnly, "Read-only in Revit — cannot be changed.")
                        : NewItem(row.ElementId, header, param, newValue, ImportChangeStatus.Change, string.Empty));
                }
            }

            // Elements the schedule shows today that the file doesn't mention at all.
            if (TargetScheduleId != null && _doc.GetElement(TargetScheduleId) is ViewSchedule schedule)
            {
                var inFile = new HashSet<long>(file.Rows.Select(r => r.ElementId));
                foreach (var elem in CollectScheduleElements(schedule).Where(e => !inFile.Contains(e.Id.Value)))
                {
                    analysis.Items.Add(new ImportChange
                    {
                        ElementId = elem.Id.Value,
                        Status = ImportChangeStatus.MissingFromFile,
                        Message = "In the schedule but not in the file (added after export, or row deleted). Left unchanged."
                    });
                }
            }

            Analysis = analysis;
        }

        // ── Apply ticked changes ──────────────────────────────────────────────
        private void ExecuteApplyChanges()
        {
            AppliedElementCount = 0;

            // One TransactionGroup, assimilated → a single "Schedule Import" entry in Revit's Undo.
            using var group = new TransactionGroup(_doc, "Schedule Import V001");
            group.Start();

            foreach (var byElement in ChangesToApply.GroupBy(c => c.ElementId))
            {
                var elem = _doc.GetElement(new ElementId(byElement.Key));
                if (elem == null)
                {
                    foreach (var c in byElement) MarkFailed(c, "Element no longer exists.");
                    continue;
                }

                using var tx = new Transaction(_doc, $"Import element {byElement.Key}");
                tx.Start();
                bool anyApplied = false;
                try
                {
                    foreach (var change in byElement)
                    {
                        var param = elem.LookupParameter(change.ParameterName);
                        if (param == null || param.IsReadOnly)
                        {
                            MarkFailed(change, "Parameter missing or read-only.");
                            continue;
                        }
                        try
                        {
                            if (WriteParameter(param, change.NewValue))
                            {
                                change.Status = ImportChangeStatus.Applied;
                                change.Message = string.Empty;
                                anyApplied = true;
                            }
                            else
                            {
                                MarkFailed(change, $"Revit rejected the value \"{change.NewValue}\" (check units/format).");
                            }
                        }
                        catch (Exception ex)
                        {
                            MarkFailed(change, ex.Message);
                        }
                    }
                    tx.Commit();
                    if (anyApplied) AppliedElementCount++;
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    foreach (var c in byElement.Where(c => c.Status == ImportChangeStatus.Applied))
                        MarkFailed(c, $"Rolled back: {ex.Message}");
                }
            }

            group.Assimilate();
        }

        // ── Helpers ────────────────────────────────────────────────────────────
        private ViewSchedule GetSchedule()
            => _doc.GetElement(TargetScheduleId) as ViewSchedule
               ?? throw new InvalidOperationException("Schedule view not found in document.");

        private List<Element> CollectScheduleElements(ViewSchedule schedule)
            => new FilteredElementCollector(_doc, schedule.Id).WhereElementIsNotElementType().ToList();

        private Parameter GetTypeParameter(Element elem, string name)
        {
            var typeId = elem.GetTypeId();
            return typeId == ElementId.InvalidElementId ? null : _doc.GetElement(typeId)?.LookupParameter(name);
        }

        private static ImportChange NewItem(long id, string header, Parameter param, string newValue, ImportChangeStatus status, string message)
            => new ImportChange
            {
                ElementId = id,
                ParameterName = header,
                OldValue = GetParameterDisplayValue(param),
                NewValue = newValue,
                Status = status,
                Message = message
            };

        private static void MarkFailed(ImportChange change, string message)
        {
            change.Status = ImportChangeStatus.Failed;
            change.Message = message;
        }

        private static bool SameValue(string a, string b)
            => string.Equals((a ?? string.Empty).Trim(), (b ?? string.Empty).Trim(), StringComparison.Ordinal);

        private static bool WriteParameter(Parameter param, string value)
        {
            switch (param.StorageType)
            {
                case StorageType.String:
                    return param.Set(value ?? string.Empty);

                case StorageType.Integer:
                    if (param.Definition.GetDataType() == SpecTypeId.Boolean.YesNo)
                    {
                        var v = value.Trim();
                        if (v.Equals("Yes", StringComparison.OrdinalIgnoreCase) || v == "1") return param.Set(1);
                        if (v.Equals("No", StringComparison.OrdinalIgnoreCase) || v == "0") return param.Set(0);
                        return false;
                    }
                    return int.TryParse(value, out int intVal) ? param.Set(intVal) : param.SetValueString(value);

                case StorageType.Double:
                    // Export wrote display units (e.g. "3000" mm); SetValueString parses them
                    // back through the project's units. Set(double) would treat them as feet.
                    return !string.IsNullOrWhiteSpace(value) && param.SetValueString(value);

                default:
                    return false;
            }
        }

        private static string GetParameterDisplayValue(Parameter param)
        {
            if (param.StorageType == StorageType.None) return string.Empty;
            if (param.StorageType == StorageType.String) return param.AsString() ?? string.Empty;
            return param.AsValueString() ?? string.Empty;
        }
    }
}
