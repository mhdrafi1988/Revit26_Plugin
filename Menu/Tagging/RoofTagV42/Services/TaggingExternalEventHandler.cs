using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26.RoofTagV42.Services;
using Revit26.RoofTagV42.ViewModels;
using Revit26.RoofTagV42.Views;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Revit26.RoofTagV42.Commands
{
    public class TaggingExternalEventHandler : IExternalEventHandler
    {
        private readonly UIApplication _uiApp;
        private readonly RoofBase _selectedRoof;
        private List<XYZ> _pointsToTag = new List<XYZ>();
        private RoofTagViewModel _viewModel;

        public TaggingExternalEventHandler(UIApplication uiApp, RoofBase selectedRoof)
        {
            _uiApp = uiApp;
            _selectedRoof = selectedRoof;
        }

        public void SetPoints(List<XYZ> points)
        {
            _pointsToTag = points ?? new List<XYZ>();
        }

        public void SetViewModel(RoofTagViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void Execute(UIApplication app)
        {
            try
            {
                if (_pointsToTag == null || _pointsToTag.Count == 0)
                {
                    LogToViewModel("ERROR: No points selected for tagging");
                    NotifyCompletion(0, 0, 0);
                    return;
                }

                if (_selectedRoof == null)
                {
                    LogToViewModel("ERROR: No roof selected");
                    NotifyCompletion(0, 0, 0);
                    return;
                }

                var doc = app.ActiveUIDocument.Document;
                var view = doc.ActiveView;

                if (view == null)
                {
                    LogToViewModel("ERROR: No active view found");
                    NotifyCompletion(0, 0, 0);
                    return;
                }

                LogToViewModel("Starting tagging operation in Revit context...");
                LogToViewModel($"Active view: {view.Name} ({view.ViewType})");

                // Get roof geometry data once
                var centroid = GeometryService.CalculateXYCentroid(_pointsToTag);
                var boundary = GeometryService.GetRoofBoundaryXY(_selectedRoof);

                LogToViewModel($"Processing {_pointsToTag.Count} points...");
                LogToViewModel($"Centroid: X={centroid.X:F2}, Y={centroid.Y:F2}");
                LogToViewModel($"Boundary points: {boundary.Count}");

                int tagsPlaced = 0;
                int tagsFailed = 0;
                var placedTagLocations = new List<XYZ>();

                using (Transaction t = new Transaction(doc, "Place Roof Spot Elevations"))
                {
                    LogToViewModel("Starting transaction...");
                    t.Start();

                    var taggingService = new TaggingService();
                    var window = Application.Current.Windows.OfType<RoofTagWindow>().FirstOrDefault();

                    for (int i = 0; i < _pointsToTag.Count; i++)
                    {
                        var point = _pointsToTag[i];

                        try
                        {
                            LogToViewModel($"Processing point {i + 1}/{_pointsToTag.Count}: " +
                                         $"X={point.X:F2}, Y={point.Y:F2}, Z={point.Z:F2}");

                            var result = taggingService.PlaceRoofSpotElevation(
                                doc,
                                _selectedRoof,
                                point,
                                centroid,
                                boundary,
                                _viewModel,
                                window,
                                placedTagLocations);

                            if (result.Success)
                            {
                                tagsPlaced++;
                                placedTagLocations.Add(point);
                                LogToViewModel($"? Point {i + 1}: Tag placed successfully");
                            }
                            else
                            {
                                tagsFailed++;
                                LogToViewModel($"? Point {i + 1}: Failed - {result.Message}");
                            }
                        }
                        catch (Exception ex)
                        {
                            tagsFailed++;
                            LogToViewModel($"? Point {i + 1}: Error - {ex.Message}");
                        }
                    }

                    t.Commit();
                    LogToViewModel("Transaction committed successfully");
                }

                // Log summary
                LogToViewModel("========================================");
                LogToViewModel("TAGGING OPERATION SUMMARY");
                LogToViewModel($"Total points processed: {_pointsToTag.Count}");
                LogToViewModel($"Tags successfully placed: {tagsPlaced}");
                LogToViewModel($"Tags failed: {tagsFailed}");
                LogToViewModel("========================================");

                if (tagsPlaced > 0)
                {
                    if (tagsFailed == 0)
                    {
                        LogToViewModel("SUCCESS: All tags placed successfully!");
                    }
                    else
                    {
                        LogToViewModel($"PARTIAL SUCCESS: {tagsPlaced} tags placed, {tagsFailed} failed");
                    }
                }
                else
                {
                    LogToViewModel("FAILURE: No tags were placed");
                }

                NotifyCompletion(tagsPlaced, tagsFailed, _pointsToTag.Count);
            }
            catch (Exception ex)
            {
                LogToViewModel($"CRITICAL ERROR: {ex.Message}");
                LogToViewModel($"Stack trace: {ex.StackTrace}");
                NotifyCompletion(0, 0, 0);
            }
        }

        private void LogToViewModel(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _viewModel?.Log(message);
            });
        }

        private void NotifyCompletion(int tagsPlaced, int tagsFailed, int totalPoints)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _viewModel?.TaggingCompleted(tagsPlaced, tagsFailed, totalPoints);
            });
        }

        public string GetName()
        {
            return "Roof Tagging External Event";
        }
    }
}