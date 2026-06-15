using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.APUS_V315.Services.Abstractions;
using System;

namespace Revit26_Plugin.APUS_V315.ViewModels.Items;

public sealed partial class SectionItemViewModel : ObservableObject, IDisposable
{
    private readonly ViewSection _view;
    private readonly bool _isPlaced;
    private readonly string _sheetNumber;
    private readonly string _placementScope;
    private readonly IViewSizeCalculator _sizeCalculator;

    [ObservableProperty]
    private bool _isSelected = true;

    public ViewSection View => _view;
    public string ViewName => _view?.Name ?? "Unknown";
    public int Scale => _view?.Scale ?? 100;
    public string ViewId => _view?.Id?.ToString() ?? "-1";
    public ElementId ElementId => _view?.Id;
    public bool IsPlaced => _isPlaced;
    public string SheetNumber => _sheetNumber ?? string.Empty;
    public string PlacementScope => _placementScope ?? string.Empty;
    public bool IsValid => _view != null && _view.IsValidObject;

    public string ScaleDisplay => $"1:{Scale}";
    public string PlacementStatus => IsPlaced ? $"Placed on Sheet {SheetNumber}" : "Not Placed";
    public string SectionSummary => $"{ViewName} | Scale: 1:{Scale} | {PlacementStatus}";

    public SectionItemViewModel(
        ViewSection view,
        bool isPlaced,
        string sheetNumber,
        string placementScope,
        IViewSizeCalculator sizeCalculator)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
        _isPlaced = isPlaced;
        _sheetNumber = sheetNumber ?? string.Empty;
        _placementScope = placementScope ?? string.Empty;
        _sizeCalculator = sizeCalculator ?? throw new ArgumentNullException(nameof(sizeCalculator));
    }

    public string GetDetailNumber()
    {
        if (_view != null && _view.IsValidObject && _view.GetPrimaryViewId() != ElementId.InvalidElementId)
        {
            var param = _view.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER);
            return param?.AsString() ?? string.Empty;
        }
        return string.Empty;
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(ViewName));
        OnPropertyChanged(nameof(Scale));
        OnPropertyChanged(nameof(ScaleDisplay));
        OnPropertyChanged(nameof(PlacementStatus));
        OnPropertyChanged(nameof(SectionSummary));
    }

    public override bool Equals(object? obj)
    {
        return obj is SectionItemViewModel other && _view?.Id == other._view?.Id;
    }

    public override int GetHashCode()
    {
        return _view?.Id?.GetHashCode() ?? 0;
    }

    public override string ToString()
    {
        return $"{ViewName} (ID: {ViewId})";
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}