// File: SheetNumberService.cs
// NEW - Manages sheet number generation and uniqueness
using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Revit26_Plugin.APUS_V318.Services
{
    /// <summary>
    /// Service for managing sheet numbers and ensuring uniqueness.
    /// Read-only operations can be called anytime.
    /// Write operations require active transaction.
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

        /// <summary>
        /// Refresh the cache of existing sheet numbers.
        /// Call after any sheet deletion.
        /// </summary>
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

        /// <summary>
        /// Checks if a sheet number already exists.
        /// </summary>
        public bool SheetNumberExists(string sheetNumber)
        {
            return _existingSheetNumbers.Contains(sheetNumber?.Trim() ?? string.Empty);
        }

        /// <summary>
        /// Gets the next available sheet number for a given prefix.
        /// Example: "AP" -> AP001, AP002, etc.
        /// </summary>
        public string GetNextAvailableSheetNumber(string prefix = "AP", int startIndex = 1)
        {
            if (string.IsNullOrWhiteSpace(prefix))
                prefix = "AP";

            prefix = prefix.Trim();

            // Get the next number for this prefix
            if (!_nextNumberByPrefix.TryGetValue(prefix, out int nextNumber))
            {
                nextNumber = GetMaxNumberForPrefix(prefix, startIndex);
                _nextNumberByPrefix[prefix] = nextNumber;
            }

            string candidate;
            do
            {
                candidate = $"{prefix}{nextNumber:D3}";
                nextNumber++;
            }
            while (_existingSheetNumbers.Contains(candidate));

            // Cache the next number to use
            _nextNumberByPrefix[prefix] = nextNumber;

            return candidate;
        }

        /// <summary>
        /// Reserves a sheet number (call before creating sheet).
        /// </summary>
        public bool TryReserveSheetNumber(string sheetNumber)
        {
            if (string.IsNullOrWhiteSpace(sheetNumber) || _existingSheetNumbers.Contains(sheetNumber))
                return false;

            _existingSheetNumbers.Add(sheetNumber);
            return true;
        }

        private int GetMaxNumberForPrefix(string prefix, int defaultStart)
        {
            var pattern = $"^{Regex.Escape(prefix)}(\\d+)$";
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