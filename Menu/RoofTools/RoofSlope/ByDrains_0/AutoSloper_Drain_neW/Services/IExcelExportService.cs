// =======================================================
// File: Services/Interfaces/IExcelExportService.cs
// Description: Service contract for Excel export
// =======================================================

using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces
{
    /// <summary>
    /// Exports slope results and drain data to Excel
    /// </summary>
    public interface IExcelExportService
    {
        /// <summary>
        /// Exports slope processing results to Excel
        /// </summary>
        /// <param name="folderPath">Export folder path</param>
        /// <param name="slopeResult">Slope processing results</param>
        /// <param name="drains">List of drains</param>
        /// <param name="includeVertexDetails">Include detailed vertex information</param>
        /// <param name="roofName">Roof name for filename</param>
        /// <returns>Full path to exported file</returns>
        string ExportToExcel(
            string folderPath,
            SlopeResult slopeResult,
            List<DrainItem> drains,
            bool includeVertexDetails,
            string roofName);
    }
}