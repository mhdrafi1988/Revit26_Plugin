using System.Threading;

namespace Revit26_Plugin.CSFL_V07.Services.Execution
{
    public class ExecutionController
    {
        public CancellationTokenSource TokenSource { get; } = new();

        public CancellationToken Token => TokenSource.Token;

        public void Cancel() => TokenSource.Cancel();
    }
}
