using Autodesk.Revit.DB;
using System.Windows;
using WorksetManager_01.ViewModels;

namespace WorksetManager_01.Views
{
    public partial class WorksetManagerWindow : Window
    {
        public WorksetManagerWindow(Document doc)
        {
            InitializeComponent();
            DataContext = new WorksetManagerViewModel(doc);
        }
    }
}
