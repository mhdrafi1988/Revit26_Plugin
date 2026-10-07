using Revit26_Plugin.SheetViewArrange.V001.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;

namespace Revit26_Plugin.SheetViewArrange.V001.Infrastructure
{
    /// <summary>Result of a Refresh or Apply request.</summary>
    public sealed class SessionResult
    {
        /// <summary>The sheet as it is now in the model, or null when it could not be read.</summary>
        public SheetSnapshot Snapshot { get; init; }

        /// <summary>Severity of <see cref="Message"/>.</summary>
        public LogLevel Level { get; init; } = LogLevel.Info;

        /// <summary>Outcome line for the log, or null when there is nothing to report.</summary>
        public string Message { get; init; }
    }

    /// <summary>
    /// The window's only link to Revit: reads the sheet and applies arrangements inside Revit's
    /// API context. Callbacks run on the UI thread once the request has been handled.
    /// </summary>
    public interface ISheetArrangeSession : IDisposable
    {
        /// <summary>The sheet as read when the tool opened.</summary>
        SheetSnapshot Initial { get; }

        /// <summary>Re-reads the sheet. Returns false when Revit did not accept the request.</summary>
        bool Refresh(Action<SessionResult> done);

        /// <summary>
        /// Re-reads the sheet, re-plans with <paramref name="settings"/> and moves the views — but
        /// only when the fresh plan still has <paramref name="previewSignature"/>, i.e. matches
        /// what the user saw. Returns false when Revit did not accept the request.
        /// </summary>
        bool Apply(SheetViewArrangeSettings settings, string previewSignature, Action<SessionResult> done);
    }
}
