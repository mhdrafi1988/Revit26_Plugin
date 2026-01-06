using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WSA_V05.Helpers;
using Revit26_Plugin.WSA_V05.Models;
using Revit26_Plugin.WSA_V05.Services;
using Revit26_Plugin.WSA_V05.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace Revit26_Plugin.WSA_V05.ViewModels
{
    public class WorksetsViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly WorksetAssignmentService _svc;
        private readonly HashSet<string> _existingWorksets;

        public ObservableCollection<LinkInfo> Links { get; } = new();
        public ObservableCollection<string> Log { get; } = new();

        // ===== User Inputs =====
        private string _plusPrefix = "+";
        public string PlusPrefix
        {
            get => _plusPrefix;
            set { _plusPrefix = value ?? ""; OnChanged(nameof(PlusPrefix)); UpdateTargets(); }
        }

        private string _linkWord = "Link";
        public string LinkWord
        {
            get => _linkWord;
            set { _linkWord = value ?? ""; OnChanged(nameof(LinkWord)); UpdateTargets(); }
        }

        private string _zeroSuffix = "";
        public string ZeroSuffix
        {
            get => _zeroSuffix;
            set { _zeroSuffix = value ?? ""; OnChanged(nameof(ZeroSuffix)); UpdateTargets(); }
        }

        // ===== Commands =====
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand ReverseSelectionCommand { get; }
        public ICommand CreateCommand { get; }
        public ICommand ReSyncCommand { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        public WorksetsViewModel(ExternalCommandData cmd)
        {
            _uidoc = cmd.Application.ActiveUIDocument;
            _svc = new WorksetAssignmentService(Log);

            _existingWorksets = new FilteredWorksetCollector(_uidoc.Document)
                .OfKind(WorksetKind.UserWorkset)
                .Select(w => w.Name)
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            var linkTypes = new FilteredElementCollector(_uidoc.Document)
                .OfClass(typeof(RevitLinkType))
                .Cast<RevitLinkType>()
                .Where(t => t.GetExternalFileReference() != null)
                .ToList();

            foreach (var lt in linkTypes)
            {
                string name = WorksetAssignmentService.GetCleanLinkName(lt);
                Links.Add(new LinkInfo(name));
            }

            UpdateTargets();

            SelectAllCommand = new RelayCommand(_ => Links.ToList().ForEach(l => l.IsSelected = true));
            SelectNoneCommand = new RelayCommand(_ => Links.ToList().ForEach(l => l.IsSelected = false));
            ReverseSelectionCommand = new RelayCommand(_ => Links.ToList().ForEach(l => l.IsSelected = !l.IsSelected));
            CreateCommand = new RelayCommand(_ => Execute(false));
            ReSyncCommand = new RelayCommand(_ => Execute(true));
        }

        private void UpdateTargets()
        {
            foreach (var l in Links)
            {
                string target =
                    $"{PlusPrefix}{LinkWord}({l.LinkName}{ZeroSuffix})";

                l.TargetWorkset = target;
                l.WorksetExists = _existingWorksets.Contains(target);
            }
        }

        private void Execute(bool resyncAll)
        {
            using var tx =
                new Transaction(_uidoc.Document, "Assign Link Worksets");

            tx.Start();

            var linkTypes = new FilteredElementCollector(_uidoc.Document)
                .OfClass(typeof(RevitLinkType))
                .Cast<RevitLinkType>()
                .ToList();

            var targets = resyncAll
                ? Links
                : Links.Where(l => l.IsSelected);

            foreach (var item in targets)
            {
                var lt = linkTypes.First(t =>
                    WorksetAssignmentService.GetCleanLinkName(t) == item.LinkName);

                _svc.Assign(_uidoc.Document, lt, item.TargetWorkset);
            }

            tx.Commit();
            Log.Add(resyncAll ? "🔄 Re-Sync complete." : "✔ Create WS complete.");
        }

        private void OnChanged(string p) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
