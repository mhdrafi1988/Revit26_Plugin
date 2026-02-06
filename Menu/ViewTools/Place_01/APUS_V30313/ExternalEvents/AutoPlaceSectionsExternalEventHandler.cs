using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
            if (ViewModel == null ||
                SortedSections == null ||
                SortedSections.Count == 0 ||
                TitleBlock == null ||
                PlacementArea == null)
            {
                ViewModel?.LogError("Invalid placement parameters.");
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

        public string GetName()
        {
            return "AutoPlaceSectionsShelfPlacementHandler";
        }
    }
}