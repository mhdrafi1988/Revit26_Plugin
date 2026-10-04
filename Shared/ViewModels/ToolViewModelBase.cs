using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.Shared.ViewModels
{
    /// <summary>
    /// Shared base for all tool ViewModels.
    /// Provides: log collection + helpers, log commands, and the three
    /// ToolWindowShell-bound status properties (IsRunning / Progress / SummaryText).
    /// </summary>
    public abstract partial class ToolViewModelBase : ObservableObject
    {
        private const int MaxLogEntries = 2000;

        // ── ToolWindowShell status strip ──────────────────────────────────────
        [ObservableProperty] private bool _isRunning;
        [ObservableProperty] private double _progress;
        [ObservableProperty] private string _summaryText = string.Empty;

        // ── Log ───────────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        /// <summary>Bound from the log ListBox's SelectedItems in code-behind;
        /// set before calling CopySelectedLogCommand.</summary>
        public IList<LogEntry> SelectedLogEntries { get; set; } = new List<LogEntry>();

        // ── Log commands ──────────────────────────────────────────────────────
        [RelayCommand]
        private void ClearLog() => OnClearLog();

        /// <summary>Override to add tool-specific reset alongside the log clear
        /// (e.g. reset metrics). Always call base.OnClearLog() to clear the list.</summary>
        protected virtual void OnClearLog() => LogEntries.Clear();

        [RelayCommand]
        private void CopyAllLog()
        {
            var text = string.Join(System.Environment.NewLine, LogEntries.Select(e => e.ToString()));
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); } catch { /* clipboard busy */ }
        }

        [RelayCommand]
        private void CopySelectedLog()
        {
            if (SelectedLogEntries == null || SelectedLogEntries.Count == 0) return;
            var text = string.Join(System.Environment.NewLine, SelectedLogEntries.Select(e => e.ToString()));
            if (string.IsNullOrEmpty(text)) return;
            try { Clipboard.SetText(text); } catch { /* clipboard busy */ }
        }

        // ── Log helper ────────────────────────────────────────────────────────
        protected void AddLog(LogLevel level, string message)
        {
            LogEntries.Add(new LogEntry(level, message));
            while (LogEntries.Count > MaxLogEntries)
                LogEntries.RemoveAt(0);
        }
    }
}
