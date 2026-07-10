// File: SectionCollectionService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V321_01.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V321_01.Services
{
    /// <summary>
    /// Collects all section views in the document (Whole Model scope — the
    /// only scope Rafi asked to support) and determines their placed/unplaced
    /// status by cross-referencing sheet viewports.
    /// </summary>
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
                var allViewports = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                var sheets = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

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

                var sections = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSection))
                    .Cast<ViewSection>()
                    .Where(v =>
                        !v.IsTemplate &&
                        v.ViewType == ViewType.Section &&
                        v.GetPrimaryViewId() == ElementId.InvalidElementId)
                    .ToList();

                foreach (var section in sections)
                {
                    try
                    {
                        bool isActuallyPlaced = false;
                        string actualSheetNumber = string.Empty;

                        if (viewToSheetMap.TryGetValue(section.Id, out var sheet))
                        {
                            isActuallyPlaced = true;
                            actualSheetNumber = sheet.SheetNumber;
                        }
                        else
                        {
                            var viewport = allViewports.FirstOrDefault(vp => vp.ViewId == section.Id);
                            if (viewport != null)
                            {
                                isActuallyPlaced = true;
                                var viewportSheet = _doc.GetElement(viewport.OwnerViewId) as ViewSheet;
                                actualSheetNumber = viewportSheet?.SheetNumber ?? "Unknown";
                            }
                        }

                        result.Add(new SectionItemViewModel(
                            section,
                            isActuallyPlaced,
                            actualSheetNumber));
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
