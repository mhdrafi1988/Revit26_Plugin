using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_30_07.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint_30_07.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByPoint_30_07.ViewModels
{
    public class AutoSlopeViewModel : INotifyPropertyChanged
    {
        // =====================================================
        // INotifyPropertyChanged
        // =====================================================
        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        // =====================================================
        // COMBOBOX SOURCE
        // =====================================================
        public List<double> SlopeOptions { get; } =
            new List<double> { 0.5, 1.0, 1.5, 2.0, 2.5 };

        // =====================================================
        // INPUTS (BOUND FROM UI)
        // =====================================================
        private double _slopePercent = 1.5;
        public double SlopePercent
        {
            get => _slopePercent;
            set
            {
                _slopePercent = value;
                Raise();
            }
        }

        private int _thresholdMeters = 50;
        public int ThresholdMeters
        {
            get => _thresholdMeters;
            set
            {
                _thresholdMeters = value;
                Raise();
            }
        }

        // =====================================================
        // RESULTS (UPDATED BY ENGINE)
        // =====================================================
        private int _verticesProcessed;
        public int VerticesProcessed
        {
            get => _verticesProcessed;
            set
            {
                _verticesProcessed = value;
                Raise();
                Raise(nameof(SummaryText));
            }
        }

        private int _verticesSkipped;
        public int VerticesSkipped
        {
            get => _verticesSkipped;
            set
            {
                _verticesSkipped = value;
                Raise();
                Raise(nameof(SummaryText));
            }
        }

        private int _drainCount;
        public int DrainCount
        {
            get => _drainCount;
            set
            {
                _drainCount = value;
                Raise();
                Raise(nameof(SummaryText));
            }
        }

        // ---------------- HIGHEST ELEVATION ----------------
        private double _highestElevation_mm;
        public double HighestElevation_mm
        {
            get => _highestElevation_mm;
            set
            {
                _highestElevation_mm = value;
                Raise();
                Raise(nameof(HighestElevationDisplay));
                Raise(nameof(SummaryText));
            }
        }

        public string HighestElevationDisplay =>
            $"{HighestElevation_mm:0} mm";

        // ---------------- LONGEST PATH ----------------
        private double _longestPath_m;
        public double LongestPath_m
        {
            get => _longestPath_m;
            set
            {
                _longestPath_m = value;
                Raise();
                Raise(nameof(LongestPathDisplay));
                Raise(nameof(SummaryText));
            }
        }

        public string LongestPathDisplay =>
            $"{LongestPath_m:0.00} m";

        private int _runDuration_sec;
        public int RunDuration_sec
        {
            get => _runDuration_sec;
            set
            {
                _runDuration_sec = value;
                Raise();
                Raise(nameof(RunDurationDisplay));
                Raise(nameof(SummaryText));
            }
        }

        public string RunDurationDisplay =>
            $"{RunDuration_sec} sec";

        private string _runDate;
        public string RunDate
        {
            get => _runDate;
            set
            {
                _runDate = value;
                Raise();
                Raise(nameof(SummaryText));
            }
        }

        // =====================================================
        // SUMMARY (OPTION A)
        // =====================================================
        public string SummaryText =>
$@"Vertices Processed : {VerticesProcessed}
Vertices Skipped   : {VerticesSkipped}
Drain Count        : {DrainCount}
Highest Elevation  : {HighestElevationDisplay}
Longest Path       : {LongestPathDisplay}
Run Duration       : {RunDurationDisplay}
Run Date           : {RunDate}";

        // =====================================================
        // COMMAND STATE
        // =====================================================
        private bool _hasRun;
        public bool HasRun
        {
            get => _hasRun;
            set
            {
                _hasRun = value;
                Raise();
            }
        }

        // =====================================================
        // COMMAND
        // =====================================================
        public ICommand RunCommand { get; }

        // =====================================================
        // CONTEXT (NOT BOUND)
        // =====================================================
        private readonly UIDocument _uidoc;
        private readonly UIApplication _app;
        private readonly ElementId _roofId;
        private readonly List<XYZ> _drainPoints;
        private readonly Action<string> _log;

        // =====================================================
        // CONSTRUCTOR
        // =====================================================
        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints,
            Action<string> log)
        {
            _uidoc = uidoc;
            _app = app;
            _roofId = roofId;
            _drainPoints = drainPoints;
            _log = log;

            RunCommand = new RelayCommand(
                _ => RunAutoSlope(),
                _ => !HasRun);

            AutoSlopeEventManager.Init();
        }

        // =====================================================
        // RUN AUTOSLOPE
        // =====================================================
        private void RunAutoSlope()
        {
            if (HasRun)
                return;

            HasRun = true;

            _log?.Invoke("Starting AutoSlope…");

            AutoSlopeHandler.Payload = new AutoSlopePayload
            {
                RoofId = _roofId,
                DrainPoints = _drainPoints,
                SlopePercent = SlopePercent,
                ThresholdMeters = ThresholdMeters,
                Vm = this,
                Log = _log
            };

            AutoSlopeEventManager.Event.Raise();
        }
    }

    // =====================================================
    // RELAY COMMAND
    // =====================================================
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
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
