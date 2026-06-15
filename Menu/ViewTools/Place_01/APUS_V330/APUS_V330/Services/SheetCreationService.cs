// File: Services/SheetCreationService.cs
using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.APUS_V330.Services
{
    /// <summary>
    /// Creates Revit sheets. Caller must have an active transaction.
    /// </summary>
    public class SheetCreationService
    {
        private readonly Document          _doc;
        private readonly SheetNumberService _sheetNumberService;

        public SheetCreationService(Document doc)
        {
            _doc                = doc ?? throw new ArgumentNullException(nameof(doc));
            _sheetNumberService = new SheetNumberService(doc);
        }

        /// <summary>Creates a sheet with an auto-generated unique number.</summary>
        public ViewSheet Create(FamilySymbol titleBlock, string baseSheetNumber = "AP")
        {
            ValidateTransaction();
            if (titleBlock == null) throw new ArgumentNullException(nameof(titleBlock));
            if (!titleBlock.IsActive) titleBlock.Activate();

            var sheet = ViewSheet.Create(_doc, titleBlock.Id);
            sheet.SheetNumber = _sheetNumberService.GetNextAvailableSheetNumber(baseSheetNumber);
            sheet.Name        = $"APUS-{sheet.SheetNumber}";
            return sheet;
        }

        /// <summary>Creates a sheet with a specific number. Number must be unique.</summary>
        public ViewSheet Create(FamilySymbol titleBlock, string sheetNumber, string sheetName)
        {
            ValidateTransaction();
            if (titleBlock == null)               throw new ArgumentNullException(nameof(titleBlock));
            if (string.IsNullOrWhiteSpace(sheetNumber)) throw new ArgumentException("Sheet number cannot be empty", nameof(sheetNumber));
            if (_sheetNumberService.SheetNumberExists(sheetNumber))
                throw new InvalidOperationException($"Sheet number '{sheetNumber}' already exists.");

            if (!titleBlock.IsActive) titleBlock.Activate();

            var sheet = ViewSheet.Create(_doc, titleBlock.Id);
            sheet.SheetNumber = sheetNumber;
            sheet.Name        = sheetName;
            return sheet;
        }

        private void ValidateTransaction()
        {
            if (!_doc.IsModifiable)
                throw new InvalidOperationException(
                    "SheetCreationService requires an active transaction.");
        }
    }
}
