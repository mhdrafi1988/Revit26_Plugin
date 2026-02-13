using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V313.Enums;
using Revit26_Plugin.APUS_V313.Models;
using Revit26_Plugin.APUS_V313.Services;
using Revit26_Plugin.APUS_V313.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.APUS_V313.ExternalEvents
{
    public class AutoPlaceSectionsExternalEventHandler : IExternalEventHandler
    {
        // Properties set by ViewModel
        public AutoPlaceSectionsViewModel ViewModel { get; set; }
        public IList<SectionItemViewModel> SortedSections { get; set; }
        public FamilySymbol TitleBlock { get; set; }
        public SheetPlacementArea PlacementArea { get; set; }
        public double HorizontalGapMm { get; set; }
        public double VerticalGapMm { get; set; }

        public void Execute(UIApplication uiapp)
        {
            var validationResult = ValidatePlacementParameters();
            if (validationResult != PlacementValidationState.Valid)
            {
                HandleValidationError(validationResult);
                return;
            }

            Document doc = uiapp.ActiveUIDocument.Document;

            try
            {
                using (Transaction tx = new Transaction(doc, "Auto Place Sections (Shelf Layout)"))
                {
                    tx.Start();

                    ViewModel.LogInfo("Starting shelf/row placement...");

                    // Create orchestrator and place sections
                    var orchestrator = new MultiSheetShelfPlacementOrchestrator(doc);

                    var summary = orchestrator.PlaceSections(
                        SortedSections,
                        TitleBlock,
                        PlacementArea,
                        HorizontalGapMm,
                        VerticalGapMm);

                    // Update progress for all placed sections
                    for (int i = 0; i < summary.TotalPlaced; i++)
                    {
                        ViewModel.Progress.Step();
                    }

                    tx.Commit();

                    // Update progress state based on result
                    if (!summary.Success)
                    {
                        ViewModel.Progress.Fail();
                    }

                    // Log results
                    if (summary.Success)
                    {
                        ViewModel.LogInfo($"? Placement completed successfully.");
                        ViewModel.LogInfo($"  Sheets created: {summary.CreatedSheets.Count}");
                        ViewModel.LogInfo($"  Views placed: {summary.TotalPlaced}");

                        if (summary.TotalFailed > 0)
                        {
                            ViewModel.LogWarning($"  Failed placements: {summary.TotalFailed}");
                        }

                        // Optionally open created sheets
                        if (ViewModel.OpenSheetsAfterPlacement && summary.CreatedSheets.Any())
                        {
                            uiapp.ActiveUIDocument.ActiveView = summary.CreatedSheets.First();
                        }
                    }
                    else
                    {
                        ViewModel.LogError($"? Placement failed: {summary.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                ViewModel.Progress.Fail();
                ViewModel.LogError($"Placement failed with error: {ex.Message}");
                ViewModel.LogError($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                // Clean up and notify completion
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ViewModel.OnPlacementComplete();
                    ViewModel = null;
                    SortedSections = null;
                    TitleBlock = null;
                    PlacementArea = null;
                });
            }
        }

        private PlacementValidationState ValidatePlacementParameters()
        {
            if (ViewModel == null)
                return PlacementValidationState.GeneralError;

            if (SortedSections == null || SortedSections.Count == 0)
                return PlacementValidationState.NoSections;

            if (TitleBlock == null)
                return PlacementValidationState.NoTitleBlock;

            if (PlacementArea == null)
                return PlacementValidationState.NoPlacementArea;

            return PlacementValidationState.Valid;
        }

        private void HandleValidationError(PlacementValidationState validationState)
        {
            var errorMessage = validationState switch
            {
                PlacementValidationState.NoSections => "No sections to place.",
                PlacementValidationState.NoTitleBlock => "No title block selected.",
                PlacementValidationState.NoPlacementArea => "Invalid placement area.",
                PlacementValidationState.InvalidParameters => "Invalid placement parameters.",
                _ => "General validation error."
            };

            ViewModel?.LogError(errorMessage);
            ViewModel?.Progress.Fail();
        }

        public string GetName()
        {
            return "AutoPlaceSectionsShelfPlacementHandler";
        }
    }
}