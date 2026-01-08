namespace Revit26_Plugin.V5_00.Infrastructure.Logging
{
    public interface IAutoSlopeLogger
    {
        void Info(string message);
        void Warn(string message);
        void Error(string message);
    }
}
