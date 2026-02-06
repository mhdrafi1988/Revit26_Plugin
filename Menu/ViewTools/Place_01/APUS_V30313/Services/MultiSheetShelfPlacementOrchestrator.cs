using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V313.Models;
using Revit26_Plugin.APUS_V313.ViewModels;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V313.Services
{
    /// <summary>
    /// Orchestrates placement across multiple sheets.
    /// Creates new sheets as needed.
    /// </summary>
    public class MultiSheetShelfPlacementOrchestrator
    {
        private readonly Document _document;
        private readonly ShelfPlacementService _placementService;
        private readonly SheetCreationService _sheetCreator;

        public MultiSheetShelfPlacementOrchestrator(Document document)
        {
            _document = document ?? throw new ArgumentNullException(nameof(document));
            _placementService = new ShelfPlacementService(document);
            _sheetCreator = new SheetCreationService(document);
        }

        /// <summary>
        /// Places sections across multiple sheets using shelf layout.
        /// Returns placement summary with all created sheets.
        /// </summary>
        public MultiSheetPlacementSummary PlaceSections(
            IList<SectionItemViewModel> sortedSections,  // PRE-SORTED list (never re-sorted)
            FamilySymbol titleBlock,
            SheetPlacementArea placementArea,
            double horizontalGapMm,
            double verticalGapMm)
        {
            if (sortedSections == null || sortedSections.Count == 0)
            {
                return new MultiSheetPlacementSummary
                {
                    Success = false,
                    Message = "No sections to place."
                };
            }

            if (titleBlock == null)
            {
                return new MultiSheetPlacementSummary
                {
                    Success = false,
                    Message = "No title block selected."
                };
            }

            var summary = new MultiSheetPlacementSummary();
            int currentIndex = 0;
            int sheetNumber = 1;

            while (currentIndex < sortedSections.Count)
            {
                // Create new sheet
                ViewSheet sheet = _sheetCreator.Create(titleBlock, sheetNumber++);
                if (sheet == null)
                {
                    summary.Message = "Failed to create sheet.";
                    summary.Success = false;
                    return summary;
                }

                summary.CreatedSheets.Add(sheet);

                // Place sections on this sheet
                var result = _placementService.PlaceOnSheet(
                    sheet,
                    sortedSections,
                    currentIndex,
                    placementArea,
                    horizontalGapMm,
                    verticalGapMm);

                // Update summary
                summary.TotalPlaced += result.PlacedCount;
                summary.TotalFailed += result.FailedCount;
                summary.AllPlacedViewIds.AddRange(result.PlacedViewIds);

                // Move to next section
                currentIndex = result.NextStartIndex;

                // Check if we placed anything on this sheet
                if (result.PlacedCount == 0)
                {
                    // No views fit on this sheet - delete it and stop
                    _document.Delete(sheet.Id);
                    summary.CreatedSheets.Remove(sheet);
                    break;
                }

                // Check if we've placed all sections
                if (currentIndex >= sortedSections.Count)
                {
                    summary.Message = $"All {sortedSections.Count} sections placed.";
                    summary.Success = true;
                    break;
                }
            }

            return summary;
        }
    }

    /// <summary>
    /// Summary of multi-sheet placement operation
    /// </summary>
    public class MultiSheetPlacementSummary
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int TotalPlaced { get; set; }
        public int TotalFailed { get; set; }
        public List<ViewSheet> CreatedSheets { get; set; } = new List<ViewSheet>();
        public List<ElementId> AllPlacedViewIds { get; set; } = new List<ElementId>();
    }
}