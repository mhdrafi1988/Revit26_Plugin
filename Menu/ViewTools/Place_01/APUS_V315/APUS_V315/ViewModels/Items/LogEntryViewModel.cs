using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.APUS_V315.Models.Enums;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using System;

namespace Revit26_Plugin.APUS_V315.ViewModels.Items;

public sealed partial class LogEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private DateTime _timestamp;

    [ObservableProperty]
    private LogLevel _level;

    [ObservableProperty]
    private string _message = string.Empty;

    public string DisplayText => $"[{Timestamp:HH:mm:ss}] {Message}";
    public string TimeStampString => Timestamp.ToString("HH:mm:ss");

    public LogEntryViewModel(LogEntry entry)
    {
        Timestamp = entry.Timestamp;
        Level = entry.Level;
        Message = entry.Message;
    }

    public LogEntryViewModel(DateTime timestamp, LogLevel level, string message)
    {
        Timestamp = timestamp;
        Level = level;
        Message = message ?? string.Empty;
    }
}