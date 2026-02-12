using Autodesk.Revit.DB.Visual;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.APUS_V315.ViewModels.Items;

public sealed partial class PlacementProgressViewModel : ObservableObject
{
    [ObservableProperty]
    private int _total;

    [ObservableProperty]
    private int _current;

    [ObservableProperty]
    private bool _isCancelled;

    [ObservableProperty]
    private string _currentOperation = string.Empty;

    public double Percentage => Total > 0 ? (Current * 100.0) / Total : 0;
    public bool ShouldContinue => !IsCancelled && Current < Total;

    public void Reset(int totalCount)
    {
        Total = totalCount;
        Current = 0;
        IsCancelled = false;
        CurrentOperation = "Starting placement...";
    }

    public void Step(string operation = "")
    {
        if (Current < Total)
        {
            Current++;
            if (!string.IsNullOrEmpty(operation))
                CurrentOperation = operation;
        }
    }

    public void Update(int value, string operation = "")
    {
        if (value >= 0 && value <= Total)
        {
            Current = value;
            if (!string.IsNullOrEmpty(operation))
                CurrentOperation = operation;
        }
    }

    public void Cancel()
    {
        IsCancelled = true;
        CurrentOperation = "Cancelling...";
    }
}