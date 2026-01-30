using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.Engine;
using Revit26_Plugin.AutoSlopeByPoint.Handlers;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByPoint.ViewModels
{
    public class AutoSlopeViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly UIApplication _app;
        private readonly Action<string> _log;

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // -----------------------------
        // INPUTS
        // -----------------------------
        private double _selectedSlope = 1.5;
        public double SelectedSlope
        {
            get => _selectedSlope;
            set { _selectedSlope = value; OnPropertyChanged(); }
        }

        private double _thresholdMeters = 50;
        public double ThresholdMeters
        {
            get => _thresholdMeters;
            set { _thresholdMeters = value; OnPropertyChanged(); }
        }

        // -----------------------------
        // SUMMARY
        // -----------------------------
        private int _verticesProcessed;
        public int VerticesProcessed
        {
            get => _verticesProcessed;
            set { _verticesProcessed = value; OnPropertyChanged(); }
        }

        private int _verticesSkipped;
        public int VerticesSkipped
        {
            get => _verticesSkipped;
            set { _verticesSkipped = value; OnPropertyChanged(); }
        }

        private double _highestElevation;
        public double HighestElevation
        {
            get => _highestElevation;
            set { _highestElevation = value; OnPropertyChanged(); }
        }

        private double _longestPathMeters;
        public double LongestPathMeters
        {
            get => _longestPathMeters;
            set { _longestPathMeters = value; OnPropertyChanged(); }
        }

        private string _summaryText;
        public string SummaryText
        {
            get => _summaryText;
            set { _summaryText = value; OnPropertyChanged(); }
        }

        // -----------------------------
        // COMMAND LOCK
        // -----------------------------
        private bool _hasRun = false;
        public bool HasRun
        {
            get => _hasRun;
            set { _hasRun = value; OnPropertyChanged(); }
        }

        // -----------------------------
        // COMMAND
        // -----------------------------
        public ObservableCollection<double> SlopeOptions { get; }
        public ICommand RunCommand { get; }

        public ElementId RoofId { get; }
        public List<XYZ> DrainPoints { get; }

        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drains,
            Action<string> log)
        {
            _uidoc = uidoc;
            _app = app;
            _log = log;

            RoofId = roofId;
            DrainPoints = drains;

            SlopeOptions = new ObservableCollection<double>
            {
                0.5, 1.0, 1.5, 2.0, 3.0
            };

            RunCommand = new RelayCommand(_ => RunAutoSlope(), _ => !HasRun);

            AutoSlopeEventManager.Initialize();
        }

        private void RunAutoSlope()
        {
            if (HasRun)
                return;

            HasRun = true;

            double slope = SelectedSlope;

            AutoSlopeHandler.Payload = new AutoSlopePayload
            {
                RoofId = RoofId,
                DrainPoints = DrainPoints,
                SlopePercent = slope,
                ThresholdMeters = ThresholdMeters,

                Log = _log,      // full color logging
                Vm = this
            };

            AutoSlopeEventManager.Event.Raise();
        }
    }

    // RelayCommand (unchanged)
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
            => _canExecute == null || _canExecute(parameter);

        public void Execute(object parameter)
            => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
