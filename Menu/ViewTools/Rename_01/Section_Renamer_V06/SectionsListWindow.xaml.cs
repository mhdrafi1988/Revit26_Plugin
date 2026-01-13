using System.Windows;

namespace Revit26_Plugin.SARV6.Views;

public partial class SectionsListWindow : Window
{
    public SectionsListWindow(object vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
