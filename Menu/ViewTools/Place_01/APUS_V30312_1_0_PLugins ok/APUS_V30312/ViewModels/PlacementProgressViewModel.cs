using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.APUS_V312.ViewModels
{
    /// <summary>
    /// Tracks placement progress and supports cancellation.
    /// Implemented WITHOUT source generators for maximum reliability.
    /// </summary>
    public class PlacementProgressViewModel : INotifyPropertyChanged
    {
        private int _total;
        public int Total
        {
            get => _total;
            private set
            {
                if (_total == value) return;
                _total = value;
                OnPropertyChanged();
            }
        }

        private int _current;
        public int Current
        {
            get => _current;
            private set
            {
                if (_current == value) return;
                _current = value;
                OnPropertyChanged();
            }
        }

        private bool _isCancelled;
        public bool IsCancelled
        {
            get => _isCancelled;
            private set
            {
                if (_isCancelled == value) return;
                _isCancelled = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Initialize progress state.
        /// </summary>
        public void Reset(int totalCount)
        {
            Total = totalCount;
            Current = 0;
            IsCancelled = false;
        }

        /// <summary>
        /// Advance progress by one step.
        /// </summary>
        public void Step()
        {
            if (Current < Total)
                Current++;
        }

        /// <summary>
        /// Request cancellation.
        /// </summary>
        public void Cancel()
        {
            IsCancelled = true;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(
            [CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
