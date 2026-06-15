using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Services.Abstractions;

public interface IDialogService
{
    // ---------- Message Boxes ----------
    void ShowInfo(string message, string title = "Information");
    void ShowWarning(string message, string title = "Warning");
    void ShowError(string message, string title = "Error");
    bool ShowConfirmation(string message, string title = "Confirm", bool showCancel = false);
    bool? ShowYesNoCancel(string message, string title = "Confirm");

    // ---------- File Dialogs ----------
    string? ShowOpenFileDialog(string filter, string defaultFileName = "", string initialDirectory = "");
    string? ShowSaveFileDialog(string filter, string defaultFileName = "", string initialDirectory = "");
    string? ShowFolderBrowserDialog(string description = "Select Folder", string? initialDirectory = null);

    // ---------- Multiple File Selection ----------
    IReadOnlyList<string> ShowOpenMultipleFilesDialog(string filter, string initialDirectory = "");

    // ---------- Clipboard Operations ----------
    void CopyToClipboard(string text);
    string? PasteFromClipboard();

    // ---------- Custom Dialogs ----------
    string? ShowInputDialog(string title, string message, string defaultValue = "");

    // ---------- Busy Indicators ----------
    IDisposable BeginBusy(string message = "Processing...");
}