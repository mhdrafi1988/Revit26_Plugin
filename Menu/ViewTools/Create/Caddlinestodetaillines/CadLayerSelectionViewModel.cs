using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit22_Plugin.ImportCadLines.ViewModels
{
    public class CadLayerSelectionViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<string> Layers { get; } = new ObservableCollection<string>();

        private string _selectedLayer;
        public string SelectedLayer
        {
            get => _selectedLayer;
            set { _selectedLayer = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
