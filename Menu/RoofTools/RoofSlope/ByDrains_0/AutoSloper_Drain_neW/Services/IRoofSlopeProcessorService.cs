// =======================================================
// File: Services/Interfaces/IRoofSlopeProcessorService.cs
// Description: Service contract for slope processing
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.AutoSlopeByDrain_21.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces
{
    /// <summary>
    /// Processes slope modifications on roof geometry
    /// </summary>
    public interface IRoofSlopeProcessorService
    {
        /// <summary>
        /// Processes roof slopes based on selected drains
        /// </summary>
        /// <param name="roof">Roof element to modify</param>
        /// <param name="selectedDrains">Selected drains to slope toward</param>
        /// <param name="slopePercent">Slope percentage to apply</param>
        /// <param name="thresholdMeters">Threshold distance in meters</param>
        /// <param name="logAction">Optional logging callback</param>
        /// <returns>Processing results</returns>
        SlopeResult ProcessRoofSlopes(
            RoofBase roof,
            List<DrainItem> selectedDrains,
            double slopePercent,
            double thresholdMeters,
            Action<string> logAction = null);
    }
}