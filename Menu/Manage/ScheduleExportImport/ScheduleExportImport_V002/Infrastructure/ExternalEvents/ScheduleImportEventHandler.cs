using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.ScheduleExportImport.V002.Core.Models;
using Revit26_Plugin.ScheduleExportImport.V002.Core.Services;

namespace Revit26_Plugin.ScheduleExportImport.V002.Infrastructure.ExternalEvents
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
        /// <summary>Warnings from the last export (e.g. table values that could not be read).</summary>
        public List<string> ExportNotes { get; private set; } = new List<string>();
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

        public string GetName() => "Schedule Export/Import V002";

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
        // The API has no "table row → element" lookup, so to read formula / room / combined
        // columns we tag each element's Comments with this marker inside a transaction that
        // is always rolled back, and match the schedule's table rows by it.
        private const string RowMarker = "§RBX§";

        private enum ColumnSource { Parameter, Count, Table }

        private sealed class ExportColumn
        {
            public string Header;
            public int FieldId;
            public ScheduleFieldType FieldType;
            public ElementId ParameterId;
            public ColumnSource Source;
        }

        private void ExecuteExportSchedule()
        {
            var schedule = GetSchedule();
            var def = schedule.Definition;
            var notes = new List<string>();

            var columns = new List<ExportColumn>();
            foreach (var fieldId in def.GetFieldOrder())
            {
                var field = def.GetField(fieldId);
                if (field.IsHidden) continue;

                // Parameter name, not ColumnHeading: import matches Excel headers back to
                // parameters by name, so a renamed heading ("W" for "Width") would never match.
                var header = field.GetName();
                var unique = header;
                int dup = 1;
                while (columns.Any(c => c.Header == unique))
                    unique = $"{header} ({dup++})";

                columns.Add(new ExportColumn
                {
                    Header = unique,
                    FieldId = fieldId.IntegerValue,
                    FieldType = field.FieldType,
                    ParameterId = field.ParameterId,
                    Source = GetColumnSource(field)
                });
            }

            var elements = CollectScheduleElements(schedule);

            // Formula / room / combined fields have no parameter on the element itself —
            // take the text Revit shows in the schedule table instead.
            List<long> tableOrder = null;
            Dictionary<long, Dictionary<int, string>> tableValues = null;
            var tableColumns = columns.Where(c => c.Source == ColumnSource.Table).ToList();
            if (tableColumns.Count > 0)
            {
                try
                {
                    tableValues = ReadScheduleTable(schedule, elements, tableColumns.Select(c => c.FieldId).ToList(), out tableOrder);
                }
                catch (Exception ex)
                {
                    notes.Add($"Could not read calculated columns from the schedule table ({ex.Message}) — they are left blank: " +
                              string.Join(", ", tableColumns.Select(c => c.Header)));
                }
            }

            var editable = new HashSet<string>();
            var rowsByElement = new List<(Element Elem, ScheduleRow Row)>();
            int tableMisses = 0;

            foreach (var elem in elements)
            {
                var row = new ScheduleRow { ElementId = elem.Id.Value };
                Dictionary<int, string> elemTable = null;
                if (tableValues != null && !tableValues.TryGetValue(elem.Id.Value, out elemTable))
                    tableMisses++;

                foreach (var col in columns)
                {
                    string value = string.Empty;
                    try
                    {
                        switch (col.Source)
                        {
                            case ColumnSource.Count:
                                value = "1"; // one row per element
                                break;
                            case ColumnSource.Table:
                                if (elemTable != null && elemTable.TryGetValue(col.FieldId, out var text))
                                    value = text;
                                break;
                            default:
                                var param = FindFieldParameter(elem, col.FieldType, col.ParameterId);
                                if (param != null)
                                {
                                    value = GetParameterDisplayValue(param);
                                    if (IsImportWritable(elem, col, param))
                                        editable.Add(col.Header);
                                }
                                break;
                        }
                    }
                    catch { }
                    row.Values[col.Header] = value ?? string.Empty;
                }
                rowsByElement.Add((elem, row));
            }

            if (tableMisses > 0)
                notes.Add($"{tableMisses} element(s) could not be matched to a schedule table row (Comments not editable for them) — " +
                          $"calculated columns are blank for those rows: {string.Join(", ", tableColumns.Select(c => c.Header))}");

            var rows = OrderLikeSchedule(def, rowsByElement, tableOrder);

            ExportedHeaders = columns.Select(c => c.Header).ToList();
            ExportedRows = rows;
            EditableHeaders = editable;
            ExportNotes = notes;
        }

        private static ColumnSource GetColumnSource(ScheduleField field)
        {
            if (field.FieldType == ScheduleFieldType.Count) return ColumnSource.Count;
            if (!field.IsCalculatedField && !field.IsCombinedParameterField
                && (field.FieldType == ScheduleFieldType.Instance || field.FieldType == ScheduleFieldType.ElementType)
                && field.ParameterId != ElementId.InvalidElementId)
                return ColumnSource.Parameter;
            return ColumnSource.Table;
        }

        /// <summary>
        /// The exact parameter the schedule field points at (by id, not by name), looking at
        /// the instance first for instance fields and the type first for type fields.
        /// </summary>
        private Parameter FindFieldParameter(Element elem, ScheduleFieldType fieldType, ElementId parameterId)
        {
            var type = _doc.GetElement(elem.GetTypeId());
            bool typeFirst = fieldType == ScheduleFieldType.ElementType;
            return FindParameter(typeFirst ? type : elem, parameterId)
                   ?? FindParameter(typeFirst ? elem : type, parameterId);
        }

        private Parameter FindParameter(Element e, ElementId parameterId)
        {
            if (e == null) return null;
            if (parameterId.Value < 0) return e.get_Parameter((BuiltInParameter)(int)parameterId.Value);
            if (_doc.GetElement(parameterId) is SharedParameterElement shared) return e.get_Parameter(shared.GuidValue);
            foreach (Parameter p in e.Parameters)
                if (p.Id == parameterId) return p;
            return null;
        }

        // Green only when import (which looks parameters up by header name) would land on
        // this same instance parameter — avoids writing to a same-named wrong parameter.
        private static bool IsImportWritable(Element elem, ExportColumn col, Parameter param)
            => col.FieldType == ScheduleFieldType.Instance
               && !param.IsReadOnly
               && param.Element?.Id == elem.Id
               && elem.LookupParameter(col.Header)?.Id == param.Id;

        private Dictionary<long, Dictionary<int, string>> ReadScheduleTable(
            ViewSchedule schedule, List<Element> elements, List<int> wantedFieldIds, out List<long> order)
        {
            order = new List<long>();
            var result = new Dictionary<long, Dictionary<int, string>>();
            var def = schedule.Definition;
            var commentsId = new ElementId(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);

            using var tx = new Transaction(_doc, "Read schedule table (rolled back)");
            tx.Start();
            try
            {
                // One body row per element, no group headers / footers / totals.
                def.IsItemized = true;
                def.ShowGrandTotal = false;
                var sortFields = def.GetSortGroupFields();
                foreach (var s in sortFields)
                {
                    s.ShowHeader = false;
                    s.ShowFooter = false;
                    s.ShowBlankLine = false;
                }
                def.SetSortGroupFields(sortFields);

                int markerFieldId = -1;
                foreach (var id in def.GetFieldOrder())
                {
                    var f = def.GetField(id);
                    if (f.FieldType == ScheduleFieldType.Instance && !f.IsCalculatedField
                        && !f.IsCombinedParameterField && f.ParameterId == commentsId)
                    {
                        f.IsHidden = false;
                        markerFieldId = id.IntegerValue;
                        break;
                    }
                }
                if (markerFieldId < 0)
                    markerFieldId = def.AddField(ScheduleFieldType.Instance, commentsId).FieldId.IntegerValue;

                foreach (var elem in elements)
                {
                    var p = elem.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                    if (p == null || p.IsReadOnly) continue;
                    try { p.Set(RowMarker + elem.Id.Value); } catch { }
                }

                _doc.Regenerate();
                schedule.RefreshData();

                var visible = def.GetFieldOrder()
                    .Where(id => !def.GetField(id).IsHidden)
                    .Select(id => id.IntegerValue)
                    .ToList();
                var body = schedule.GetTableData().GetSectionData(SectionType.Body);
                int firstCol = body.FirstColumnNumber;
                int markerCol = firstCol + visible.IndexOf(markerFieldId);
                var wantedCols = wantedFieldIds
                    .Select(id => (FieldId: id, Col: visible.IndexOf(id)))
                    .Where(x => x.Col >= 0)
                    .ToList();

                for (int r = body.FirstRowNumber; r <= body.LastRowNumber; r++)
                {
                    var marker = schedule.GetCellText(SectionType.Body, r, markerCol);
                    if (string.IsNullOrEmpty(marker) || !marker.StartsWith(RowMarker, StringComparison.Ordinal)) continue;
                    if (!long.TryParse(marker.Substring(RowMarker.Length), out var elemId) || result.ContainsKey(elemId)) continue;

                    var values = new Dictionary<int, string>();
                    foreach (var (fieldId, col) in wantedCols)
                        values[fieldId] = schedule.GetCellText(SectionType.Body, r, firstCol + col) ?? string.Empty;
                    result[elemId] = values;
                    order.Add(elemId);
                }
            }
            finally
            {
                // Nothing in the model is ever kept from this.
                if (tx.GetStatus() == TransactionStatus.Started) tx.RollBack();
            }
            return result;
        }

        /// <summary>
        /// Rows in the schedule's own order: the table order when it was read, otherwise the
        /// schedule's sorting/grouping fields applied to the parameter values, then Element ID.
        /// </summary>
        private List<ScheduleRow> OrderLikeSchedule(ScheduleDefinition def,
            List<(Element Elem, ScheduleRow Row)> rows, List<long> tableOrder)
        {
            var keys = new List<(Func<Element, object> Key, bool Descending)>();
            foreach (var s in def.GetSortGroupFields())
            {
                var field = def.GetField(s.FieldId);
                if (GetColumnSource(field) != ColumnSource.Parameter) continue;
                var fieldType = field.FieldType;
                var paramId = field.ParameterId;
                keys.Add((e => SortKey(FindFieldParameter(e, fieldType, paramId)), s.SortOrder == ScheduleSortOrder.Descending));
            }

            IEnumerable<(Element Elem, ScheduleRow Row)> ordered = rows;
            if (keys.Count > 0)
            {
                var cmp = new SortKeyComparer();
                IOrderedEnumerable<(Element Elem, ScheduleRow Row)> o = null;
                foreach (var (key, desc) in keys)
                {
                    if (o == null)
                        o = desc ? rows.OrderByDescending(x => key(x.Elem), cmp) : rows.OrderBy(x => key(x.Elem), cmp);
                    else
                        o = desc ? o.ThenByDescending(x => key(x.Elem), cmp) : o.ThenBy(x => key(x.Elem), cmp);
                }
                ordered = o.ThenBy(x => x.Row.ElementId);
            }
            else
            {
                ordered = rows.OrderBy(x => x.Row.ElementId);
            }

            if (tableOrder != null)
            {
                var rank = new Dictionary<long, int>();
                for (int i = 0; i < tableOrder.Count; i++) rank[tableOrder[i]] = i;
                // OrderBy is stable: rows missing from the table keep the sorted order, at the end.
                ordered = ordered.OrderBy(x => rank.TryGetValue(x.Row.ElementId, out var i) ? i : int.MaxValue);
            }

            return ordered.Select(x => x.Row).ToList();
        }

        private static object SortKey(Parameter p)
        {
            if (p == null || !p.HasValue) return null;
            switch (p.StorageType)
            {
                case StorageType.Double: return p.AsDouble();
                case StorageType.Integer: return (double)p.AsInteger();
                case StorageType.String: return p.AsString();
                default: return p.AsValueString();
            }
        }

        /// <summary>Numbers numerically, text "naturally" (Door 2 before Door 10), blanks first.</summary>
        private sealed class SortKeyComparer : IComparer<object>
        {
            public int Compare(object a, object b)
            {
                bool aBlank = a == null || (a is string sa && sa.Length == 0);
                bool bBlank = b == null || (b is string sb && sb.Length == 0);
                if (aBlank || bBlank) return aBlank == bBlank ? 0 : aBlank ? -1 : 1;
                if (a is double da && b is double db) return da.CompareTo(db);
                return NaturalCompare(a.ToString(), b.ToString());
            }

            private static int NaturalCompare(string x, string y)
            {
                int i = 0, j = 0;
                while (i < x.Length && j < y.Length)
                {
                    if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
                    {
                        int si = i, sj = j;
                        while (i < x.Length && char.IsDigit(x[i])) i++;
                        while (j < y.Length && char.IsDigit(y[j])) j++;
                        var nx = x.Substring(si, i - si).TrimStart('0');
                        var ny = y.Substring(sj, j - sj).TrimStart('0');
                        int c = nx.Length != ny.Length ? nx.Length.CompareTo(ny.Length) : string.CompareOrdinal(nx, ny);
                        if (c != 0) return c;
                    }
                    else
                    {
                        int c = char.ToUpperInvariant(x[i]).CompareTo(char.ToUpperInvariant(y[j]));
                        if (c != 0) return c;
                        i++; j++;
                    }
                }
                return (x.Length - i).CompareTo(y.Length - j);
            }
        }

        // ── Analyze (read-only) ────────────────────────────────────────────────
        private void ExecuteAnalyzeImport()
        {
            var file = ImportFile ?? throw new InvalidOperationException("No import file was loaded.");
            var analysis = new ImportAnalysis { RowsInFile = file.Rows.Count };

            var dedup = ImportRowValidator.Deduplicate(file.Rows);
            analysis.DuplicateRowsMerged = dedup.MergedDuplicateRows;
            foreach (var conflict in dedup.Conflicts)
            {
                var e = _doc.GetElement(new ElementId(conflict.ElementId));
                if (e != null) conflict.ElementName = GetElementName(e);
                analysis.Items.Add(conflict);
            }

            foreach (var row in dedup.UniqueRows)
            {
                var elem = _doc.GetElement(new ElementId(row.ElementId));
                if (elem == null)
                {
                    analysis.Items.Add(new ImportChange
                    {
                        ElementId = row.ElementId,
                        ElementName = "—",
                        Status = ImportChangeStatus.NotFound,
                        Message = "No element with this ID in the model (deleted, or file from another model)."
                    });
                    continue;
                }

                var elemName = GetElementName(elem);
                analysis.ElementsMatched++;
                foreach (var header in file.Headers)
                {
                    if (!row.Values.TryGetValue(header, out var newValue)) continue;

                    var param = elem.LookupParameter(header);
                    if (param == null)
                    {
                        var typeParam = GetTypeParameter(elem, header);
                        if (typeParam != null && !SameValue(GetParameterDisplayValue(typeParam), newValue))
                            analysis.Items.Add(NewItem(row.ElementId, elemName, header, typeParam, newValue, ImportChangeStatus.TypeParameter,
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
                        ? NewItem(row.ElementId, elemName, header, param, newValue, ImportChangeStatus.ReadOnly, "Read-only in Revit — cannot be changed.")
                        : NewItem(row.ElementId, elemName, header, param, newValue, ImportChangeStatus.Change, string.Empty));
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
                        ElementName = GetElementName(elem),
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
            using var group = new TransactionGroup(_doc, "Schedule Import V002");
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

        private static ImportChange NewItem(long id, string elementName, string header, Parameter param, string newValue, ImportChangeStatus status, string message)
            => new ImportChange
            {
                ElementId = id,
                ElementName = elementName,
                ParameterName = header,
                OldValue = GetParameterDisplayValue(param),
                NewValue = newValue,
                Status = status,
                Message = message
            };

        private static string GetElementName(Element elem)
        {
            try { return elem.Name ?? string.Empty; }
            catch { return string.Empty; }
        }

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
