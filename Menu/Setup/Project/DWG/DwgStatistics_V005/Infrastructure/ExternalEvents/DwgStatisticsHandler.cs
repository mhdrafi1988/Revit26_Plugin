using System;
using System.Collections.Concurrent;
using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.DwgStatistics.V005.Infrastructure.ExternalEvents
{
    /// <summary>
    /// Runs actions queued by the DWG Statistics window inside the Revit API context. Several actions
    /// can be queued before Revit raises the event once, so every queued action runs in order.
    /// (Same queue pattern as DWG Manager V003.)
    /// </summary>
    public sealed class DwgStatisticsHandler : IExternalEventHandler
    {
        private readonly ConcurrentQueue<Action<UIApplication>> _queue = new();

        /// <summary>Queues an action to run when the external event fires.</summary>
        public void Queue(Action<UIApplication> action) => _queue.Enqueue(action);

        /// <inheritdoc/>
        public void Execute(UIApplication app)
            => ToolGuard.RunHandler(GetType(), app, ExecuteUnguarded);

        private void ExecuteUnguarded(UIApplication app)
        {
            while (_queue.TryDequeue(out var action))
                action(app);
        }

        /// <inheritdoc/>
        public string GetName() => "DWG Statistics V005";
    }
}
