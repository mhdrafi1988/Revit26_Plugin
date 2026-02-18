using System;
using System.IO;
using System.Windows;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Infrastructure.Helpers
{
    public static class DialogService
    {
        public static string SelectFolder(string initialPath = "")
        {
            return SelectFolderWpf(initialPath);
        }

        private static string SelectFolderWpf(string initialPath)
        {
            try
            {
                var dialog = new System.Windows.Window
                {
                    Title = "Select Folder for AutoSlope Exports",
                    Width = 400,
                    Height = 150,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };

                var stackPanel = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
                var textBox = new System.Windows.Controls.TextBox
                {
                    Text = initialPath,
                    Margin = new Thickness(0, 0, 0, 10)
                };

                var buttonPanel = new System.Windows.Controls.DockPanel();
                var browseButton = new System.Windows.Controls.Button
                {
                    Content = "Browse",
                    Width = 75,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                var okButton = new System.Windows.Controls.Button
                {
                    Content = "OK",
                    Width = 75,
                    Margin = new Thickness(0, 0, 10, 0)
                };
                var cancelButton = new System.Windows.Controls.Button { Content = "Cancel", Width = 75 };

                string result = null;
                bool dialogResult = false;

                browseButton.Click += (s, e) =>
                {
                    var openDialog = new Microsoft.Win32.OpenFileDialog
                    {
                        Filter = "All files (*.*)|*.*",
                        InitialDirectory = textBox.Text,
                        Title = "Select a folder (choose any file in the folder)",
                        CheckFileExists = true,
                        ValidateNames = false
                    };
                    if (openDialog.ShowDialog() == true)
                        textBox.Text = Path.GetDirectoryName(openDialog.FileName);
                };

                okButton.Click += (s, e) => { result = textBox.Text; dialogResult = true; dialog.Close(); };
                cancelButton.Click += (s, e) => { dialogResult = false; dialog.Close(); };

                buttonPanel.Children.Add(browseButton);
                buttonPanel.Children.Add(okButton);
                buttonPanel.Children.Add(cancelButton);

                stackPanel.Children.Add(new System.Windows.Controls.TextBlock
                {
                    Text = "Enter or select folder path:",
                    Margin = new Thickness(0, 0, 0, 5)
                });
                stackPanel.Children.Add(textBox);
                stackPanel.Children.Add(buttonPanel);

                dialog.Content = stackPanel;
                dialog.ShowDialog();
                return dialogResult ? result : null;
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