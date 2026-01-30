using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.DwgSymbolicConverter_V01.Models;
using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Revit26_Plugin.DwgSymbolicConverter_V01.Services
{
    public class CadGeometryScanService
    {
        private readonly UIApplication _uiApp;
        private readonly Action<string, Brush> _log;

        public CadGeometryScanService(UIApplication app, Action<string, Brush> log)
        {
            _uiApp = app;
            _log = log;
        }

        public void Scan(CadFileInfo info, ObservableCollection<CadGeometrySummary> output)
        {
            _log("[INFO] Geometry scan started", Brushes.White);

            // NOTE: placeholder counts — real extraction comes next iteration
            output.Add(new CadGeometrySummary { GeometryType = "Line", Count = 132, LayerName = "A-WALL" });
            output.Add(new CadGeometrySummary { GeometryType = "Polyline", Count = 18, LayerName = "A-ROOF" });

            _log("[INFO] Geometry scan complete", Brushes.White);
        }
    }
}
