using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;

namespace Revit26_Plugin.RoofCreateTest.V001
{
    public partial class RoofCreateTestViewModel : ObservableObject
    {
        private readonly ExternalEvent _legacyEvent;
        private readonly ExternalEvent _staticEvent;

        public ObservableCollection<Level> Levels { get; }
        public ObservableCollection<RoofType> RoofTypes { get; }
        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateRoofLegacyCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateRoofStaticCommand))]
        private Level selectedLevel;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateRoofLegacyCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateRoofStaticCommand))]
        private RoofType selectedRoofType;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CreateRoofLegacyCommand))]
        [NotifyCanExecuteChangedFor(nameof(CreateRoofStaticCommand))]
        private bool isBusy;

        public RoofCreateTestViewModel(UIApplication uiApp, IList<Level> levels, IList<RoofType> roofTypes)
        {
            Levels = new ObservableCollection<Level>(levels);
            RoofTypes = new ObservableCollection<RoofType>(roofTypes);

            SelectedLevel = Levels.FirstOrDefault();
            SelectedRoofType = RoofTypes.FirstOrDefault();

            var legacyHandler = new RoofCreateHandler(this);
            _legacyEvent = ExternalEvent.Create(legacyHandler);

            var staticHandler = new RoofCreateStaticHandler(this);
            _staticEvent = ExternalEvent.Create(staticHandler);

            Log(LogLevel.Info, $"Window opened — {Levels.Count} levels, {RoofTypes.Count} roof types loaded");
        }

        private bool CanCreateRoof() => !IsBusy && SelectedLevel != null && SelectedRoofType != null;

        [RelayCommand(CanExecute = nameof(CanCreateRoof))]
        private void CreateRoofLegacy()
        {
            IsBusy = true;
            Log(LogLevel.Info, $"Legacy API requested: '{SelectedLevel.Name}', '{SelectedRoofType.Name}'");
            _legacyEvent.Raise();
        }

        [RelayCommand(CanExecute = nameof(CanCreateRoof))]
        private void CreateRoofStatic()
        {
            IsBusy = true;
            Log(LogLevel.Info, $"Static API requested: '{SelectedLevel.Name}', '{SelectedRoofType.Name}'");
            _staticEvent.Raise();
        }

        public void OnOperationCompleted() => RunOnUi(() => IsBusy = false);

        public void Log(LogLevel level, string message) => RunOnUi(() => LogEntries.Add(new LogEntry(level, message)));

        [RelayCommand] private void CopyAll() { /* keep as before */ }
        [RelayCommand] private void CopySelected(IList<object> selected) { /* keep as before */ }
        [RelayCommand] private void ClearLog() => LogEntries.Clear();

        private static void RunOnUi(System.Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
                dispatcher.Invoke(action);
            else
                action();
        }
    }
}