using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RRLPV4.Models;
using Revit26_Plugin.RRLPV4.Services;

namespace Revit26_Plugin.RRLPV4.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private string _status = "Ready";
        private GraphicsStyle _selectedStyle;
        private string _selectedDivision = "Divide by 3";

        public Autodesk.Revit.DB.RoofBase SelectedRoof { get; }

        public ObservableCollection<string> Logs { get; } = new();
        public ObservableCollection<GraphicsStyle> LineStyles { get; } = new();
        public ObservableCollection<string> Divisions { get; } = new() { "Divide by 2", "Divide by 3", "Divide by 5" };

        public ICommand ExecuteCommand { get; }

        public MainViewModel(Autodesk.Revit.DB.RoofBase roof)
        {
            SelectedRoof = roof;
            ExecuteCommand = new RelayCommand(Execute);

            Logs.Add($"Loaded Roof Id: {roof.Id.Value}");
            LoadLineStyles();
        }

        public string StatusMessage
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public GraphicsStyle SelectedLineStyle
        {
            get => _selectedStyle ?? LineStyles.FirstOrDefault();
            set => SetProperty(ref _selectedStyle, value);
        }

        public string SelectedDivision
        {
            get => _selectedDivision;
            set => SetProperty(ref _selectedDivision, value);
        }

        private void LoadLineStyles()
        {
            Document doc = SelectedRoof.Document;

            var styles = new FilteredElementCollector(doc)
                .OfClass(typeof(GraphicsStyle))
                .Cast<GraphicsStyle>()
                .Where(s => s.GraphicsStyleCategory.CategoryType == CategoryType.Annotation)
                .ToList();

            foreach (var s in styles)
                LineStyles.Add(s);

            Logs.Add("Detail line styles loaded.");
        }

        private void Execute()
        {
            StatusMessage = "Processing...";
            Logs.Add("Execution started.");

            RoofService.ExecuteRoofProcessing(
                SelectedRoof,
                SelectedLineStyle,
                SelectedDivision,
                msg => Logs.Add(msg));

            StatusMessage = "Complete";
        }
    }
}
