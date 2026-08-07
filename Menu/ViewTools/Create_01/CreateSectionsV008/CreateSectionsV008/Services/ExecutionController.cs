using System.Threading;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.Services
{
    /// <summary>
    /// Controls cancellation of a long-running operation via CancellationToken.
    /// Unchanged from V07. In V008 this now actually functions for mid-run
    /// cancellation because the dialog is modeless (see SectionCreationExternalEvent).
    /// </summary>
    public class ExecutionController
    {
        public CancellationTokenSource TokenSource { get; private set; } = new();

        public CancellationToken Token => TokenSource.Token;

        public void Cancel() => TokenSource.Cancel();

        /// <summary>
        /// V008: resets the token source for a fresh run. Once a
        /// CancellationTokenSource is cancelled it cannot be un-cancelled,
        /// so a new run after a cancel needs a new token.
        /// </summary>
        public void Reset() => TokenSource = new CancellationTokenSource();
    }
}
