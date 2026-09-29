using System;
using System.Threading.Tasks;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V057.ExternalEvents
{
    /// <summary>
    /// Runs work inside a valid Revit API context from the modeless window.
    /// <para>
    /// Once <c>RoofRidgeCommand.Execute</c> returns, button clicks in the modeless window are
    /// outside the API context, so starting a Transaction (or calling PickObject) throws
    /// "Starting a transaction from an external application running outside of API context
    /// is not allowed". <see cref="InvokeAsync"/> queues the work on an
    /// <see cref="ExternalEvent"/> and returns a Task that completes when Revit has run it.
    /// </para>
    /// Must be constructed inside an API context (i.e. during the command's Execute).
    /// </summary>
    public sealed class RevitApiInvoker : IExternalEventHandler, IDisposable
    {
        private readonly ExternalEvent _externalEvent;
        private Action<UIApplication> _pendingWork;
        private TaskCompletionSource<bool> _pendingTcs;

        public RevitApiInvoker()
        {
            _externalEvent = ExternalEvent.Create(this);
        }

        /// <summary>
        /// Queues <paramref name="work"/> to run in the Revit API context. Exceptions thrown
        /// by the work (including OperationCanceledException from picks) are rethrown to the awaiter.
        /// </summary>
        public Task InvokeAsync(Action<UIApplication> work)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            if (_pendingTcs != null)
                throw new InvalidOperationException("A Revit operation is already pending.");

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingWork = work;
            _pendingTcs = tcs;

            ExternalEventRequest request = _externalEvent.Raise();
            if (request != ExternalEventRequest.Accepted)
            {
                _pendingWork = null;
                _pendingTcs = null;
                tcs.SetException(new InvalidOperationException(
                    $"Revit did not accept the request ({request}). Finish any active command and try again."));
            }
            return tcs.Task;
        }

        public void Execute(UIApplication app)
        {
            var work = _pendingWork;
            var tcs = _pendingTcs;
            _pendingWork = null;
            _pendingTcs = null;
            if (work == null || tcs == null) return;

            try
            {
                work(app);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        }

        public string GetName() => "Roof Ridge Lines V057 - Revit API Invoker";

        public void Dispose()
        {
            _externalEvent?.Dispose();
            if (_pendingTcs != null)
            {
                _pendingTcs.TrySetCanceled();
                _pendingTcs = null;
                _pendingWork = null;
            }
        }
    }
}
