// File: CalloutPlacementExternalEvent.cs

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.CalloutCOP_V06.Models;
using Revit26_Plugin.CalloutCOP_V06.Services;
using Revit26_Plugin.CalloutCOP_V06.ViewModels;

namespace Revit26_Plugin.CalloutCOP_V06.ExternalEvents
{
    public class CalloutPlacementExternalEvent : IExternalEventHandler
    {
        private readonly Document _doc;
        private readonly ObservableCollection<ViewItemViewModel> _views;
        private readonly ObservableCollection<CopLogEntry> _logs;
        private readonly Func<ViewDrafting> _draftingViewProvider;
        private readonly Func<double> _sizeProvider; // mm
        private readonly Action<int, int> _onFinished;

        public CalloutPlacementExternalEvent(
            Document doc,
            ObservableCollection<ViewItemViewModel> views,
            ObservableCollection<CopLogEntry> logs,
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
                _logs.Add(new CopLogEntry(
                    CopLogLevel.Warning,
                    "No Drafting View selected."));

                _onFinished?.Invoke(0, 0);
                return;
            }

            var targets = _views.Where(v => v.IsSelected).ToList();
            if (!targets.Any())
            {
                _logs.Add(new CopLogEntry(
                    CopLogLevel.Warning,
                    "No target views selected."));

                _onFinished?.Invoke(0, 0);
                return;
            }

            using var tx = new Transaction(
                _doc,
                "Callout COP V06 – Reference Callouts");

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

                    _logs.Add(new CopLogEntry(
                        CopLogLevel.Info,
                        $"Reference callout placed in {vm.Name}"));

                    success++;
                }
                catch (Exception ex)
                {
                    if (subTx.HasStarted())
                        subTx.RollBack();

                    _logs.Add(new CopLogEntry(
                        CopLogLevel.Error,
                        $"{vm.Name}: {ex.Message}"));

                    failed++;
                }
            }

            tx.Commit();

            _logs.Add(new CopLogEntry(
                CopLogLevel.Info,
                $"Finished. Success: {success}, Failed: {failed}"));

            _onFinished?.Invoke(success, failed);
        }

        public string GetName()
            => "CalloutCOP V06 – Placement External Event";
    }
}
