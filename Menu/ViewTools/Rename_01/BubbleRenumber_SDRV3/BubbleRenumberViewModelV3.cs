using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Revit22_Plugin.SDRV3.Services;
using Revit22_Plugin.SDRV3.Renumber;
using Revit22_Plugin.SDRV3.Relay;

namespace Revit22_Plugin.SDRV3
{
    public class BubbleRenumberViewModelV3 : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public ObservableCollection<ViewSheet> Sheets { get; }

        private ViewSheet _selectedSheet;
        public ViewSheet SelectedSheet
        {
            get => _selectedSheet;
            set { _selectedSheet = value; OnPropertyChanged(); }
        }

        private string _startNum = "1";
        public string StartingDetailNumber
        {
            get => _startNum;
            set { _startNum = value; OnPropertyChanged(); }
        }

        private string _thresholdMm = "20";
        public string VerticalThresholdMm
        {
            get => _thresholdMm;
            set { _thresholdMm = value; OnPropertyChanged(); }
        }

        private string _logText = "";
        public string LogText
        {
            get => _logText;
            set { _logText = value; OnPropertyChanged(); }
        }

        public RelayCommandSDRV3 RunCommand { get; }

        public BubbleRenumberViewModelV3(UIDocument uidoc)
        {
            _uidoc = uidoc;
            _doc = uidoc.Document;

            Sheets = new ObservableCollection<ViewSheet>(
                new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber)
            );

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

            RunCommand = new RelayCommandSDRV3(_ => ExecuteRenumber());
        }

        private void ExecuteRenumber()
        {
            LogText = "";

            if (!int.TryParse(StartingDetailNumber, out int startNum) || startNum <= 0)
            {
                LogText = "❌ Invalid starting number.";
                return;
            }

            if (!double.TryParse(VerticalThresholdMm, out double thresholdMm) || thresholdMm <= 0)
            {
                LogText = "❌ Invalid vertical threshold (mm).";
                return;
            }

            double thresholdFt = UnitUtils.ConvertToInternalUnits(thresholdMm, UnitTypeId.Millimeters);

            var result = BubbleRenumberServiceV4.Run(_doc, SelectedSheet, startNum, thresholdFt);

            LogText =
$"Sheet: {result.SheetNumber} - {result.SheetName}\n" +
$"Total: {result.Total}\n" +
$"Success: {result.Success}\n" +
$"Failed: {result.Failed.Count}\n\n" +
result.LogMessage;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
