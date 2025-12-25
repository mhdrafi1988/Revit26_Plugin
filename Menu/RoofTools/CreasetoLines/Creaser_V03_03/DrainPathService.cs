using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V03_03.Helpers;
using Revit26_Plugin.Creaser_V03_03.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Creaser_V03_03.Services
{
    /// <summary>
    /// Drain path computation service
    /// </summary>
    public static class DrainPathService
    {
        /// <summary>
        /// ENTRY POINT – must be at CLASS LEVEL
        /// </summary>
        public static List<DrainPath> ComputeDrainPaths(
            Document doc,
            RoofBase roof,
            UiLogService log)
        {
            log.Log("DrainPathService started");

            // TEMP SAFE RETURN (logic does not matter yet)
            return new List<DrainPath>();
        }
    }
}
