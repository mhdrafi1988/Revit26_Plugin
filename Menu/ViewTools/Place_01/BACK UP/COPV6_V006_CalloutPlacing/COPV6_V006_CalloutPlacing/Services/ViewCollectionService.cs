using Autodesk.Revit.DB;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.CalloutCOP_V06.Helpers;
using Revit26_Plugin.CalloutCOP_V06.ViewModels;

namespace Revit26_Plugin.CalloutCOP_V06.Services
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

        public static ObservableCollection<ViewDrafting> CollectDraftingViews(Document doc)
            => new(new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .OrderBy(v => v.Name));

        public static ObservableCollection<string> CollectSheetNumbers(Document doc)
            => new(new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Select(s => s.SheetNumber)
                .Distinct()
                .OrderBy(s => s));
    }
}
