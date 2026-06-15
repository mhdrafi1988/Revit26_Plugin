using Microsoft.Win32;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Revit26_Plugin.APUS_V315.Services.Implementation;

public sealed class WpfDialogService : IDialogService
{
    private Window? GetActiveWindow()
    {
        try
        {
            return Application.Current?.Windows.OfType<Window>().FirstOrDefault(x => x.IsActive)
                   ?? Application.Current?.MainWindow;
        }
        catch
        {
            return null;
        }
    }

    // ---------- Message Boxes ----------
    public void ShowInfo(string message, string title = "Information")
    {
        var owner = GetActiveWindow();
        MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    public void ShowWarning(string message, string title = "Warning")
    {
        var owner = GetActiveWindow();
        MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    public void ShowError(string message, string title = "Error")
    {
        var owner = GetActiveWindow();
        MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ShowConfirmation(string message, string title = "Confirm", bool showCancel = false)
    {
        var owner = GetActiveWindow();
        var result = MessageBox.Show(owner, message, title,
            showCancel ? MessageBoxButton.YesNoCancel : MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        return result == MessageBoxResult.Yes;
    }

    public bool? ShowYesNoCancel(string message, string title = "Confirm")
    {
        var owner = GetActiveWindow();
        var result = MessageBox.Show(owner, message, title, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return result switch
        {
            MessageBoxResult.Yes => true,
            MessageBoxResult.No => false,
            _ => null
        };
    }

    // ---------- File Dialogs ----------
    public string? ShowOpenFileDialog(string filter, string defaultFileName = "", string initialDirectory = "")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            RestoreDirectory = true,
            Multiselect = false
        };

        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        var owner = GetActiveWindow();
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter, string defaultFileName = "", string initialDirectory = "")
    {
        var dialog = new SaveFileDialog
        {
            Filter = filter,
            FileName = defaultFileName,
            RestoreDirectory = true
        };

        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        var owner = GetActiveWindow();
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }

    public string? ShowFolderBrowserDialog(string description = "Select Folder", string? initialDirectory = null)
    {
        // Simple input dialog for folder path
        return ShowInputDialog(description, "Enter folder path:", initialDirectory ?? "");
    }

    // ---------- Multiple File Selection ----------
    public IReadOnlyList<string> ShowOpenMultipleFilesDialog(string filter, string initialDirectory = "")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            RestoreDirectory = true,
            Multiselect = true
        };

        if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
            dialog.InitialDirectory = initialDirectory;

        var owner = GetActiveWindow();
        return dialog.ShowDialog(owner) == true ? dialog.FileNames : Array.Empty<string>();
    }

    // ---------- Clipboard Operations ----------
    public void CopyToClipboard(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text))
                return;
            Clipboard.SetText(text);
        }
        catch (Exception ex)
        {
            ShowWarning($"Failed to copy to clipboard: {ex.Message}", "Clipboard Error");
        }
    }

    public string? PasteFromClipboard()
    {
        try
        {
            return Clipboard.ContainsText() ? Clipboard.GetText() : null;
        }
        catch (Exception ex)
        {
            ShowWarning($"Failed to paste from clipboard: {ex.Message}", "Clipboard Error");
            return null;
        }
    }

    // ---------- Input Dialog - Using built-in WPF ----------
    public string? ShowInputDialog(string title, string message, string defaultValue = "")
    {
        var owner = GetActiveWindow();
        var dialog = new Window
        {
            Title = title,
            Width = 450,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            ShowInTaskbar = false,
            Background = SystemColors.WindowBrush
        };

        var grid = new Grid { Margin = new Thickness(15) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Message
        var messageBlock = new TextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 15),
            FontSize = 13
        };
        Grid.SetRow(messageBlock, 0);
        grid.Children.Add(messageBlock);

        // Input
        var textBox = new TextBox
        {
            Text = defaultValue,
            Margin = new Thickness(0, 0, 0, 20),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 13
        };
        Grid.SetRow(textBox, 1);
        grid.Children.Add(textBox);

        // Buttons
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var okButton = new Button
        {
            Content = "OK",
            Padding = new Thickness(25, 6, 25, 6),
            Margin = new Thickness(0, 0, 10, 0),
            IsDefault = true,
            MinWidth = 80
        };
        okButton.Click += (s, e) => { dialog.DialogResult = true; dialog.Close(); };
        buttonPanel.Children.Add(okButton);

        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(25, 6, 25, 6),
            IsCancel = true,
            MinWidth = 80
        };
        cancelButton.Click += (s, e) => { dialog.DialogResult = false; dialog.Close(); };
        buttonPanel.Children.Add(cancelButton);

        Grid.SetRow(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        dialog.Content = grid;

        dialog.Loaded += (s, e) =>
        {
            textBox.Focus();
            textBox.SelectAll();
        };

        return dialog.ShowDialog() == true ? textBox.Text : null;
    }

    // ---------- Busy Indicators ----------
    public IDisposable BeginBusy(string message = "Processing...")
    {
        var owner = GetActiveWindow();
        if (owner == null)
            return new DisposableAction(() => { });

        var cursor = owner.Cursor;
        owner.Cursor = Cursors.Wait;
        owner.IsEnabled = false;

        return new DisposableAction(() =>
        {
            owner.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                owner.Cursor = cursor;
                owner.IsEnabled = true;
            }));
        });
    }

    // ---------- Helper Classes ----------
    private sealed class DisposableAction : IDisposable
    {
        private readonly Action _action;
        public DisposableAction(Action action) => _action = action;
        public void Dispose() => _action?.Invoke();
    }
}