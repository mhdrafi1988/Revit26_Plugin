using Revit26_Plugin.APUS_V313.Enums;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.APUS_V313.ViewModels
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

        private ProgressState _state = ProgressState.NotStarted;
        public ProgressState State
        {
            get => _state;
            set
            {
                if (_state == value) return;
                _state = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsCancelled)); // Keep for compatibility
            }
        }

        // Keep for backward compatibility
        public bool IsCancelled => State == ProgressState.Cancelled;

        /// <summary>
        /// Initialize progress state.
        /// </summary>
        public void Reset(int totalCount)
        {
            Total = totalCount;
            Current = 0;
            State = ProgressState.Running;
        }

        /// <summary>
        /// Advance progress by one step.
        /// </summary>
        public void Step()
        {
            if (Current < Total)
            {
                Current++;

                if (Current >= Total && State == ProgressState.Running)
                {
                    State = ProgressState.Completed;
                }
            }
        }

        /// <summary>
        /// Request cancellation.
        /// </summary>
        public void Cancel()
        {
            State = ProgressState.Cancelled;
        }

        /// <summary>
        /// Mark as failed.
        /// </summary>
        public void Fail()
        {
            State = ProgressState.Failed;
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