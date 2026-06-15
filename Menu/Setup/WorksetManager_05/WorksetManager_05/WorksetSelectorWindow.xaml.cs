using Revit26_Plugin.WorksetManager_06.Models;
using Revit26_Plugin.WorksetManager_06.ViewModels;
using Revit26_Plugin.Shared.Models;
using System;

namespace Revit26_Plugin.WorksetManager_06.Views
{
    // ── Window ────────────────────────────────────────────────────────────────

    public partial class WorksetSelectorWindow : MahApps.Metro.Controls.MetroWindow
    {
        public WorksetSelectorWindow(WorksetsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose += () => Close();
        }
    }
}
