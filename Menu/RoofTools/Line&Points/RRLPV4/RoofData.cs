using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.RRLPV4.Models
{
    public class RoofData
    {
        public Autodesk.Revit.DB.RoofBase SelectedRoof { get; set; }
        public XYZ Point1 { get; set; }
        public XYZ Point2 { get; set; }
        public GraphicsStyle UsedLineStyle { get; set; }
        public string DivisionStrategy { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;

        public int DetailLinesCreated { get; set; }
        public int PerpendicularLinesCreated { get; set; }
        public int EdgePointsAdded { get; set; }
        public bool IsSuccess { get; set; }

        public List<string> LogMessages { get; } = new();

        public void AddLog(string message)
        {
            LogMessages.Add($"{DateTime.Now:HH:mm:ss} - {message}");
        }

        public string GetSummary()
        {
            return $"Detail: {DetailLinesCreated}, Perp: {PerpendicularLinesCreated}, " +
                   $"EdgePts: {EdgePointsAdded}, Duration: {Duration:mm\\:ss}";
        }
    }
}
