// ===============================
// File: SectionFromLineExternalEvent.cs
// ===============================

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CSFL_V07.ViewModels;
using Revit26_Plugin.CSFL_V07.Services.Orchestration;

namespace Revit26_Plugin.CSFL_V07.Services.Execution
{
    /// <summary>
    /// Revit ExternalEvent handler responsible for executing
    /// section creation logic on the Revit API thread.
    /// </summary>
    public class SectionFromLineExternalEvent : IExternalEventHandler
    {
        private readonly UIDocument _uiDoc;
        private readonly ViewPlan _planView;
        private readonly SectionFromLineViewModel _viewModel;

        private IList<Reference> _references;

        public SectionFromLineExternalEvent(
            UIDocument uiDoc,
            ViewPlan planView,
            SectionFromLineViewModel viewModel)
        {
            _uiDoc = uiDoc ?? throw new ArgumentNullException(nameof(uiDoc));
            _planView = planView ?? throw new ArgumentNullException(nameof(planView));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        }

        /// <summary>
        /// Called by DialogService when user presses "Create".
        /// </summary>
        public void Raise(IList<Reference> references)
        {
            _references = references;
            ExternalEvent.Create(this).Raise();
        }

        public void Execute(UIApplication app)
        {
            Document doc = _uiDoc.Document;

            if (_references == null || _references.Count == 0)
                return;

            try
            {
                var orchestrator = new SectionFromLineOrchestrator(
                    doc,
                    _planView,
                    _viewModel);

                orchestrator.Start(_references);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Section From Line", ex.Message);
            }
        }

        public string GetName()
        {
            return "Section From Line External Event";
        }
    }
}
