using Revit26_Plugin.APUS_V315.Models.Enums;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Services.Abstractions;

public record LogEntry(DateTime Timestamp, LogLevel Level, string Message);

public interface ILogService
{
    IReadOnlyList<LogEntry> Entries { get; }
    event EventHandler<LogEntry> LogEntryAdded;

    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message);
    void LogSuccess(string message);
    void Clear();
}