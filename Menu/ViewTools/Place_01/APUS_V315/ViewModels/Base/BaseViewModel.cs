using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.APUS_V315.ViewModels.Base;

public abstract class BaseViewModel : ObservableObject
{
    private bool _isBusy;
    private string _statusMessage = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        protected set => SetProperty(ref _statusMessage, value);
    }

    protected void SetBusy(string message)
    {
        IsBusy = true;
        StatusMessage = message;
    }

    protected void SetIdle(string message = "Ready")
    {
        IsBusy = false;
        StatusMessage = message;
    }
}