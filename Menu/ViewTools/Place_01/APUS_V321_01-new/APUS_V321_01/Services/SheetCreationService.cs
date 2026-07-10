// File: SheetCreationService.cs
using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.APUS_V321_01.Services
{
    /// <summary>
    /// Creates Revit sheets. Does NOT manage transactions — the caller must
    /// have an active transaction before calling any method. In V321 sheet
    /// creation itself stays a single transaction; per-viewport placement
    /// inside that sheet uses SubTransaction isolation (see
    /// SectionPlacementHandler) so one viewport failure doesn't roll back
    /// the whole sheet.
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

        /// <summary>Creates a new sheet with auto-generated unique sheet number in "{prefix}-{3-digit}" form.</summary>
        public ViewSheet Create(FamilySymbol titleBlock, string baseSheetNumber = "SEC")
        {
            ValidateTransaction();

            if (titleBlock == null)
                throw new ArgumentNullException(nameof(titleBlock));

            if (!titleBlock.IsActive)
                titleBlock.Activate();

            var sheet = ViewSheet.Create(_doc, titleBlock.Id);

            sheet.SheetNumber = _sheetNumberService.GetNextAvailableSheetNumber(baseSheetNumber);
            sheet.Name = $"APUS-{sheet.SheetNumber}";

            return sheet;
        }

        /// <summary>Creates a new sheet with a specific number (must be unique). REQUIRES active transaction.</summary>
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
