using System;
using System.IO;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Utils
{
    public static class Logger
    {
        private static readonly string PathLog =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Revit26", "RoofRidgeLines_V11.log");

        public static void LogException(Exception ex, string ctx)
        {
            File.AppendAllText(PathLog,
                $"{DateTime.Now} [{ctx}] {ex}\n");
        }
    }
}
