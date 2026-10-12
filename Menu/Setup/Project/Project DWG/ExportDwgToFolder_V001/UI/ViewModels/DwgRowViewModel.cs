using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.ExportDwgToFolder.V001.Core.Models;

namespace Revit26_Plugin.ExportDwgToFolder.V001.UI.ViewModels
{
    /// <summary>Result of the last export attempt for one DWG row.</summary>
    public enum DwgRowStatus { Pending, Exported, Skipped, Failed }

    /// <summary>
    /// One row in the DWG list: the scanned DWG, its tick box and the outcome of the last export.
    /// </summary>
    public partial class DwgRowViewModel : ObservableObject
    {
        public DwgRowViewModel(DwgInstanceInfo info)
        {
            Info = info;
        }

        /// <summary>The scanned DWG this row represents.</summary>
        public DwgInstanceInfo Info { get; }

        public string FileName => Info.OutputFileName;
        public string Source => Info.SourceType == DwgSourceType.Link ? "Link" : "Import";
        public string ViewType => Info.ViewCategory == DwgViewCategory.NoViews ? "No views" : Info.ViewCategory.ToString();
        public string ViewName => Info.ViewName ?? "–";
        public string Sheet => Info.SheetInfo ?? "–";

        /// <summary>True when the row will be exported on the next Run.</summary>
        [ObservableProperty] private bool isTicked = true;

        /// <summary>Outcome of the last export (drives the row colour).</summary>
        [ObservableProperty] private DwgRowStatus status = DwgRowStatus.Pending;

        /// <summary>Short result text shown in the Result column.</summary>
        [ObservableProperty] private string message = string.Empty;
    }
}
