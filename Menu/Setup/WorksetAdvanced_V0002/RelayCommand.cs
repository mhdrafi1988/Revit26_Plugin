using System;
using System.Windows.Input;

namespace Revit26_Plugin.WSAV02.Helpers
{
    public class RelayCommandWSAV02 : ICommand
    {
        private readonly Action<object> _exec;
        private readonly Func<object, bool> _can;

        public RelayCommandWSAV02(Action<object> exec, Func<object, bool> can = null)
        {
            _exec = exec;
            _can = can;
        }

        public bool CanExecute(object parameter) => _can?.Invoke(parameter) ?? true;

        public void Execute(object parameter) => _exec(parameter);

        public event EventHandler CanExecuteChanged;
    }
}
