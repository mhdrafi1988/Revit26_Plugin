using System.Collections.ObjectModel;
using Revit26_Plugin.CalloutCOP_V04.Helpers;

namespace Revit26_Plugin.CalloutCOP_V04.Services
{
    public sealed class LoggerService
    {
        public ObservableCollection<string> Items { get; } = new();

        public void Info(string msg) => Write("?? " + msg);
        public void Warn(string msg) => Write("?? " + msg);
        public void Error(string msg) => Write("? " + msg);
        public void Success(string msg) => Write("? " + msg);

        private void Write(string msg)
        {
            UiDispatcherHelper.Invoke(() => Items.Add(msg));
        }
    }
}
