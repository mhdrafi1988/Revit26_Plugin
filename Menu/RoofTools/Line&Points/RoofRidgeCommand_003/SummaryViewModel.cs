using System.Collections.Generic;
using System.ComponentModel;
using Revit22_Plugin.RRLPV3.Models;

namespace Revit22_Plugin.RRLPV3.ViewModels
{
    /// <summary>
    /// ViewModel for the Summary Window after processing.
    /// </summary>
    public class SummaryViewModel : INotifyPropertyChanged
    {
        private readonly RoofData _data;

        public SummaryViewModel(RoofData data)
        {
            _data = data;
        }

        // -------------------------
        // Properties displayed in UI
        // -------------------------

        public string ResultStatus =>
            _data.IsSuccess ? "Completed Successfully" : "Completed with Errors";

        public int DetailLinesCreated => _data.DetailLinesCreated;
        public int PerpendicularLinesCreated => _data.PerpendicularLinesCreated;
        public int ShapePointsAdded => _data.ShapePointsAdded;

        public string Duration => _data.Duration.ToString(@"mm\:ss");

        public IEnumerable<string> Logs => _data.LogMessages;

        // -------------------------
        // INotifyPropertyChanged
        // -------------------------

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
