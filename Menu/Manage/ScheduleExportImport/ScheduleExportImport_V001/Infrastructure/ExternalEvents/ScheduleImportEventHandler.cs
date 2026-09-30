using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Models;

namespace Revit26_Plugin.ScheduleExportImport.V001.Infrastructure.ExternalEvents
{
    public enum ScheduleImportRequest
    {
        LoadSchedules,
        ExportSchedule,
        ImportSchedule
    }

    /// <summary>
    /// Handles all Revit-API work for this tool:
    ///   LoadSchedules  — collects all schedule views in the document
    ///   ExportSchedule — reads fields + rows from a ViewSchedule, delivers data to ViewModel
    ///   ImportSchedule — writes edited parameter values back to elements via a Transaction
    /// </summary>
    public class ScheduleImportEventHandler : IExternalEventHandler
    {
        private readonly Document _doc;

        public ScheduleImportRequest Request { get; set; }

        // ---- ExportSchedule inputs ----
        public ElementId TargetScheduleId { get; set; }

        // ---- ImportSchedule inputs ----
        public List<ScheduleRow> RowsToImport { get; set; } = new List<ScheduleRow>();
        public List<string> WritableHeaders { get; set; } = new List<string>();

        // ---- Outputs (read by ViewModel after RequestCompleted fires) ----
        public List<ScheduleViewInfo> LoadedSchedules { get; private set; } = new List<ScheduleViewInfo>();
        public List<string> ExportedHeaders { get; private set; } = new List<string>();
        public List<ScheduleRow> ExportedRows { get; private set; } = new List<ScheduleRow>();
        public List<ScheduleFieldInfo> ExportedFields { get; private set; } = new List<ScheduleFieldInfo>();
        public ImportResult LastImportResult { get; private set; } = new ImportResult();
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
                    case ScheduleImportRequest.LoadSchedules:
                        ExecuteLoadSchedules();
                        break;
                    case ScheduleImportRequest.ExportSchedule:
                        ExecuteExportSchedule();
                        break;
                    case ScheduleImportRequest.ImportSchedule:
                        ExecuteImportSchedule();
                        break;
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
            var schedules = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .Where(vs => !vs.IsTemplate && !vs.IsTitleblockRevisionSchedule)
                .OrderBy(vs => vs.Name)
                .ToList();

            LoadedSchedules = schedules.Select(vs =>
            {
                var tableDef = vs.Definition;
                int fieldCount = tableDef.GetFieldCount();
                int rowCount = 0;
                try { rowCount = vs.GetTableData().GetSectionData(SectionType.Body).NumberOfRows; }
                catch { }

                return new ScheduleViewInfo
                {
                    ViewId = vs.Id,
                    Name = vs.Name,
                    CategoryName = vs.Definition.CategoryId != ElementId.InvalidElementId
                        ? Category.GetCategory(_doc, vs.Definition.CategoryId)?.Name ?? "Unknown"
                        : "Multi-Category",
                    RowCount = Math.Max(0, rowCount - 1), // subtract header row
                    FieldCount = fieldCount
                };
            }).ToList();
        }

        // ── Read schedule data into rows ──────────────────────────────────────
        private void ExecuteExportSchedule()
        {
            var schedule = _doc.GetElement(TargetScheduleId) as ViewSchedule;
            if (schedule == null)
                throw new InvalidOperationException("Schedule view not found in document.");

            var tableDef = schedule.Definition;
            int fieldCount = tableDef.GetFieldCount();

            var fields = new List<ScheduleFieldInfo>();
            var headers = new List<string>();

            for (int i = 0; i < fieldCount; i++)
            {
                var field = tableDef.GetField(i);
                if (field.IsHidden) continue;

                var header = field.ColumnHeading;
                if (string.IsNullOrWhiteSpace(header))
                    header = field.GetName();

                // Make header unique if duplicated
                var uniqueHeader = header;
                int dup = 1;
                while (headers.Contains(uniqueHeader))
                    uniqueHeader = $"{header} ({dup++})";

                headers.Add(uniqueHeader);
                fields.Add(new ScheduleFieldInfo
                {
                    Header = uniqueHeader,
                    FieldId = field.FieldId,
                    StorageType = StorageType.String, // actual type resolved per-element at import time
                    IsReadOnly = IsFieldReadOnly(field)
                });
            }

            // Collect elements that appear in this schedule using FilteredElementCollector
            // with the schedule's own filter — same set Revit shows in the schedule view.
            var scheduleElements = new FilteredElementCollector(_doc, schedule.Id)
                .WhereElementIsNotElementType()
                .ToList();

            // Build a lookup: ElementId → row values, reading params directly from the element.
            // For the cell text we use schedule.GetCellText but we need the row index per element.
            // The safest approach for Revit 2026 is to read parameter values directly from elements.
            var rows = new List<ScheduleRow>();

            foreach (var elem in scheduleElements)
            {
                var row = new ScheduleRow { ElementId = elem.Id.Value };

                foreach (var fi in fields)
                {
                    string cellValue = string.Empty;
                    try
                    {
                        // Try to find the parameter by name on the element
                        var param = FindParameterByName(elem, fi.Header);
                        if (param != null)
                            cellValue = GetParameterDisplayValue(param);
                    }
                    catch { }
                    row.Values[fi.Header] = cellValue;
                }
                rows.Add(row);
            }

            ExportedHeaders = headers;
            ExportedRows = rows;
            ExportedFields = fields;
        }

