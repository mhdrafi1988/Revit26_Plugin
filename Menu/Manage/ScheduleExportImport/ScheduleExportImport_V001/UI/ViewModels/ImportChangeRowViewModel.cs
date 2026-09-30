using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.ScheduleExportImport.V001.Core.Models;

namespace Revit26_Plugin.ScheduleExportImport.V001.UI.ViewModels
{
    public partial class ImportChangeRowViewModel : ObservableObject
    {
        public ImportChange Model { get; }

        public ImportChangeRowViewModel(ImportChange model)
        {
            Model = model;
            isSelected = model.Status == ImportChangeStatus.Change;
        }

        [ObservableProperty]
        private bool isSelected;

        public long ElementId => Model.ElementId;
        public string ParameterName => Model.ParameterName;
        public string OldValue => Model.OldValue;
        public string NewValue => Model.NewValue;
        public ImportChangeStatus Status => Model.Status;
        public string Message => Model.Message;
        public bool IsSelectable => Model.Status == ImportChangeStatus.Change;

        public string StatusLabel => Model.Status switch
        {
            ImportChangeStatus.Change => "Change",
            ImportChangeStatus.NotFound => "Not found",
            ImportChangeStatus.Duplicate => "Duplicate ID",
            ImportChangeStatus.ReadOnly => "Read-only",
            ImportChangeStatus.TypeParameter => "Type param",
            ImportChangeStatus.MissingFromFile => "Missing from file",
            ImportChangeStatus.Applied => "Applied",
            ImportChangeStatus.Failed => "Failed",
            _ => Model.Status.ToString()
        };

        /// <summary>Re-reads the model after the handler has updated Status/Message.</summary>
        public void Refresh()
        {
            if (!IsSelectable) IsSelected = false;
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(Message));
            OnPropertyChanged(nameof(IsSelectable));
        }
    }
}
