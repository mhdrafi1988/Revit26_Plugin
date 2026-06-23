using System;
using System.Windows;
using System.Windows.Threading;

namespace Revit26_Plugin.RoofFromFloor.Services
{
    public class UiLogService
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _append;

        public UiLogService(Dispatcher dispatcher, Action<string> append)
        {
            _dispatcher = dispatcher;
            _append = append;
        }

        public void Info(string msg) => Write(msg);
        public void Warn(string msg) => Write($"? {msg}");
        public void Error(string msg) => Write($"? {msg}");

        private void Write(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {msg}";

            if (_dispatcher.CheckAccess())
                _append(line);
            else
                _dispatcher.Invoke(() => _append(line));
        }
    }
}
