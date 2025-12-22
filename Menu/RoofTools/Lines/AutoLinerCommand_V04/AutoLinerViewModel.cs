using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoLiner_V04.Models;
using Revit26_Plugin.AutoLiner_V04.Services;
using System.Collections.ObjectModel;
using Revit26_Plugin.AutoLiner_V04.Models;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V04.ViewModels
{
    public partial class AutoLinerViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly UiLogService _log;

        

public ObservableCollection<UiLogEntry> LogEntries => _log.Entries;


    public IRelayCommand RunCommand { get; }

        public AutoLinerViewModel(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            _log = new UiLogService();

            RunCommand = new RelayCommand(Run);
        }

        private void Run()
        {
            Document doc = _uiDoc.Document;
            View view = doc.ActiveView;

            _log.Info("AutoLiner started");

            // Validation intentionally minimal here
            var selection = _uiDoc.Selection.GetElementIds();
            if (selection.Count != 1)
            {
                _log.Info("Select exactly one roof");
                return;
            }

            var roof = doc.GetElement(selection.First()) as RoofBase;
            if (roof == null)
            {
                _log.Info("Selected element is not a roof");
                return;
            }

            var editor = roof.GetSlabShapeEditor();

            var pathService = new ShapeDownhillPathService(_log);
            var placementService = new DetailItemPlacementService(_log);

            var symbol =
                new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();

            if (symbol == null)
            {
                _log.Info("No detail item family loaded");
                return;
            }

            using (Transaction t = new Transaction(doc, "AutoLiner"))
            {
                t.Start();

                if (!symbol.IsActive)
                    symbol.Activate();

                var paths = pathService.GeneratePaths(editor);
                placementService.PlacePaths(doc, view, symbol, paths);

                t.Commit();
            }

            _log.Info("AutoLiner completed");
        }
    }
}
