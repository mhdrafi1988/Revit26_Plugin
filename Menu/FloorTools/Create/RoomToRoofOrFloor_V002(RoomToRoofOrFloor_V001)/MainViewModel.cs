using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoomToRoofOrFloor.V002.Core.Engine;
using Revit26_Plugin.RoomToRoofOrFloor.V002.Core.Models;
using Revit26_Plugin.RoomToRoofOrFloor.V002.Infrastructure.ExternalEvents;
using Revit26_Plugin.RoomToRoofOrFloor.V002.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoomToRoofOrFloor.V002.UI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly ExternalEvent _runEvent;

        public ObservableCollection<RoomListItem> Rooms { get; } = new();
        public ObservableCollection<RoofTypeOption> RoofTypes { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();

        [ObservableProperty]
        private RoofTypeOption _selectedRoofType;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private bool _isRunning;

        [ObservableProperty] private int _totalRooms;
        [ObservableProperty] private int _roofsCreated;
        [ObservableProperty] private int _floorsCreatedFallback;
        [ObservableProperty] private int _skipped;
        [ObservableProperty] private string _statusText = "Ready";

        public MainViewModel(UIDocument uiDoc)
        {
            _doc = uiDoc.Document;

            // ExternalEvent.Create must run in a valid API context — the
            // constructor, called from the command, qualifies. Never create
            // this lazily on first Run click.
            var handler = new RunExternalEventHandler(this);
            _runEvent = ExternalEvent.Create(handler);

            LoadRooms();
            LoadRoofTypes();
        }

        private void LoadRooms()
        {
            var rooms = new FilteredElementCollector(_doc)
                .OfClass(typeof(SpatialElement))
                .OfCategory(BuiltInCategory.OST_Rooms)
                .Cast<Room>()
                .Where(r => r.Area > 0);

            foreach (var room in rooms)
            {
                var levelName = _doc.GetElement(room.LevelId) is Level lvl ? lvl.Name : "";
                Rooms.Add(new RoomListItem(room.Id, room.Name, room.Number, levelName));
            }

            TotalRooms = Rooms.Count(r => r.IsSelected);
        }

        private void LoadRoofTypes()
        {
            foreach (var t in RevitTypeHelper.GetRoofTypes(_doc))
                RoofTypes.Add(t);

            SelectedRoofType = RoofTypes.FirstOrDefault();
        }

        [RelayCommand]
        private void SelectAll()
        {
            foreach (var r in Rooms) r.IsSelected = true;
            TotalRooms = Rooms.Count(r => r.IsSelected);
        }

        [RelayCommand]
        private void SelectNone()
        {
            foreach (var r in Rooms) r.IsSelected = false;
            TotalRooms = Rooms.Count(r => r.IsSelected);
        }

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            if (SelectedRoofType == null)
            {
                Logs.Add(new LogEntry(LogLevel.Error, "No roof type selected — pick one before running"));
                return;
            }

            IsRunning = true;
            StatusText = "Processing…";
            RoofsCreated = 0;
            FloorsCreatedFallback = 0;
            Skipped = 0;

            _runEvent.Raise();
        }

        private bool CanRun() => !IsRunning;

        /// <summary>Executed inside RunExternalEventHandler, on the Revit API thread.</summary>
        public void RunOnRevitThread()
        {
            var engine = new RoomToRoofOrFloorEngine(_doc);
            var selectedRooms = Rooms.Where(r => r.IsSelected).ToList();
            int processed = 0;

            foreach (var item in selectedRooms)
            {
                if (_doc.GetElement(item.RoomId) is not Room room) continue;

                var result = engine.ProcessRoom(room, SelectedRoofType.TypeId, entry => Logs.Add(entry));

                switch (result.Outcome)
                {
                    case RoomOutcome.RoofCreated: RoofsCreated++; break;
                    case RoomOutcome.FloorCreatedFallback: FloorsCreatedFallback++; break;
                    default: Skipped++; break;
                }

                processed++;
                StatusText = $"Processing… {processed} of {selectedRooms.Count}";
            }

            IsRunning = false;
            StatusText = $"Done — {RoofsCreated} roofs, {FloorsCreatedFallback} floors (fallback), {Skipped} skipped";
        }
    }
}
