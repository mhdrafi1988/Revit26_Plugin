using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RRLPV4.Models;
using Revit26_Plugin.RRLPV4.Services;
using Revit26_Plugin.RRLPV4.Utils;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace Revit26_Plugin.RRLPV4.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        private string _status = "Ready";
        private GraphicsStyle _selectedStyle;
        private string _selectedDivision = "Divide by 3";
        private readonly Document _doc;
        private readonly UIDocument _uidoc;

        public RoofBase SelectedRoof { get; }

        public ObservableCollection<string> Logs { get; } = new();
        public ObservableCollection<GraphicsStyle> LineStyles { get; } = new();
        public ObservableCollection<string> Divisions { get; } = new() { "Divide by 2", "Divide by 3", "Divide by 5" };

        public ICommand ExecuteCommand { get; }
        public ICommand CloseCommand { get; }

        public MainViewModel(RoofBase roof, UIDocument uidoc)
        {
            SelectedRoof = roof;
            _uidoc = uidoc;
            _doc = roof.Document;

            ExecuteCommand = new RelayCommand(Execute, CanExecute);
            CloseCommand = new RelayCommand(Close);

            Logger.LogInfo($"Loaded Roof Id: {roof.Id.Value}");
            LoadLogsFromLogger();
            LoadLineStyles();
        }

        private void LoadLogsFromLogger()
        {
            Logs.Clear();
            foreach (var entry in Logger.Entries.TakeLast(50))
                Logs.Add(entry);
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
            try
            {
                var styles = new FilteredElementCollector(_doc)
                    .OfClass(typeof(GraphicsStyle))
                    .Cast<GraphicsStyle>()
                    .Where(s => s.GraphicsStyleCategory?.CategoryType == CategoryType.Annotation)
                    .ToList();

                foreach (var s in styles)
                    LineStyles.Add(s);

                Logger.LogInfo($"Loaded {LineStyles.Count} detail line styles.");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Loading line styles");
            }
        }

        private bool CanExecute() => SelectedLineStyle != null;

        private void Execute()
        {
            try
            {
                StatusMessage = "Select points...";

                if (!PointSelectionService.PickTwoFarPoints(_uidoc, out XYZ p1, out XYZ p2))
                {
                    StatusMessage = "Cancelled";
                    return;
                }

                StatusMessage = "Processing...";
                Logger.LogInfo("Execution started.");

                var roofData = new RoofData
                {
                    SelectedRoof = SelectedRoof,
                    Point1 = p1,
                    Point2 = p2,
                    UsedLineStyle = SelectedLineStyle,
                    DivisionStrategy = SelectedDivision,
                    StartTime = DateTime.Now
                };

                RoofService.ExecuteRoofProcessing(roofData, msg =>
                {
                    Logs.Add(msg);
                    Logger.LogInfo(msg);
                });

                roofData.EndTime = DateTime.Now;
                roofData.IsSuccess = true;

                Logger.LogInfo($"Completed: {roofData.GetSummary()}");
                StatusMessage = "Complete";
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Execution failed");
                StatusMessage = "Failed - Check Logs";
            }
        }

        private void Close()
        {
            System.Windows.Application.Current.Windows
                .OfType<Views.MainWindow>()
                .FirstOrDefault()?.Close();
        }
    }
}