using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.CalloutCOP.V011.Services;
using Revit26_Plugin.CalloutCOP.V011.ViewModels;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.CalloutCOP.V011.ExternalEvents
{
    public class CalloutPlacementExternalEvent : IExternalEventHandler
    {
        private readonly Document _doc;
        private readonly ObservableCollection<ViewItemViewModel> _views;
        private readonly ObservableCollection<LogEntry> _logs;
        private readonly Func<ViewDrafting> _draftingViewProvider;
        private readonly Func<double> _sizeProvider; // mm
        private readonly Action<int, int> _onFinished;

        public CalloutPlacementExternalEvent(
            Document doc,
            ObservableCollection<ViewItemViewModel> views,
            ObservableCollection<LogEntry> logs,
            Func<ViewDrafting> draftingViewProvider,
            Func<double> sizeProvider,
            Action<int, int> onFinished)
        {
            _doc = doc;
            _views = views;
            _logs = logs;
            _draftingViewProvider = draftingViewProvider;
            _sizeProvider = sizeProvider;
            _onFinished = onFinished;
        }

        public void Execute(UIApplication app)
        {
            int success = 0;
            int failed = 0;

            var draftingView = _draftingViewProvider();
            if (draftingView == null)
            {
                _logs.Add(new LogEntry(LogLevel.Warning, "No Drafting View selected."));
                _onFinished?.Invoke(0, 0);
                return;
            }

            var targets = _views.Where(v => v.IsSelected).ToList();
            if (!targets.Any())
            {
                _logs.Add(new LogEntry(LogLevel.Warning, "No target views selected."));
                _onFinished?.Invoke(0, 0);
                return;
            }

            using var tx = new Transaction(_doc, "Callout COP V011 - Reference Callouts");
            tx.Start();

            foreach (var vm in targets)
            {
                using var subTx = new SubTransaction(_doc);

                try
                {
                    subTx.Start();

                    ReferenceCalloutService.CreateReferenceCallout(
                        _doc,
                        vm.View,
                        draftingView,
                        _sizeProvider()); // mm

                    subTx.Commit();

                    _logs.Add(new LogEntry(LogLevel.Success, $"Reference callout placed in {vm.Name}"));
                    success++;
                }
                catch (Exception ex)
                {
                    if (subTx.HasStarted())
                        subTx.RollBack();

                    _logs.Add(new LogEntry(LogLevel.Error, $"{vm.Name}: {ex.Message}"));
                    failed++;
                }
            }

            tx.Commit();

            _logs.Add(new LogEntry(LogLevel.Info, $"Finished. Success: {success}, Failed: {failed}"));
            _onFinished?.Invoke(success, failed);
        }

        public string GetName() => "Callout COP V011 - Placement External Event";
    }
}
