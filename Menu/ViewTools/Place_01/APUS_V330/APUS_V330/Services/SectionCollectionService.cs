// File: Services/SectionCollectionService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V330.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V330.Services
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
                var allViewports = new FilteredElementCollector(_doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .ToList();

                var sheets = new FilteredElementCollector(_doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .ToList();

                // Map: view element ID → sheet
                var viewToSheetMap = new Dictionary<ElementId, ViewSheet>();
                foreach (var sheet in sheets)
                {
                    try
                    {
                        foreach (var viewId in sheet.GetAllPlacedViews())
                            if (!viewToSheetMap.ContainsKey(viewId))
                                viewToSheetMap[viewId] = sheet;
                    }
                    catch { /* skip problematic sheets */ }
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
                        bool   isPlaced     = false;
                        string sheetNumber  = string.Empty;

                        if (viewToSheetMap.TryGetValue(section.Id, out var sheet))
                        {
                            isPlaced    = true;
                            sheetNumber = sheet.SheetNumber;
                        }
                        else
                        {
                            var vp = allViewports.FirstOrDefault(v => v.ViewId == section.Id);
                            if (vp != null)
                            {
                                isPlaced    = true;
                                sheetNumber = (_doc.GetElement(vp.OwnerViewId) as ViewSheet)?.SheetNumber ?? "Unknown";
                            }
                        }

                        string scope = section.LookupParameter("Placement_Scope")?.AsString() ??
                                       section.LookupParameter("Detail Number")?.AsString()    ??
                                       string.Empty;

                        result.Add(new SectionItemViewModel(section, isPlaced, sheetNumber, scope));
                    }
                    catch { /* skip problematic sections */ }
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
