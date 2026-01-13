using Autodesk.Revit.DB;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.CalloutCOP_V04.Helpers;
using Revit26_Plugin.CalloutCOP_V04.Models;
using Revit26_Plugin.CalloutCOP_V04.Services;

namespace Revit26_Plugin.CalloutCOP_V04.ViewModels
{
    public class CalloutCOPViewModel
    {
        private readonly RevitContextService _context;
        private readonly LoggerService _logger;

        public ObservableCollection<CalloutItem> Items { get; }
        public ObservableCollection<ViewSheet> Sheets { get; }
        public ObservableCollection<ViewDrafting> DraftingViews { get; }

        public ViewDrafting SelectedDraftingView { get; set; }
        public ViewFilterState Filter { get; } = new();

        public LoggerService Logger => _logger;

        public RelayCommand PlaceCommand { get; }

        public CalloutCOPViewModel(RevitContextService context)
        {
            _context = context;
            _logger = new LoggerService();

            Sheets = new(CalloutCollectorService.GetSheets(context.Doc));
            DraftingViews = new(CalloutCollectorService.GetDraftingViews(context.Doc));
            Items = new(CalloutCollectorService.GetSectionItems(context.Doc));

            PlaceCommand = new RelayCommand(Place, CanPlace);
        }

        private bool CanPlace()
            => SelectedDraftingView != null && Items.Any(i => i.IsSelected);

        private void Place()
        {
            CalloutPlacementService.Place(
                _context.Doc,
                _context.UiDoc,
                Items.Where(i => i.IsSelected),
                SelectedDraftingView,
                _logger);
        }
    }
}
