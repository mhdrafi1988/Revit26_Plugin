using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WSA_V05.Helpers;
using Revit26_Plugin.WSA_V05.Models;
using Revit26_Plugin.WSA_V05.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows.Input;

namespace Revit26_Plugin.WSA_V05.ViewModels
{
    public class WorksetsViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly WorksetAssignmentService _svc;
        private HashSet<string> _existingWorksets;

        public ObservableCollection<LinkInfo> Links { get; } = new();
        public ObservableCollection<string> Log { get; } = new();

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

        public ICommand SelectAllCommand { get; }
        public ICommand CreateCommand { get; }
        public ICommand ReSyncCommand { get; }

        public WorksetsViewModel(ExternalCommandData cmd)
        {
            _uidoc = cmd.Application.ActiveUIDocument;
            _svc = new WorksetAssignmentService(Log);
            LoadData();

            SelectAllCommand = new RelayCommand(_ => { foreach (var l in Links) l.IsSelected = true; });
            CreateCommand = new RelayCommand(_ => ExecuteBatch(false));
            ReSyncCommand = new RelayCommand(_ => ExecuteBatch(true));
        }

        private void LoadData()
        {
            _existingWorksets = new FilteredWorksetCollector(_uidoc.Document)
                .OfKind(WorksetKind.UserWorkset)
                .Select(w => w.Name)
                .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

            var linkNames = new FilteredElementCollector(_uidoc.Document)
                .OfClass(typeof(RevitLinkType))
                .Cast<RevitLinkType>()
                .Select(t => Path.GetFileNameWithoutExtension(t.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            Links.Clear();
            foreach (var name in linkNames)
            {
                Links.Add(new LinkInfo(name));
            }

            UpdateTargets();
        }

        private void UpdateTargets()
        {
            foreach (var l in Links)
            {
                l.TargetWorkset = $"{PlusPrefix}{LinkWord}({l.LinkName}{ZeroSuffix})";
                l.WorksetExists = _existingWorksets.Contains(l.TargetWorkset);
            }
        }

        // WorksetsViewModel_05.cs - ExecuteBatch method update (replace ONLY this method)
        private void ExecuteBatch(bool processAll)
        {
            var allLinkTypes = new FilteredElementCollector(_uidoc.Document)
                .OfClass(typeof(RevitLinkType))
                .Cast<RevitLinkType>()
                .Where(t => t.GetExternalFileReference() != null)
                .ToList();

            var items = processAll ? Links : Links.Where(l => l.IsSelected);

            foreach (var item in items)
            {
                var cleanLinkName = item.LinkName; // Already Path.GetFileNameWithoutExtension from LoadData
                var type = allLinkTypes.FirstOrDefault(t => Path.GetFileNameWithoutExtension(t.Name) == cleanLinkName);

                if (type != null)
                {
                    Log.Add($"[MATCH] Found type '{type.Name}' for '{cleanLinkName}'");
                    _svc.Assign(_uidoc.Document, type, item.TargetWorkset);
                }
                else
                {
                    Log.Add($"[WARN] No RevitLinkType found for '{cleanLinkName}'");
                }
            }

            LoadData(); // Refresh UI
            Log.Add("Done.");
        }


        public event PropertyChangedEventHandler PropertyChanged;

        private void OnChanged(string p) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
