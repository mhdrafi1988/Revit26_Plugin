// File: SheetNumberService.cs
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Revit26_Plugin.APUS.V322.Services
{
    /// <summary>
    /// Generates sheet numbers as {prefix}-{3-digit} (e.g. SEC-001) and
    /// guarantees uniqueness by skipping any number that already exists in
    /// the model, per Rafi's decision. Read-only operations can be called
    /// anytime; TryReserveSheetNumber should be called before sheet creation
    /// so concurrent candidates in the same run don't collide.
    /// </summary>
    public class SheetNumberService
    {
        private readonly Document _doc;
        private HashSet<string> _existingSheetNumbers;
        private Dictionary<string, int> _nextNumberByPrefix;

        public SheetNumberService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            RefreshCache();
        }

        /// <summary>Refresh the cache of existing sheet numbers. Call after any sheet deletion.</summary>
        public void RefreshCache()
        {
            _existingSheetNumbers = new HashSet<string>(
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Select(s => s.SheetNumber)
                    .Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase
            );

            _nextNumberByPrefix = new Dictionary<string, int>();
        }

        public bool SheetNumberExists(string sheetNumber)
        {
            return _existingSheetNumbers.Contains(sheetNumber?.Trim() ?? string.Empty);
        }

        /// <summary>
        /// Gets the next available sheet number for a given prefix, in the
        /// format "{prefix}-{number:D3}" (e.g. SEC-001, SEC-002...).
        /// Skips any candidate already present in the model.
        /// </summary>
        public string GetNextAvailableSheetNumber(string prefix = "SEC", int startIndex = 1)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "SEC";

            prefix = prefix.Trim();

            if (!_nextNumberByPrefix.TryGetValue(prefix, out int nextNumber))
            {
                nextNumber = GetMaxNumberForPrefix(prefix, startIndex);
                _nextNumberByPrefix[prefix] = nextNumber;
            }

            string candidate;
            do
            {
                candidate = $"{prefix}-{nextNumber:D3}";
                nextNumber++;
            }
            while (_existingSheetNumbers.Contains(candidate));

            _nextNumberByPrefix[prefix] = nextNumber;

            return candidate;
        }

        /// <summary>Reserves a sheet number (call before creating sheet) so it isn't reused within the same run.</summary>
        public bool TryReserveSheetNumber(string sheetNumber)
        {
            if (string.IsNullOrWhiteSpace(sheetNumber) || _existingSheetNumbers.Contains(sheetNumber))
                return false;

            _existingSheetNumbers.Add(sheetNumber);
            return true;
        }

        private int GetMaxNumberForPrefix(string prefix, int defaultStart)
        {
            // Matches "{prefix}-{digits}" — the V321 dash-delimited format.
            var pattern = $"^{Regex.Escape(prefix)}-(\\d+)$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);

            var maxNumber = defaultStart - 1;

            foreach (var sheetNumber in _existingSheetNumbers)
            {
                var match = regex.Match(sheetNumber);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                {
                    maxNumber = Math.Max(maxNumber, number);
                }
            }

            return maxNumber + 1;
        }
    }
}
