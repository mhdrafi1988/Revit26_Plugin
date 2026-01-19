using System;
using System.Windows.Input;

namespace Revit26_Plugin.CalloutCOP_V06
{
    public class LogEntry : ICommand
    {
        private readonly Action<object> _execute;

        public LogEntry(Action<object> execute)
        {
            _execute = execute;
        }

        public bool CanExecute(object parameter) => true;

        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged;
    }
}
