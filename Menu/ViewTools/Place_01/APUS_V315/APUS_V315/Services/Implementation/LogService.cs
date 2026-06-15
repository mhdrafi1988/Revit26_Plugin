using Revit26_Plugin.APUS_V315.Models.Enums;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using System.Collections.Generic;
using System;

namespace Revit26_Plugin.APUS_V315.Services.Implementation;

public sealed class LogService : ILogService, IDisposable
{
    private readonly List<LogEntry> _entries = new();
    private readonly object _lock = new();
    private const int MaxEntries = 5000;

    public IReadOnlyList<LogEntry> Entries
    {
        get
        {
            lock (_lock)
                return _entries.ToArray();
        }
    }

    public event EventHandler<LogEntry>? LogEntryAdded;

    public void LogInfo(string message) => AddEntry(LogLevel.Info, $"ℹ️ {message}");
    public void LogWarning(string message) => AddEntry(LogLevel.Warning, $"⚠️ {message}");
    public void LogError(string message) => AddEntry(LogLevel.Error, $"❌ {message}");
    public void LogSuccess(string message) => AddEntry(LogLevel.Success, $"✅ {message}");

    private void AddEntry(LogLevel level, string message)
    {
        var entry = new LogEntry(DateTime.Now, level, message);

        lock (_lock)
        {
            _entries.Add(entry);

            while (_entries.Count > MaxEntries)
                _entries.RemoveAt(0);
        }

        LogEntryAdded?.Invoke(this, entry);
    }

    public void Clear()
    {
        lock (_lock)
            _entries.Clear();
    }

    public void Dispose()
    {
        LogEntryAdded = null;
    }
}