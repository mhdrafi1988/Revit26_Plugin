using System;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByPoint.V028.Infrastructure.Helpers
{
    public static class DialogService
    {
        public static string SelectFolder(string initialPath = "")
        {
            try
            {
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select Folder for AutoSlope Exports",
                    InitialDirectory = initialPath
                };
                return dialog.ShowDialog() == true ? dialog.FolderName : null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error selecting folder: {ex.Message}\n\nUsing default folder.",
                    "Folder Selection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return initialPath;
            }
        }

        public static string ShowSaveFileDialog(string filter, string initialDirectory, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = filter,
                InitialDirectory = initialDirectory,
                FileName = defaultFileName
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}