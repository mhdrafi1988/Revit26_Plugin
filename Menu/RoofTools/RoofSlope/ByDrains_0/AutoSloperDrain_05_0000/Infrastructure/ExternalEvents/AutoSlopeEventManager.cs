using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlope.V5_00.Core.Models;
using System;

namespace Revit26_Plugin.AutoSlope.V5_00.Infrastructure.ExternalEvents
{
    public static class AutoSlopeEventManager
    {
        private static AutoSlopeHandler _handler;
        private static ExternalEvent _event;
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;

        public static void Init()
        {
            lock (_lock)
            {
                if (_isInitialized && _handler != null && _event != null)
                    return;

                try
                {
                    _handler = new AutoSlopeHandler();
                    _event = ExternalEvent.Create(_handler);
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize AutoSlopeEventManager: {ex.Message}");
                    throw new InvalidOperationException("Could not initialize AutoSlope event manager", ex);
                }
            }
        }

        public static void RaiseEvent(AutoSlopePayload payload)
        {
            if (payload == null)
                throw new ArgumentNullException(nameof(payload));

            lock (_lock)
            {
                if (!_isInitialized || _handler == null || _event == null)
                {
                    throw new InvalidOperationException("AutoSlopeEventManager not initialized. Call Init() first.");
                }

                try
                {
                    _handler.SetPayload(payload);
                    _event.Raise();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to raise AutoSlope event: {ex.Message}");
                    throw;
                }
            }
        }

        public static bool IsInitialized
        {
            get
            {
                lock (_lock)
                {
                    return _isInitialized && _handler != null && _event != null;
                }
            }
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_event != null)
                {
                    _event.Dispose();
                    _event = null;
                }
                _handler = null;
                _isInitialized = false;
            }
        }
    }
}