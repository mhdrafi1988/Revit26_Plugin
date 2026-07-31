using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofCreationIsolationTest.V001.Core.Models;
using Revit26_Plugin.RoofCreationIsolationTest.V001.Core.Services;
using Revit26_Plugin.RoofCreationIsolationTest.V001.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;

namespace Revit26_Plugin.RoofCreationIsolationTest.V001.Infrastructure.ExternalEvents
{
    /// <summary>
    /// IExternalEventHandler for the single Run action in this diagnostic tool.
    /// Opens one Transaction, delegates to RoofTestService, and rolls back
    /// regardless of outcome — this is an isolation TEST, not a production
    /// roof-placement tool, so nothing it creates should persist in the model
    /// by default. On completion, raises Completed with the full RoofTestResult
    /// so the ViewModel can update UI state (re-enable Run, show summary).
    ///
    /// Execute() runs on the Revit API thread (invoked via ExternalEvent.Raise()),
    /// NOT the WPF UI thread. All log writes go through ThreadSafeLogSink, which
    /// marshals each Add() onto the UI dispatcher — this is what makes the log
    /// panel actually update; writing directly to the bound ObservableCollection
    /// from this thread was the original bug (log area stayed empty).
    /// </summary>
    public class RunTestHandler : IExternalEventHandler
    {
        private readonly RoofTestService _service = new();
        private readonly ThreadSafeLogSink _log;

        /// <summary>Raised after Execute() completes, on the Revit API thread, with the full result.</summary>
        public event Action<RoofTestResult>? Completed;

        public RunTestHandler(ThreadSafeLogSink log)
        {
            _log = log;
        }

        public void Execute(UIApplication app)
        {
            _log.Add(LogLevel.Info, "Execute() entered on Revit API thread");

            Document doc = app.ActiveUIDocument.Document;
            RoofTestResult result;

            Transaction? tx = null;
            try
            {
                tx = new Transaction(doc, "Roof Creation Isolation Test");
                tx.Start();
                _log.Add(LogLevel.Info, "Opening Transaction \"Roof Creation Isolation Test\"");

                result = _service.RunTest(doc, _log);

                // Always roll back: this tool never leaves test geometry in the model.
                _log.Add(LogLevel.Warning, "Rolling back Transaction \"Roof Creation Isolation Test\" (isolation test - no persistent changes)");
                tx.RollBack();
            }
            catch (Exception ex)
            {
                // Catches failures in transaction open/rollback itself, distinct from
                // the roof-creation exception already captured inside RoofTestService.
                _log.Add(LogLevel.Error,
                    $"EXCEPTION in Execute() transaction handling:\n  Type: {ex.GetType().FullName}\n  Message: {ex.Message}\n  StackTrace:\n{ex.StackTrace}");

                try
                {
                    // Transaction.RollBack() is safe to call here since we only ever
                    // Start() it above and never Commit() — guarded by status check
                    // in case the exception happened before Start() completed.
                    if (tx != null && tx.GetStatus() == TransactionStatus.Started)
                        tx.RollBack();
                }
                catch (Exception rollbackEx)
                {
                    _log.Add(LogLevel.Error, $"EXCEPTION during rollback attempt: {rollbackEx.Message}");
                }

                result = new RoofTestResult
                {
                    Success = false,
                    ExceptionTypeName = ex.GetType().FullName,
                    ExceptionMessage = ex.Message,
                    ExceptionStackTrace = ex.StackTrace,
                    ExceptionSource = ex.Source
                };
            }
            finally
            {
                tx?.Dispose();
            }

            string validationTag = result.ValidationPassed
                ? "Validation: PASS"
                : $"Validation: FAIL ({result.ValidationIssues.Count} issue(s))";

            string summary = result.Success
                ? $"{validationTag} | 1 created | 0 skipped | 0 failed"
                : $"{validationTag} | 0 created | 0 skipped | 1 failed";

            LogLevel summaryLevel = result.Success ? LogLevel.Success : LogLevel.Error;
            _log.Add(summaryLevel, $"=== RESULT: {summary} ===");

            _log.Add(LogLevel.Info, "Execute() exited");

            Completed?.Invoke(result);
        }

        public string GetName() => "Roof Creation Isolation Test - Run Handler";
    }
}
