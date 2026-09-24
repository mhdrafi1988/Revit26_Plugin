using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Core.Models;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB002.UI.Views
{
    /// <summary>
    /// Modal 3D-view picker: typable/searchable list, offered both when the
    /// user clicks a Type row and when a checked-elements action button is
    /// pressed. Set <see cref="Views"/> and <see cref="PromptText"/> before
    /// calling ShowDialog(); on a true DialogResult, <see cref="SelectedView"/>
    /// holds the user's pick.
    /// </summary>
    public partial class View3DPickerWindow : Window
    {
        public List<View3DOption> Views { get; set; } = new();
        public View3DOption SelectedView { get; private set; }

        public View3DPickerWindow()
        {
            InitializeComponent();
        }

        public void Initialize(string prompt, List<View3DOption> views)
        {
            PromptText.Text = prompt;
            Views = views;

            ViewsListBox.ItemsSource = CollectionViewSource.GetDefaultView(Views);
            if (Views.Count > 0)
                ViewsListBox.SelectedIndex = 0;
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            var view = ViewsListBox.ItemsSource as ICollectionView;
            if (view == null) return;

            string query = SearchBox.Text.Trim();
            view.Filter = query.Length == 0
                ? (System.Predicate<object>)null
                : o => ((View3DOption)o).Name.IndexOf(query, System.StringComparison.OrdinalIgnoreCase) >= 0;

            if (ViewsListBox.Items.Count > 0)
                ViewsListBox.SelectedIndex = 0;
        }

        private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                TryConfirm();
                e.Handled = true;
            }
            else if (e.Key == Key.Down && ViewsListBox.Items.Count > 0)
            {
                ViewsListBox.Focus();
                if (ViewsListBox.SelectedIndex < 0) ViewsListBox.SelectedIndex = 0;
                e.Handled = true;
            }
        }

        private void ViewsListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e) => TryConfirm();

        private void OpenView_Click(object sender, RoutedEventArgs e) => TryConfirm();

        private void No_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TryConfirm()
        {
            var selected = ViewsListBox.SelectedItem as View3DOption
                ?? ViewsListBox.Items.Cast<View3DOption>().FirstOrDefault();
            if (selected == null)
            {
                MessageBox.Show(this, "No 3D view matches your search.", "Worksets & Elements Browser",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            SelectedView = selected;
            DialogResult = true;
            Close();
        }
    }
}
