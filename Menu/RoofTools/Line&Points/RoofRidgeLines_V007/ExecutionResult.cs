using System;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Models
{
    public class ExecutionResult
    {
        public bool IsSuccess { get; set; }
        public string StatusMessage { get; set; }
        public int DetailLinesCreated { get; set; }
        public int PerpendicularLinesCreated { get; set; }
        public int ShapePointsAdded { get; set; }
        public TimeSpan Duration { get; set; }
        public List<string> LogMessages { get; } = new List<string>();

        public void AddLog(string message)
        {
            LogMessages.Add($"{DateTime.Now:HH:mm:ss} - {message}");
        }
    }
}