using Autodesk.Revit.UI;
using Revit26_Plugin.WSAV03.Helpers;
using Revit26_Plugin.WSAV03.Models;
using Revit26_Plugin.WSAV03.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace Revit26_Plugin.WSAV03.ViewModels
{
    public class WorksetsViewModel : INotifyPropertyChanged
    {
        private readonly UIDocument _uidoc;
        private readonly WorksetServiceWSAV03 _svc;

        public ObservableCollection<WorksetItem> Items { get; }
            = new ObservableCollection<WorksetItem>();

        public ObservableCollection<string> LogMessages { get; }
            = new ObservableCollection<string>();

        private string _pattern = "+Link({name})";
        public string Pattern
        {
            get => _pattern;
            set
            {
                _pattern = string.IsNullOrWhiteSpace(value) ? "+Link({name})" : value;
                OnChanged(nameof(Pattern));
                UpdatePreviewNames();
            }
        }

        public ICommand CreateCommand { get; }
        public ICommand ReSyncCommand { get; }
        public ICommand CloseCommand { get; }

        public event Action<bool> RequestClose;
        public event PropertyChangedEventHandler PropertyChanged;

        public WorksetsViewModel(ExternalCommandData cmd)
        {
            _uidoc = cmd.Application.ActiveUIDocument;
            _svc = new WorksetServiceWSAV03(LogMessages);

            // Load Linked Files
            var links = _svc.GetLinkedFileNames(_uidoc.Document);

            foreach (var ln in links)
            {
                // Check if this link is already assigned to a workset
                bool isAssigned = _svc.IsLinkAssignedToWorkset(_uidoc.Document, ln);

                // ONLY add to grid if NOT assigned to a workset
                if (!isAssigned)
                {
                    var item = new WorksetItem(ln);
                    item.IsAssigned = false; // Explicitly set to false
                    Items.Add(item);
                    LogMessages.Add($"📋 Added unassigned link: {ln}");
                }
                else
                {
                    LogMessages.Add($"⏭️ Skipping already assigned link: {ln}");
                }
            }

            UpdatePreviewNames();

            CreateCommand = new RelayCommandWSAV03(_ => CreateWorksets());
            ReSyncCommand = new RelayCommandWSAV03(_ => ReSync());
            CloseCommand = new RelayCommandWSAV03(_ => RequestClose?.Invoke(true));
        }

        private void UpdatePreviewNames()
        {
            foreach (var item in Items)
                item.PreviewName = Pattern.Replace("{name}", item.LinkName);
        }

        private void CreateWorksets()
        {
            var selected = Items.Where(i => i.IsSelected).ToList();
            if (!selected.Any())
            {
                LogMessages.Add("⚠ No items selected.");
                return;
            }

            foreach (var item in selected)
            {
                _svc.CreateAndAssign(_uidoc.Document, item.PreviewName, item.LinkName);
                // After successful assignment, we could remove from grid if desired
                // But keeping them allows resync if needed
            }

            LogMessages.Add("✔ Operation complete.");
        }

        private void ReSync()
        {
            foreach (var item in Items)
            {
                _svc.ReSyncAssignment(_uidoc.Document, item.PreviewName, item.LinkName);
            }

            LogMessages.Add("🔄 Re-Sync complete.");
        }

        private void OnChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}