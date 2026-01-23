using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.WSFL_008.ViewModels;
using Revit26_Plugin.WSFL_008.Models;
using Revit26_Plugin.WSFL_008.Models;
using Revit26_Plugin.WSFL_008.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.WSFL_008.ViewModels
{
    public partial class WorksetsViewModel : ObservableObject
    {
        private readonly UIDocument _uidoc;
        private readonly WorksetService _service;

        public ObservableCollection<WorksetItem> Items { get; } = new();
        public ObservableCollection<LogEntry> Log { get; } = new();

        [ObservableProperty]
        private string pattern = "+Link({name})";

        public IRelayCommand CreateCommand { get; }
        public IRelayCommand CloseCommand { get; }

        public event Action RequestClose;

        public WorksetsViewModel(ExternalCommandData commandData)
        {
            _uidoc = commandData.Application.ActiveUIDocument;

            _service = new WorksetService(AddLog);

            foreach (string linkName in _service.GetLinkedFileNames(_uidoc.Document))
            {
                Items.Add(new WorksetItem(linkName));
            }

            UpdatePreviewNames();

            CreateCommand = new RelayCommand(CreateWorksets);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
        }

        partial void OnPatternChanged(string value)
        {
            UpdatePreviewNames();
        }

        private void UpdatePreviewNames()
        {
            foreach (WorksetItem item in Items)
            {
                item.PreviewName = Pattern.Replace("{name}", item.LinkName);
            }
        }

        private void CreateWorksets()
        {
            var selected = Items
                .Where(i => i.IsSelected)
                .Select(i => (i.PreviewName, i.LinkName))
                .ToList();

            if (!selected.Any())
            {
                AddLog(new LogEntry(LogLevel.Warning, "No links selected."));
                return;
            }

            _service.CreateAndAssign(_uidoc.Document, selected);
        }

        private void AddLog(LogEntry entry)
        {
            Log.Add(entry);
        }
    }
}