        // ── Write edited rows back to elements ────────────────────────────────
        private void ExecuteImportSchedule()
        {
            var result = new ImportResult();
            var notFoundIds = new List<long>();

            using var txGroup = new TransactionGroup(_doc, "Schedule Import V001");
            txGroup.Start();

            foreach (var row in RowsToImport)
            {
                var elementId = new ElementId((int)row.ElementId);
                var elem = _doc.GetElement(elementId);
                if (elem == null)
                {
                    result.ElementsNotFound++;
                    notFoundIds.Add(row.ElementId);
                    continue;
                }

                bool anyWritten = false;
                using var tx = new Transaction(_doc, $"Update element {row.ElementId}");
                tx.Start();
                try
                {
                    foreach (var header in WritableHeaders)
                    {
                        if (!row.Values.TryGetValue(header, out var newValue)) continue;

                        var param = FindParameterByName(elem, header);
                        if (param == null || param.IsReadOnly)
                        {
                            result.ParametersSkipped++;
                            continue;
                        }

                        try
                        {
                            WriteParameter(param, newValue);
                            result.ParametersWritten++;
                            anyWritten = true;
                        }
                        catch
                        {
                            result.ParametersSkipped++;
                        }
                    }
                    tx.Commit();
                    if (anyWritten) result.ElementsUpdated++;
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    result.Errors++;
                    if (result.Errors == 1)
                        result.ErrorDetail = $"Element {row.ElementId}: {ex.Message}";
                }
            }

            if (result.Errors == 0)
                txGroup.Assimilate();
            else
                txGroup.Assimilate(); // keep partial successes

            if (notFoundIds.Count > 0 && string.IsNullOrEmpty(result.ErrorDetail))
                result.ErrorDetail = $"Elements not found: {string.Join(", ", notFoundIds.Take(5))}" +
                    (notFoundIds.Count > 5 ? $"... (+{notFoundIds.Count - 5} more)" : "");

            LastImportResult = result;
        }

        private static Parameter FindParameterByName(Element elem, string name)
        {
            // Try by name match; prefer instance params
            foreach (Parameter p in elem.Parameters)
            {
                if (string.Equals(p.Definition.Name, name, StringComparison.OrdinalIgnoreCase))
                    return p;
            }
            return null;
        }

        private static void WriteParameter(Parameter param, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            switch (param.StorageType)
            {
                case StorageType.String:
                    param.Set(value);
                    break;
                case StorageType.Integer:
                    if (int.TryParse(value, out int intVal))
                        param.Set(intVal);
                    break;
                case StorageType.Double:
                    if (double.TryParse(value, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out double dblVal))
                        param.Set(dblVal);
                    break;
                case StorageType.ElementId:
                    if (int.TryParse(value, out int idVal))
                        param.Set(new ElementId(idVal));
                    break;
            }
        }

        private static string GetParameterDisplayValue(Parameter param)
        {
            if (param.StorageType == StorageType.None) return string.Empty;
            if (param.StorageType == StorageType.String) return param.AsString() ?? string.Empty;
            // Use AsValueString for numbers/element ids so units are shown the same way
            // the schedule would display them.
            return param.AsValueString() ?? string.Empty;
        }

        private static bool IsFieldReadOnly(ScheduleField field)
        {
            // Formula fields and certain built-in fields are read-only
            return field.IsCalculatedField || field.FieldType == ScheduleFieldType.Formula;
        }
    }
}
