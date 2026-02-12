// File: SheetCreationService.cs
// FIXED: Removed duplicate SheetNumberService instantiation
using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.APUS_V317.Services
{
    /// <summary>
    /// Service for creating Revit sheets.
    /// CRITICAL: This service does NOT manage transactions.
    /// Caller must have an active transaction before calling any method.
    /// </summary>
    public class SheetCreationService
    {
        private readonly Document _doc;
        private readonly SheetNumberService _sheetNumberService;

        public SheetCreationService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetNumberService = new SheetNumberService(doc);
        }

        /// <summary>
        /// Creates a new sheet with auto-generated unique sheet number.
        /// REQUIRES: Active transaction.
        /// </summary>
        public ViewSheet Create(FamilySymbol titleBlock, string baseSheetNumber = "AP")
        {
            ValidateTransaction();

            if (titleBlock == null)
                throw new ArgumentNullException(nameof(titleBlock));

            if (!titleBlock.IsActive)
                titleBlock.Activate();

            var sheet = ViewSheet.Create(_doc, titleBlock.Id);

            // Generate unique sheet number
            sheet.SheetNumber = _sheetNumberService.GetNextAvailableSheetNumber(baseSheetNumber);
            sheet.Name = $"APUS-{sheet.SheetNumber}";

            return sheet;
        }

        /// <summary>
        /// Creates a new sheet with specific number (must be unique).
        /// REQUIRES: Active transaction.
        /// </summary>
        public ViewSheet Create(FamilySymbol titleBlock, string sheetNumber, string sheetName)
        {
            ValidateTransaction();

            if (titleBlock == null)
                throw new ArgumentNullException(nameof(titleBlock));

            if (string.IsNullOrWhiteSpace(sheetNumber))
                throw new ArgumentException("Sheet number cannot be empty", nameof(sheetNumber));

            if (_sheetNumberService.SheetNumberExists(sheetNumber))
                throw new InvalidOperationException($"Sheet number '{sheetNumber}' already exists.");

            if (!titleBlock.IsActive)
                titleBlock.Activate();

            var sheet = ViewSheet.Create(_doc, titleBlock.Id);
            sheet.SheetNumber = sheetNumber;
            sheet.Name = sheetName;

            return sheet;
        }

        private void ValidateTransaction()
        {
            if (!_doc.IsModifiable)
                throw new InvalidOperationException(
                    "SheetCreationService requires an active transaction. " +
                    "Caller must start a transaction before calling this method.");
        }
    }
}