// =======================================================
// File: ResultRow.cs
// Namespace: Revit26_Plugin_Revised.AutoSlopeByPoint.V032.UI.ViewModels
// New in V032: one row of the results side panel. Column names match the
// Excel "Vertex Data" sheet so the panel and the workbook read the same.
// Display strings are formatted once here; the DataGrid binds to them.
// =======================================================

using System;
using System.Globalization;
using Revit26_Plugin_Revised.AutoSlopeByPoint.V032.Core.Models;

namespace Revit26_Plugin_Revised.AutoSlopeByPoint.V032.UI.ViewModels
{
    public enum ResultsTab
    {
        Longest,
        Skipped
    }

    public sealed class ResultRow
    {
        public int RowNumber { get; init; }
        public int VertexIndex { get; init; }
        public string DrainIndexText { get; init; }
        public string PathLengthText { get; init; }
        public string ElevCalcText { get; init; }
        public string ElevModelText { get; init; }
        public string ElevDiffText { get; init; }
        public bool IsAdjusted { get; init; }
        public bool IsTop { get; init; }
        public string Status { get; init; }
        public string Fix { get; init; }
        public Point3 Position { get; init; }

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static ResultRow FromProcessed(VertexData v, int rowNumber)
        {
            double diff = v.ElevationDiffMm;
            return new ResultRow
            {
                RowNumber = rowNumber,
                VertexIndex = v.VertexIndex,
                DrainIndexText = v.DrainIndex >= 0 ? v.DrainIndex.ToString(Inv) : "—",
                PathLengthText = v.PathLengthMeters.ToString("0.00", Inv),
                ElevCalcText = Math.Round(v.ElevationCalcMm).ToString("0", Inv),
                ElevModelText = v.HasModelValue ? Math.Round(v.ElevationModelMm).ToString("0", Inv) : "—",
                ElevDiffText = !v.HasModelValue ? "—" : diff > 0 ? "+" + diff.ToString("0", Inv) : diff.ToString("0", Inv),
                IsAdjusted = v.HasModelValue && diff != 0,
                IsTop = rowNumber == 1,
                Position = v.Position
            };
        }

        public static ResultRow FromSkipped(VertexData v, int rowNumber)
        {
            bool noPath = v.Outcome == VertexOutcome.NoPath;
            return new ResultRow
            {
                RowNumber = rowNumber,
                VertexIndex = v.VertexIndex,
                Status = noPath ? "No path" : "Beyond distance",
                PathLengthText = noPath ? "—" : v.PathLengthMeters.ToString("0.00", Inv),
                Fix = noPath ? "Check openings or the max link length" : "Raise the drainage distance",
                Position = v.Position
            };
        }

        public static string Header(ResultsTab tab) => tab == ResultsTab.Longest
            ? "#\tVertexIndex\tDrainIndex\tPathLength_m\tElevCalc_mm\tElevModel_mm\tElevDiff_mm"
            : "#\tVertexIndex\tStatus\tPathLength_m\tSuggested fix";

        /// <summary>Tab-separated, so a paste into Excel lands in columns.</summary>
        public string ToTsv(ResultsTab tab) => tab == ResultsTab.Longest
            ? $"{RowNumber}\t{VertexIndex}\t{DrainIndexText}\t{PathLengthText}\t{ElevCalcText}\t{ElevModelText}\t{ElevDiffText}"
            : $"{RowNumber}\t{VertexIndex}\t{Status}\t{PathLengthText}\t{Fix}";
    }
}
