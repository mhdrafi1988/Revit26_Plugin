using Autodesk.Revit.UI;
using Revit26_Plugin.Shared.Services;
using System;
using System.Collections.Concurrent;

namespace Revit26_Plugin.BulkRename.V001.Core.Services
{
    /// <summary>
    /// Runs actions queued by the WPF window inside the Revit API context. Several actions can be queued
    /// before Revit raises the event once, so every queued action runs in order.
    /// </summary>
    public sealed class BulkRenameHandler : IExternalEventHandler
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
        public string GetName() => "Bulk Rename V001";
    }
}
