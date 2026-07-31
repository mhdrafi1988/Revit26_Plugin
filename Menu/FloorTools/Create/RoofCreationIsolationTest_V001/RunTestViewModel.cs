using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofCreationIsolationTest.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.RoofCreationIsolationTest.V001.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace Revit26_Plugin.RoofCreationIsolationTest.V001.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the single-button diagnostic window. Owns the ExternalEvent/handler
    /// pair (created in the constructor, per your convention that ExternalEvent.Create()
    /// must run in a valid API execution context — the ViewModel is constructed from the
    /// command's Execute(), which qualifies). No other inputs exist on this tool by design.
    ///
    /// LogEntries is only ever mutated through the ThreadSafeLogSink wrapper below —
    /// RunTestHandler.Execute() runs on the Revit API thread, and direct
    /// ObservableCollection.Add() calls from that thread do not reach the WPF-bound
    /// ListBox (this was the original "log area never updates" bug).
    /// </summary>
    public partial class RunTestViewModel : ObservableObject
    {
        private readonly RunTestHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        [ObservableProperty]
        private bool isRunEnabled = true;

        [ObservableProperty]
        private string summaryText = string.Empty;

        public RunTestViewModel()
        {
            // Captures the Dispatcher for whichever thread constructs this ViewModel —
            // that is the WPF UI thread, since construction happens from
            // RunTestCommand.Execute() before the window is shown.
            var logSink = new ThreadSafeLogSink(LogEntries, Dispatcher.CurrentDispatcher);

            _handler = new RunTestHandler(logSink);
            _handler.Completed += OnHandlerCompleted;

            // Per project convention: ExternalEvent.Create() called here in the
            // ViewModel constructor (invoked from the command's Execute()), not
            // lazily inside Execute() of the handler itself.
            _externalEvent = ExternalEvent.Create(_handler);
        }

        [RelayCommand]
        private void Run()
        {
            LogEntries.Add(new LogEntry(LogLevel.Info, "=== Run clicked ==="));
            IsRunEnabled = false;
            SummaryText = string.Empty;

            LogEntries.Add(new LogEntry(LogLevel.Info, "ExternalEvent.Raise() called, awaiting Revit API context"));
            _externalEvent.Raise();
        }

        private void OnHandlerCompleted(Core.Models.RoofTestResult result)
        {
            // Completed is invoked on the Revit API thread inside Execute(); marshal
            // back to the UI thread before touching bound properties.
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsRunEnabled = true;

                string validationTag = result.ValidationPassed
                    ? "Validation: PASS"
                    : $"Validation: FAIL ({result.ValidationIssues.Count} issue(s))";

                SummaryText = result.Success
                    ? $"{validationTag} | 1 created | 0 skipped | 0 failed"
                    : $"{validationTag} | 0 created | 0 skipped | 1 failed";
            });
        }

        [RelayCommand]
        private void CopyAll()
        {
            var text = string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString()));
            TrySetClipboard(text);
        }

        [RelayCommand]
        private void CopySelected(System.Collections.IList? selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0)
            {
                TrySetClipboard(string.Empty);
                return;
            }

            var text = string.Join(Environment.NewLine,
                selectedItems.Cast<LogEntry>().Select(e => e.ToString()));
            TrySetClipboard(text);
        }

        [RelayCommand]
        private void ClearLog()
        {
            LogEntries.Clear();
            SummaryText = string.Empty;
        }

        [RelayCommand]
        private void ExportLog()
        {
            try
            {
                var dialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Text files (*.txt)|*.txt",
                    FileName = $"RoofCreationIsolationTest_Logs_{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HH-mm}.txt"
                };

                if (dialog.ShowDialog() == true)
                {
                    var text = string.Join(Environment.NewLine, LogEntries.Select(e => e.ToString()));
                    File.WriteAllText(dialog.FileName, text, Encoding.UTF8);
                    LogEntries.Add(new LogEntry(LogLevel.Success, $"Log exported to: {dialog.FileName}"));
                }
            }
            catch (Exception ex)
            {
                LogEntries.Add(new LogEntry(LogLevel.Error, $"Export failed: {ex.Message}"));
            }
        }

        private static void TrySetClipboard(string text)
        {
            try
            {
                Clipboard.SetText(string.IsNullOrEmpty(text) ? " " : text);
            }
            catch
            {
                // Clipboard access can transiently fail (e.g. another process holding
                // the clipboard); silently ignored per existing suite convention of
                // not surfacing non-critical clipboard errors as dialogs.
            }
        }
    }
}
