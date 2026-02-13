// File: SectionCollectionService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V314.Services
{
    public class SectionCollectionService
    {
        private readonly Document _doc;

        public SectionCollectionService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public List<SectionItemViewModel> Collect()
        {
            var result = new List<SectionItemViewModel>();

            try
            {
                // Get all viewports in the document
                var allViewports = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                // Collect all sheets
                var sheets = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                // Create a map of view IDs to sheets
                var viewToSheetMap = new Dictionary<ElementId, ViewSheet>();

                foreach (var sheet in sheets)
                {
                    try
                    {
                        var placedViewIds = sheet.GetAllPlacedViews();
                        foreach (var viewId in placedViewIds)
                        {
                            if (!viewToSheetMap.ContainsKey(viewId))
                            {
                                viewToSheetMap[viewId] = sheet;
                            }
                        }
                    }
                    catch
                    {
                        // Skip problematic sheets
                    }
                }

                // Collect section views
                var sections = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSection))
                    .Cast<ViewSection>()
                    .Where(v =>
                        !v.IsTemplate &&
                        v.ViewType == ViewType.Section &&
                        v.GetPrimaryViewId() == ElementId.InvalidElementId)
                    .ToList();

                // Build ViewModels with VERIFIED placement status
                foreach (var section in sections)
                {
                    try
                    {
                        bool isActuallyPlaced = false;
                        string actualSheetNumber = string.Empty;

                        // Method 1: Check viewport map
                        if (viewToSheetMap.TryGetValue(section.Id, out var sheet))
                        {
                            isActuallyPlaced = true;
                            actualSheetNumber = sheet.SheetNumber;
                        }
                        else
                        {
                            // Method 2: Check if there's a viewport for this view
                            var viewport = allViewports.FirstOrDefault(vp => vp.ViewId == section.Id);
                            if (viewport != null)
                            {
                                isActuallyPlaced = true;
                                var viewportSheet = _doc.GetElement(viewport.OwnerViewId) as ViewSheet;
                                actualSheetNumber = viewportSheet?.SheetNumber ?? "Unknown";
                            }
                        }

                        string scope = section.LookupParameter("Placement_Scope")?.AsString() ??
                                      section.LookupParameter("Detail Number")?.AsString() ??
                                      string.Empty;

                        result.Add(new SectionItemViewModel(
                            section,
                            isActuallyPlaced,
                            actualSheetNumber,
                            scope));
                    }
                    catch
                    {
                        // Skip problematic sections
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ERROR in SectionCollectionService: {ex.Message}");
            }

            return result;
        }
    }
}