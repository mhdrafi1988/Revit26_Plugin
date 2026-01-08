using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.Models;
using System.Collections.Generic;
using System.ComponentModel;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V08.ViewModels
{
    public class SummaryViewModel : INotifyPropertyChanged
    {
        private readonly ExecutionResult _result;

        public SummaryViewModel(ExecutionResult result)
        {
            _result = result;
        }

        public string ResultStatus => _result.StatusMessage;
        public int DetailLinesCreated => _result.DetailLinesCreated;
        public int PerpendicularLinesCreated => _result.PerpendicularLinesCreated;
        public int ShapePointsAdded => _result.ShapePointsAdded;
        public string Duration => _result.Duration.ToString(@"mm\:ss");
        public IEnumerable<string> Logs => _result.LogMessages;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string prop) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}