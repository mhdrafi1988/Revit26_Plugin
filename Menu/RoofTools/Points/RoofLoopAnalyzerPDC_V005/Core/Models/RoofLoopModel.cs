// =======================================================
// File: RoofLoopModel.cs
// Location: Core/Models/
// Changes vs V004: converted from hand-rolled INotifyPropertyChanged to
// CommunityToolkit.Mvvm's ObservableObject + [ObservableProperty], per
// this suite's stack convention.
// =======================================================

using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Core.Models
{
    public partial class RoofLoopModel : ObservableObject
    {
        public int Index { get; set; }
        public double PerimeterMm { get; set; }
        public string LoopType { get; set; }       // Outer / Inner
        public bool IsCircular { get; set; }
        public string LoopShapeType { get; set; }  // Circular / Rectangle / Other

        // Circle geometry — populated only for Circular loops
        public XYZ Center { get; set; }
        public double Radius { get; set; }

        [ObservableProperty]
        private bool isSelected = true;

        [ObservableProperty]
        private int recommendedPoints = 8;

        public CurveLoop Geometry { get; set; }
    }
}
