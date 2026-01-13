using System;
using System.Windows.Input;

namespace Revit22_Plugin.copv3
{
    public class CalloutCOPV3RelayCommand : ICommand
    {
        private readonly Action<object> _action;

        public CalloutCOPV3RelayCommand(Action<object> action)
        {
            _action = action;
        }

        public bool CanExecute(object parameter) => true;

        public event EventHandler CanExecuteChanged;

        public void Execute(object parameter) => _action(parameter);
    }
}
