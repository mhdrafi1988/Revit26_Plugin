using Revit26_Plugin.DeleteLineStyles.V001.UI.ViewModels;
using Revit26_Plugin.Shared.Services;
using System;
using System.Windows;

namespace Revit26_Plugin.DeleteLineStyles.V001.UI.Views
{
    /// <summary>
    /// Delete Line Styles window; everything is bound to <see cref="DeleteLineStylesViewModel"/>.
    /// </summary>
    public partial class DeleteLineStylesWindow : Window
    {
        /// <summary>Creates the window for <paramref name="viewModel"/>.</summary>
        public DeleteLineStylesWindow(DeleteLineStylesViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            Title = ToolCatalog.DeleteLineStyles.Title;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }
}
