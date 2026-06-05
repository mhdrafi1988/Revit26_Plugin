// File: Services/SheetNumberService.cs
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Revit26_Plugin.APUS_V330.Services
{
    public class SheetNumberService
    {
        private readonly Document          _doc;
        private HashSet<string>            _existingSheetNumbers;
        private Dictionary<string, int>    _nextNumberByPrefix;

        public SheetNumberService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            RefreshCache();
        }

        public void RefreshCache()
        {
            _existingSheetNumbers = new HashSet<string>(
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .Select(s => s.SheetNumber)
                    .Where(s => !string.IsNullOrEmpty(s)),
                StringComparer.OrdinalIgnoreCase);

            _nextNumberByPrefix = new Dictionary<string, int>();
        }

        public bool SheetNumberExists(string sheetNumber)
            => _existingSheetNumbers.Contains(sheetNumber?.Trim() ?? string.Empty);

        public string GetNextAvailableSheetNumber(string prefix = "AP", int startIndex = 1)
        {
            if (string.IsNullOrWhiteSpace(prefix)) prefix = "AP";
            prefix = prefix.Trim();

            if (!_nextNumberByPrefix.TryGetValue(prefix, out int nextNumber))
                nextNumber = GetMaxNumberForPrefix(prefix, startIndex);

            string candidate;
            do
            {
                candidate = $"{prefix}{nextNumber:D3}";
                nextNumber++;
            }
            while (_existingSheetNumbers.Contains(candidate));

            _nextNumberByPrefix[prefix] = nextNumber;
            return candidate;
        }

        public bool TryReserveSheetNumber(string sheetNumber)
        {
            if (string.IsNullOrWhiteSpace(sheetNumber) || _existingSheetNumbers.Contains(sheetNumber))
                return false;
            _existingSheetNumbers.Add(sheetNumber);
            return true;
        }

        private int GetMaxNumberForPrefix(string prefix, int defaultStart)
        {
            var pattern  = $"^{Regex.Escape(prefix)}(\\d+)$";
            var regex    = new Regex(pattern, RegexOptions.IgnoreCase);
            int maxNumber = defaultStart - 1;

            foreach (var sn in _existingSheetNumbers)
            {
                var match = regex.Match(sn);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int number))
                    maxNumber = Math.Max(maxNumber, number);
            }
            return maxNumber + 1;
        }
    }
}
