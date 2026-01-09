using System;
using System.Collections.ObjectModel;
using System.Windows;

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Commands;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Models;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Creation;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Geometry;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Logging;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Workflow;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.ViewModels
{
    /// <summary>
    /// Main ViewModel for Roof Ridge Lines tool.
    /// Handles user interaction ONLY.
    /// All Revit model changes are executed via ExternalEvent.
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        private readonly UIDocument _uiDoc;
        private readonly Window _window;

        // ExternalEvent infrastructure (MANDATORY for modeless UI)
        private readonly ExternalEvent _externalEvent;
        private readonly RoofRidgeExternalEventHandler _handler;

        [ObservableProperty]
        private double pointInterval = 1.0;

        [ObservableProperty]
        private string statusMessage = "Ready";

        /// <summary>
        /// Live log bound to UI (bottom panel).
        /// </summary>
        public ObservableCollection<string> Log { get; } = new();

        public IRelayCommand SelectRoofCommand { get; }

        public MainViewModel(UIDocument uiDoc, Window window)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
            _window = window ?? throw new ArgumentNullException(nameof(window));

            // Build services (pure, reusable)
            var creator = new RidgeElementCreator(
                new RidgeGeometryCalculator(),
                new RidgeIntersectionFinder());

            var workflow = new RoofRidgeWorkflow(creator);

            // ExternalEvent handler (executes on Revit API thread)
            _handler = new RoofRidgeExternalEventHandler(workflow);
            _externalEvent = ExternalEvent.Create(_handler);

            SelectRoofCommand = new RelayCommand(ExecuteWorkflow);
        }

        /// <summary>
        /// Collects user input ONLY.
        /// Raises ExternalEvent to perform model changes.
        /// </summary>
        private void ExecuteWorkflow()
        {
            Log.Clear();

            try
            {
                _window.Hide();

                LogStep("Select roof element...");
                var roofRef = _uiDoc.Selection.PickObject(
                    Autodesk.Revit.UI.Selection.ObjectType.Element);

                var roof = _uiDoc.Document.GetElement(roofRef) as RoofBase
                    ?? throw new InvalidOperationException("Selected element is not a roof.");

                LogStep("Pick ridge start point...");
                XYZ start = _uiDoc.Selection.PickPoint();

                LogStep("Pick ridge end point...");
                XYZ end = _uiDoc.Selection.PickPoint();

                var context = new RoofRidgeContext(
                    _uiDoc,
                    roof,
                    start,
                    end,
                    PointInterval);

                // Pass context to handler and raise ExternalEvent
                _handler.Context = context;
                _externalEvent.Raise();

                LogStep("Processing on Revit API thread...");
                StatusMessage = "Running...";
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                LogStep("Operation cancelled by user.");
                StatusMessage = "Cancelled.";
            }
            catch (Exception ex)
            {
                new FileOperationLogger().Log(ex);
                LogStep("ERROR:");
                LogStep(ex.Message);
                StatusMessage = "Failed. See log.";
            }
            finally
            {
                _window.Show();
            }
        }

        private void LogStep(string message)
        {
            Log.Add(message);
        }
    }
}
