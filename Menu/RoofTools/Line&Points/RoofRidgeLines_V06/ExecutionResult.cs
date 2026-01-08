// File: ExecutionResult.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Models
//
// Responsibility:
// - Captures execution statistics for summary display
// - No Revit API references

using System.Collections.Generic;

namespace Revit26_Plugin.RoofRidgeLines_V06.Models
{
    /// <summary>
    /// Represents the result of the wizard execution.
    /// </summary>
    public class ExecutionResult
    {
        public int DetailLinesCreated { get; set; }

        public int ShapePointsAdded { get; set; }

        public List<string> Warnings { get; } = new();

        public double ExecutionTimeSeconds { get; set; }
    }
}
