// File: MainWizardViewModel.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.ViewModels
//
// Responsibility:
// - Hosts wizard steps
// - Controls navigation and execution
// - Exposes live log feed
// - Handles cancellation
//
// IMPORTANT:
// - ZERO Revit API references
// - Pure MVVM

using System;
using System.Collections.ObjectModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Revit26_Plugin.RoofRidgeLines_V06.Logging;
using Revit26_Plugin.RoofRidgeLines_V06.Models;

namespace Revit26_Plugin.RoofRidgeLines_V06.ViewModels
{
    public class MainWizardViewModel : ObservableObject
    {
        private readonly CancellationTokenSource _cts = new();

        private WizardState _currentState = WizardState.Idle;
        private int _currentStepIndex;

        public ObservableCollection<object> Steps { get; } = new();

        public ObservableCollection<UILogEntry> LogEntries { get; } = new();

        public WizardState CurrentState
        {
            get => _currentState;
            private set => SetProperty(ref _currentState, value);
        }

        public int CurrentStepIndex
        {
            get => _currentStepIndex;
            set => SetProperty(ref _currentStepIndex, value);
        }

        public CancellationToken CancellationToken => _cts.Token;

        public IRelayCommand NextCommand { get; }
        public IRelayCommand BackCommand { get; }
        public IRelayCommand CancelCommand { get; }

        public MainWizardViewModel()
        {
            NextCommand = new RelayCommand(MoveNext, CanMoveNext);
            BackCommand = new RelayCommand(MoveBack, CanMoveBack);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void MoveNext()
        {
            CurrentStepIndex++;
            UpdateStateForStep();
        }

        private bool CanMoveNext()
        {
            return CurrentStepIndex < Steps.Count - 1;
        }

        private void MoveBack()
        {
            CurrentStepIndex--;
            UpdateStateForStep();
        }

        private bool CanMoveBack()
        {
            return CurrentStepIndex > 0;
        }

        private void Cancel()
        {
            _cts.Cancel();
            CurrentState = WizardState.Cancelled;
        }

        private void UpdateStateForStep()
        {
            CurrentState = CurrentStepIndex switch
            {
                0 => WizardState.Idle,
                1 => WizardState.RoofValidated,
                2 => WizardState.PointsPicked,
                _ => CurrentState
            };
        }
    }
}
