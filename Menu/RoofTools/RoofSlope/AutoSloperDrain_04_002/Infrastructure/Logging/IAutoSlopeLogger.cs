namespace Revit22_Plugin.V4_02.Infrastructure.Logging
{
    public interface IAutoSlopeLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
