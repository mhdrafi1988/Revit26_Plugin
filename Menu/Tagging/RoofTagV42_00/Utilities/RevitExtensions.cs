using Autodesk.Revit.DB;
using System;

namespace Revit26.RoofTagV42.Utilities
{
    public static class RevitExtensions
    {
        public static bool IsPlanView(this View view)
        {
            return view.ViewType == ViewType.FloorPlan ||
                   view.ViewType == ViewType.CeilingPlan ||
                   view.ViewType == ViewType.EngineeringPlan;
        }

        public static bool IsHorizontal(this XYZ vector, double tolerance = 0.001)
        {
            return Math.Abs(vector.Z) < tolerance;
        }

        public static double ToFeet(this double millimeters)
        {
            return millimeters / 304.8;
        }

        public static double ToMillimeters(this double feet)
        {
            return feet * 304.8;
        }
    }
}