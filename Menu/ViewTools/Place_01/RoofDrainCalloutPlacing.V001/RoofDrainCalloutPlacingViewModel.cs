using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.ExternalEvents;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V001.ViewModels
{
    public partial class RoofDrainCalloutPlacingViewModel : ObservableObject
    {
        private readonly ExternalEvent _runEvent;
        private readonly Document _doc;

        public ObservableCollection<View> PlanViews { get; } = new ObservableCollection<View>();
        public ObservableCollection<View> DraftingViews { get; } = new ObservableCollection<View>();
        public ObservableCollection<LogEntry> Logs { get; } = new ObservableCollection<LogEntry>();

        [ObservableProperty] private View selectedPlanView;
        [ObservableProperty] private View selectedDraftingView;
        [ObservableProperty] private string groupingToleranceMmText = "500";
        [ObservableProperty] private string calloutSizeMmText = "500";
        [ObservableProperty] private int roofsFoundCount;
        [ObservableProperty] private bool isBusy;

        [ObservableProperty] private int metricRoofs;
        [ObservableProperty] private int metricZeroPoints;
        [ObservableProperty] private int metricGroups;
        [ObservableProperty] private int metricCallouts;

        public double GroupingToleranceMm =>
            double.TryParse(GroupingToleranceMmText, out var v) ? v : 500;

        public double CalloutSizeMm =>
            double.TryParse(CalloutSizeMmText, out var v) ? v : 500;

        public RoofDrainCalloutPlacingViewModel(UIApplication uiApp)
        {
            _doc = uiApp.ActiveUIDocument.Document;

            // ExternalEvent.Create() must happen here, in the ViewModel constructor,
            // which itself is constructed during IExternalCommand.Execute() — i.e.
            // inside the valid API context. Never create lazily from UI interaction.
            var handler = new RoofDrainCalloutRunHandler(this);
            _runEvent = ExternalEvent.Create(handler);

            LoadPlanViews(uiApp);
            LoadDraftingViews();
        }

        private void LoadPlanViews(UIApplication uiApp)
        {
            var collector = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewPlan))
                .Cast<ViewPlan>()
                .Where(v => !v.IsTemplate);

            foreach (var v in collector)
                PlanViews.Add(v);

            var active = uiApp.ActiveUIDocument.ActiveView;
            SelectedPlanView = (active is ViewPlan) ? active : PlanViews.FirstOrDefault();

            if (SelectedPlanView != null)
                RoofsFoundCount = new FilteredElementCollector(_doc, SelectedPlanView.Id)
                    .OfClass(typeof(RoofBase))
                    .WhereElementIsNotElementType()
                    .GetElementCount();
        }

        private void LoadDraftingViews()
        {
            var collector = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .Where(v => !v.IsTemplate);

            foreach (var v in collector)
                DraftingViews.Add(v);

            SelectedDraftingView = DraftingViews.FirstOrDefault();
        }

        partial void OnSelectedPlanViewChanged(View value)
        {
            if (value == null) { RoofsFoundCount = 0; return; }

            RoofsFoundCount = new FilteredElementCollector(_doc, value.Id)
                .OfClass(typeof(RoofBase))
                .WhereElementIsNotElementType()
                .GetElementCount();
        }

        [RelayCommand]
        private void Run()
        {
            if (SelectedPlanView == null || SelectedDraftingView == null)
            {
                Logs.Add(new LogEntry(LogLevel.Warning, "Select a plan view and a drafting view before running."));
                return;
            }

            IsBusy = true;
            _runEvent.Raise();
        }

        /// <summary>Called by the ExternalEvent handler on the Revit API thread's completion — updates are marshalled fine since collections are ObservableCollection bound via WPF's dispatcher.</summary>
        public void ReportResults(int roofs, int zeroPoints, int groups, int callouts)
        {
            MetricRoofs = roofs;
            MetricZeroPoints = zeroPoints;
            MetricGroups = groups;
            MetricCallouts = callouts;
            IsBusy = false;
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            var sb = new StringBuilder();
            foreach (var entry in Logs)
                sb.AppendLine(entry.ToString());
            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());
        }

        [RelayCommand]
        private void ClearLogs()
        {
            Logs.Clear();
        }

        /// <summary>Called from the Window's code-behind with the log ListBox's SelectedItems
        /// (WPF doesn't bind ListBox.SelectedItems directly, so this is invoked on click
        /// rather than exposed as a RelayCommand).</summary>
        public void CopySelectedLogs(System.Collections.IList selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;

            var sb = new StringBuilder();
            foreach (var item in selectedItems)
                if (item is LogEntry entry)
                    sb.AppendLine(entry.ToString());

            if (sb.Length > 0)
                Clipboard.SetText(sb.ToString());
        }
    }
}
