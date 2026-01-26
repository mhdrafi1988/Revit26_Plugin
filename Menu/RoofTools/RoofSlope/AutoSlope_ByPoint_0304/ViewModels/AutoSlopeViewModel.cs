// =======================================================
// File: AutoSlopeViewModel.cs
// Purpose: UI state + summary binding
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.ExternalEvents;
using Revit26_Plugin.AutoSlopeByPoint.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Revit26_Plugin.AutoSlopeByPoint.ViewModels
{
    public class AutoSlopeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void Raise([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public List<double> SlopeOptions { get; } =
            new() { 0.5, 1.0, 1.5, 2.0, 2.5 };

        public double SlopePercent { get; set; } = 1.5;
        public int ThresholdMeters { get; set; } = 50;

        public int VerticesProcessed { get => _vp; set { _vp = value; Raise(); Raise(nameof(SummaryText)); } }
        public int VerticesSkipped { get => _vs; set { _vs = value; Raise(); Raise(nameof(SummaryText)); } }
        public int DrainCount { get => _dc; set { _dc = value; Raise(); Raise(nameof(SummaryText)); } }

        private int _vp, _vs, _dc;

        public double HighestElevation_mm { get => _he; set { _he = value; Raise(); Raise(nameof(SummaryText)); } }
        private double _he;

        public double AverageElevation_ft { get => _ae; set { _ae = value; Raise(); Raise(nameof(SummaryText)); } }
        private double _ae;

        public double LongestPath_m { get => _lp; set { _lp = value; Raise(); Raise(nameof(SummaryText)); } }
        private double _lp;

        public int RunDuration_sec { get => _rd; set { _rd = value; Raise(); Raise(nameof(SummaryText)); } }
        private int _rd;

        public string RunDate { get => _rdate; set { _rdate = value; Raise(); Raise(nameof(SummaryText)); } }
        private string _rdate;

        public string SummaryText =>
$@"Vertices Processed : {VerticesProcessed}
Vertices Skipped   : {VerticesSkipped}
Drain Count        : {DrainCount}
Highest Elevation  : {HighestElevation_mm:0} mm
Average Elevation  : {AverageElevation_ft:0.####} ft
Longest Path       : {LongestPath_m:0.00} m
Run Duration       : {RunDuration_sec} sec
Run Date           : {RunDate}";

        public bool HasRun { get; set; }
        public ICommand RunCommand { get; }

        public AutoSlopeViewModel(
            UIDocument uidoc,
            UIApplication app,
            ElementId roofId,
            List<XYZ> drainPoints,
            Action<string> log)
        {
            RunCommand = new RelayCommand(_ => RunAutoSlope(), _ => !HasRun);
            AutoSlopeEventManager.Init();
        }

        private void RunAutoSlope()
        {
            HasRun = true;

            AutoSlopeHandler.Payload = new AutoSlopePayload
            {
                Vm = this
            };

            AutoSlopeEventManager.Event.Raise();
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;
        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);
        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
