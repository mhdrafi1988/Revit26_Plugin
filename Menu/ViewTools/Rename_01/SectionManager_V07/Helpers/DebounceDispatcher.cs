using System;
using System.Timers;

namespace Revit26_Plugin.SectionManager_V07.Helpers
{
    public class DebounceDispatcher
    {
        private Timer _timer;

        public void Debounce(int milliseconds, Action action)
        {
            _timer?.Stop();
            _timer = new Timer(milliseconds) { AutoReset = false };
            _timer.Elapsed += (_, __) => action();
            _timer.Start();
        }
    }
}
