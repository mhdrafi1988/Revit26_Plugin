using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AutoLiner_V02.ExternalEvents;
using Revit26_Plugin.AutoLiner_V02.Models;
using Revit26_Plugin.AutoLiner_V02.Services;
using System.Collections.ObjectModel;
using System.Text;

namespace Revit26_Plugin.AutoLiner_V02.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly Element _roof;
        private readonly AutoLinerExternalEventHandler _handler;
        private readonly ExternalEvent _externalEvent;

        public ObservableCollection<DetailItemOption> DetailItems { get; }

        [ObservableProperty]
        private DetailItemOption selectedDetailItem;

        [ObservableProperty]
        private string logText;

        private readonly StringBuilder _log = new();

        public MainViewModel(
            Document doc,
            Element roof,
            AutoLinerExternalEventHandler handler,
            ExternalEvent externalEvent)
        {
            _doc = doc;
            _roof = roof;
            _handler = handler;
            _externalEvent = externalEvent;

            DetailItems =
                new ObservableCollection<DetailItemOption>(
                    DetailItemCollectorService.Collect(doc));

            Log("Ready.");
        }

        [RelayCommand]
        private void Execute()
        {
            if (SelectedDetailItem == null)
            {
                Log("❌ No detail family selected");
                return;
            }

            Log("▶ Execute requested");

            _handler.Document = _doc;
            _handler.ActiveView = _doc.ActiveView;
            _handler.Roof = _roof;
            _handler.DetailSymbol = SelectedDetailItem.Symbol;
            _handler.Log = Log;

            // ✅ SAFE Revit API call
            _externalEvent.Raise();
        }

        private void Log(string message)
        {
            _log.AppendLine(message);
            LogText = _log.ToString();
        }
    }
}
