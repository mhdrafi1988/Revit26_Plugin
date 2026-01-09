using System;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Logging
{
    public interface IOperationLogger
    {
        void Log(string message);
        void Log(Exception ex);
    }
}
