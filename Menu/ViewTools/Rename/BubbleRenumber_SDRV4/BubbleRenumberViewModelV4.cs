using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Revit26_Plugin.SDRV4.Relay;
using Revit26_Plugin.SDRV4.Services;

namespace Revit26_Plugin.SDRV4.ViewModels
{
    public class BubbleRenumberViewModelV4 : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        // SHEET LIST
        public ObservableCollection<ViewSheet> Sheets { get; }

        private ViewSheet _selectedSheet;
        public ViewSheet SelectedSheet
        {
            get => _selectedSheet;
            set { _selectedSheet = value; OnPropertyChanged(); }
        }

        // STARTING NUMBER
        private string _startNum = "1";
        public string StartingDetailNumber
        {
            get => _startNum;
            set { _startNum = value; OnPropertyChanged(); }
        }

        // THRESHOLD (MM)
        private string _thresholdMm = "20";
        public string VerticalThresholdMm
        {
            get => _thresholdMm;
            set { _thresholdMm = value; OnPropertyChanged(); }
        }

        // UI LOG TEXT
        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(); }
        }

        // COMMAND
        public RelayCommandSDRV4 RunCommand { get; }

        public BubbleRenumberViewModelV4(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;

            // LOAD SHEETS
            Sheets = new ObservableCollection<ViewSheet>(
                new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber)
            );

            // PUT ACTIVE SHEET AT TOP IF APPLICABLE
            if (_doc.ActiveView is ViewSheet av)
            {
                var active = Sheets.FirstOrDefault(s => s.Id == av.Id);
                if (active != null)
                {
                    Sheets.Remove(active);
                    Sheets.Insert(0, active);
                }
            }

            SelectedSheet = Sheets.FirstOrDefault();

            // WIRE COMMAND
            RunCommand = new RelayCommandSDRV4(_ => ExecuteRenumber());
        }

        private void ExecuteRenumber()
        {
            LogText = ""; // CLEAR LOG

            // VALIDATION
            if (!int.TryParse(StartingDetailNumber, out int startNum) || startNum <= 0)
            {
                LogText += "⚠ Invalid starting number.\n";
                return;
            }

            if (!double.TryParse(VerticalThresholdMm, out double thresholdMm) || thresholdMm <= 0)
            {
                LogText += "⚠ Invalid vertical threshold (mm).\n";
                return;
            }

            // CONVERT MM → FT
            double thresholdFt = UnitUtils.ConvertToInternalUnits(thresholdMm, UnitTypeId.Millimeters);

            // RUN SERVICE
            var result = BubbleRenumberServiceV4.Run(_doc, SelectedSheet, startNum, thresholdFt);

            // WRITE SUMMARY TO LOG
            LogText +=
$"Sheet: {result.SheetNumber} - {result.SheetName}\n" +
$"Total: {result.Total}\n" +
$"Success: {result.Success}\n" +
$"Failed: {result.Failed.Count}\n\n" +
result.LogMessage;
        }

        // NOTIFY PROPERTY CHANGED
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
