// =======================================================
// File: Services/Interfaces/IUnitConversionService.cs
// Description: Service contract for unit conversions
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces
{
    /// <summary>
    /// Converts between Revit internal units and display units
    /// </summary>
    public interface IUnitConversionService
    {
        // Feet (Revit internal) to Millimeters
        double FeetToMm(double feet);
        double MmToFeet(double mm);

        // Feet to Meters
        double FeetToMeters(double feet);
        double MetersToFeet(double meters);

        // Convert XYZ to Point3D (mm)
        Point3D XyzToPoint3D(XYZ xyz);

        // Convert Point3D to XYZ (feet)
        XYZ Point3DToXyz(Point3D point);

        // Convert list of XYZ to Point3D list
        List<Point3D> XyzListToPoint3DList(List<XYZ> xyzList);

        // Convert list of Point3D to XYZ list
        List<XYZ> Point3DListToXyzList(List<Point3D> pointList);
    }
}