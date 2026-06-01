// =======================================================
// File: Services/Implementations/UnitConversionService.cs
// Description: Unit conversion implementation
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Implementations
{
    public class UnitConversionService : IUnitConversionService
    {
        // 1 foot = 304.8 mm
        private const double FeetToMmFactor = 304.8;
        private const double MmToFeetFactor = 1.0 / 304.8;

        // 1 foot = 0.3048 meters
        private const double FeetToMetersFactor = 0.3048;
        private const double MetersToFeetFactor = 1.0 / 0.3048;

        public double FeetToMm(double feet) => feet * FeetToMmFactor;
        public double MmToFeet(double mm) => mm * MmToFeetFactor;

        public double FeetToMeters(double feet) => feet * FeetToMetersFactor;
        public double MetersToFeet(double meters) => meters * MetersToFeetFactor;

        public Point3D XyzToPoint3D(XYZ xyz)
        {
            return new Point3D(FeetToMm(xyz.X), FeetToMm(xyz.Y), FeetToMm(xyz.Z));
        }

        public XYZ Point3DToXyz(Point3D point)
        {
            return new XYZ(MmToFeet(point.X), MmToFeet(point.Y), MmToFeet(point.Z));
        }

        public List<Point3D> XyzListToPoint3DList(List<XYZ> xyzList)
        {
            var result = new List<Point3D>();
            foreach (var xyz in xyzList)
                result.Add(XyzToPoint3D(xyz));
            return result;
        }

        public List<XYZ> Point3DListToXyzList(List<Point3D> pointList)
        {
            var result = new List<XYZ>();
            foreach (var point in pointList)
                result.Add(Point3DToXyz(point));
            return result;
        }
    }
}