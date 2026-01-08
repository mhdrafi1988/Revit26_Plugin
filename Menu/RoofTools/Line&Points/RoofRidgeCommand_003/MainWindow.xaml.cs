using System.Windows;
using Revit22_Plugin.RRLPV3.ViewModels;

namespace Revit22_Plugin.RRLPV3.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // No ViewModel created here.
            // DataContext is assigned externally from the command.
        }
    }
}
