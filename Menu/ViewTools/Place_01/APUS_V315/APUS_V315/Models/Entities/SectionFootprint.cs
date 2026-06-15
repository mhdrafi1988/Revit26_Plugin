using System;

namespace Revit26_Plugin.APUS_V315.Models.Entities;

public record SectionFootprint(double WidthFeet, double HeightFeet)
{
    public double WidthFeet { get; } = Math.Max(WidthFeet, 0.05);
    public double HeightFeet { get; } = Math.Max(HeightFeet, 0.05);

    public double Area => WidthFeet * HeightFeet;

    public override string ToString() => $"{WidthFeet:F2}' × {HeightFeet:F2}'";
}