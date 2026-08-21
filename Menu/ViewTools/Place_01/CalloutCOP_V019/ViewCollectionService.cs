using Autodesk.Revit.DB;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.CalloutCOP.V019.Helpers;
using Revit26_Plugin.CalloutCOP.V019.ViewModels;

namespace Revit26_Plugin.CalloutCOP.V019.Services
{
    public static class ViewCollectionService
    {
        public static ObservableCollection<ViewItemViewModel> CollectViews(Document doc)
        {
            var sheetLookup = ViewSheetLookupService.Build(doc);

            return new ObservableCollection<ViewItemViewModel>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(RevitViewValidator.IsSelectableTargetView)
                    .Select(v =>
                    {
                        sheetLookup.TryGetValue(v.Id, out var sheets);
                        return new ViewItemViewModel(v, sheets);
                    }));
        }

        public static ObservableCollection<DraftingViewItemViewModel> CollectDraftingViews(Document doc)
        {
            var detailLookup = ViewSheetLookupService.BuildWithDetailNumbers(doc);

            return new ObservableCollection<DraftingViewItemViewModel>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewDrafting))
                    .Cast<ViewDrafting>()
                    .OrderBy(v => v.Name)
                    .Select(v =>
                    {
                        detailLookup.TryGetValue(v.Id, out var placements);
                        return new DraftingViewItemViewModel(v, placements);
                    }));
        }

        public static ObservableCollection<string> CollectSheetNumbers(Document doc)
            => new(new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Select(s => s.SheetNumber)
                .Distinct()
                .OrderBy(s => s));
    }
}
