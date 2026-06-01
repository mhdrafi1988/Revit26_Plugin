// =======================================================
// File: Services/Implementations/DialogService.cs
// Description: Dialog service implementation
// =======================================================

using Microsoft.Win32;
using Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces;
using System;
using System.IO;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Implementations
{
    public class DialogService : IDialogService
    {
        public string SelectFolder(string initialPath = null)
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select Export Folder",
                InitialDirectory = string.IsNullOrEmpty(initialPath) || !Directory.Exists(initialPath)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : initialPath
            };

            var result = dialog.ShowDialog();
            return result == true ? dialog.FolderName : null;
        }

        public MessageBoxResult ShowMessage(string message, string title,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information)
        {
            return MessageBox.Show(message, title, buttons, icon);
        }

        public bool Confirm(string message, string title = "Confirm")
        {
            var result = MessageBox.Show(message, title,
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            return result == MessageBoxResult.Yes;
        }
    }
}