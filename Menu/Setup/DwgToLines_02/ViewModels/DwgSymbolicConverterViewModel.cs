using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;
using Revit26_Plugin.DwgSymbolicConverter_V03.Services;
using Revit26_Plugin.DwgSymbolicConverter_V03.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.ViewModels
{
    public partial class DwgSymbolicConverterViewModel : ObservableObject
    {
        private readonly UIApplication _uiApp;
        private readonly UIDocument _uiDoc;
        private readonly Window _view;

        private readonly CadGeometryScanService _scanService;

        public ObservableCollection<DwgItemModel> CadItems { get; } = new();
        public ObservableCollection<CadGeometrySummary> GeometrySummary { get; } = new();
        public ObservableCollection<string> LiveLog { get; } = new();

        [ObservableProperty]
        private DwgItemModel _selectedCad;

        public DwgSymbolicConverterViewModel(
            UIApplication uiApp,
            Window view)
        {
            _uiApp = uiApp;
            _uiDoc = uiApp.ActiveUIDocument;
            _view = view;

            // Initialize _scanService with required parameters
            _scanService = new CadGeometryScanService(_uiApp, (msg, brush) => { /* Logging logic or leave empty */ });

            RefreshCadList();
        }

        // ----------------------------------------------------
        // LISTING
        // ----------------------------------------------------

        private void RefreshCadList()
        {
            CadItems.Clear();

            var items = DwgCollectorService
                .Collect(_uiDoc.Document)
                .OrderBy(i => i.TypeName);

            foreach (var item in items)
                CadItems.Add(item);

            Log($"DWGs found: {CadItems.Count}");
        }

        partial void OnSelectedCadChanged(DwgItemModel value)
        {
            if (value == null)
                return;

            Log($"Selected: {value}");
            ScanSelectedCad();
        }

        // ----------------------------------------------------
        // PICK DWG (HIDE ? PICK ? SHOW)
        // ----------------------------------------------------

        [RelayCommand]
        private void PickCad()
        {
            try
            {
                _view.Hide();

                ImportInstance picked =
                    CadManualPickService.Pick(_uiDoc);

                if (picked == null)
                    return;

                SelectedCad = new DwgItemModel(picked);
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                Log("Pick canceled.");
            }
            finally
            {
                _view.Show();
                _view.Activate();
            }
        }

        // ----------------------------------------------------
        // SCAN / UPDATE (AUTO)
        // ----------------------------------------------------

        private void ScanSelectedCad()
        {
            GeometrySummary.Clear();

            ImportInstance import =
                _uiDoc.Document.GetElement(
                    SelectedCad.ElementId) as ImportInstance;

            if (import == null)
            {
                Log("DWG not found in active document.");
                return;
            }

            View view = _uiDoc.ActiveView;

            Plane plane = Plane.CreateByNormalAndOrigin(
                view.ViewDirection,
                view.Origin);

            using (Transaction tx =
                new Transaction(_uiDoc.Document, "DWG Geometry Scan"))
            {
                tx.Start();

                SketchPlane sketchPlane =
                    SketchPlane.Create(_uiDoc.Document, plane);

                _scanService.Scan(
                    import,
                    view,
                    sketchPlane,
                    ResolveSplineMode(),
                    GeometrySummary);

                tx.Commit();
            }

            Log($"Geometry rows: {GeometrySummary.Count}");
        }

        // ----------------------------------------------------
        // HELPERS
        // ----------------------------------------------------

        private SplineHandlingMode ResolveSplineMode()
        {
            // No enum assumptions — safe fallback
            return Enum.GetValues(typeof(SplineHandlingMode))
                       .Cast<SplineHandlingMode>()
                       .First();
        }

        private void Log(string msg)
        {
            LiveLog.Add($"{DateTime.Now:HH:mm:ss}  {msg}");
        }
    }
}
