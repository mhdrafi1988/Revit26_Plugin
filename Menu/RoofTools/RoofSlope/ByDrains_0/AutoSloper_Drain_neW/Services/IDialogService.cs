// =======================================================
// File: Services/Interfaces/IDialogService.cs
// Description: Service contract for UI dialogs
// =======================================================

using System.Windows;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Services.Interfaces
{
    /// <summary>
    /// Provides UI dialog services
    /// </summary>
    public interface IDialogService
    {
        /// <summary>
        /// Shows a folder browser dialog
        /// </summary>
        /// <param name="initialPath">Initial folder path</param>
        /// <returns>Selected folder path or null if cancelled</returns>
        string SelectFolder(string initialPath = null);

        /// <summary>
        /// Shows a message box
        /// </summary>
        /// <param name="message">Message text</param>
        /// <param name="title">Dialog title</param>
        /// <param name="buttons">Button configuration</param>
        /// <param name="icon">Icon type</param>
        /// <returns>Message box result</returns>
        MessageBoxResult ShowMessage(
            string message,
            string title,
            MessageBoxButton buttons = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.Information);

        /// <summary>
        /// Shows a confirmation dialog
        /// </summary>
        /// <param name="message">Confirmation message</param>
        /// <param name="title">Dialog title</param>
        /// <returns>True if user confirms</returns>
        bool Confirm(string message, string title = "Confirm");
    }
}