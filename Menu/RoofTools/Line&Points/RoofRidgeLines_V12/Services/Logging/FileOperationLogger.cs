using System;
using System.IO;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Logging
{
    public class FileOperationLogger : IOperationLogger
    {
        private readonly string _path =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Revit26",
                "RoofRidgeLines_V12.log");

        public void Log(string message)
        {
            File.AppendAllText(_path, $"{DateTime.Now}: {message}\n");
        }

        public void Log(Exception ex)
        {
            File.AppendAllText(_path, $"{DateTime.Now}: {ex}\n");
        }
    }
}
