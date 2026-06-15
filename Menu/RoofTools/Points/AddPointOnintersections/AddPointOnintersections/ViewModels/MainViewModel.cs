using System;
using System.Collections.ObjectModel;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AddPointOnintersections.ExternalEvents;
using Revit26_Plugin.AddPointOnintersections.Helpers;
using Revit26_Plugin.AddPointOnintersections.Models;

namespace Revit26_Plugin.AddPointOnintersections.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private RoofSelectionContext _context;
        private ExternalEvent _externalEvent;
        private AddPointsExternalEventHandler _handler;

        public MainViewModel()
        {
            Logs = new ObservableCollection<string>();
            SelectedRoofElementId = "-";
            SelectedDetailLineCount = 0;
            ShapePointsAddedCount = 0;
            ZeroElevationConfirmed = "-";
        }

        public ObservableCollection<string> Logs { get; }

        [ObservableProperty]
        private string selectedRoofElementId;

        [ObservableProperty]
        private int selectedDetailLineCount;

        [ObservableProperty]
        private int shapePointsAddedCount;

        [ObservableProperty]
        private string zeroElevationConfirmed;

        [ObservableProperty]
        private bool isBusy;

        public void Initialize(
            RoofSelectionContext context,
            ExternalEvent externalEvent,
            AddPointsExternalEventHandler handler)
        {
            _context = context;
            _externalEvent = externalEvent;
            _handler = handler;

            SelectedRoofElementId = context.RoofId.Value.ToString();
            SelectedDetailLineCount = context.DetailLineIds.Count;
            ShapePointsAddedCount = 0;
            ZeroElevationConfirmed = "-";

            ClearLogs();
        }

        [RelayCommand(CanExecute = nameof(CanExecuteAction))]
        private void Execute()
        {
            if (_context == null)
            {
                Log("Execution cannot start. Context is null.");
                return;
            }

            if (_externalEvent == null)
            {
                Log("Execution cannot start. ExternalEvent is null.");
                return;
            }

            if (_handler == null)
            {
                Log("Execution cannot start. Handler is null.");
                return;
            }

            _handler.SetRequest(_context);
            _externalEvent.Raise();
        }

        private bool CanExecuteAction()
        {
            return !IsBusy;
        }

        partial void OnIsBusyChanged(bool value)
        {
            ExecuteCommand.NotifyCanExecuteChanged();
        }

        public void Log(string message)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {message}";

            WpfDispatcherHelper.SafeInvoke(() =>
            {
                Logs.Add(line);
            });
        }

        private void ClearLogs()
        {
            WpfDispatcherHelper.SafeInvoke(() =>
            {
                Logs.Clear();
            });
        }
    }
}