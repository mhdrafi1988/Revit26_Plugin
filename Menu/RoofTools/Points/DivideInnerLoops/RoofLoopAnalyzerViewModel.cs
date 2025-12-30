using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit22_Plugin.PDCV1.Models;
using Revit22_Plugin.PDCV1.Services;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit22_Plugin.PDCV1.ViewModels
{
    public class RoofLoopAnalyzerViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly RoofBase _roof;
        private readonly RoofGeometryService _geometryService;
        private readonly c _divisionService;

        public ObservableCollection<RoofLoopModel> Loops { get; set; }

        private string _summary;
        public string Summary
        {
            get => _summary;
            set => SetProperty(ref _summary, value);
        }

        public IRelayCommand AnalyzeCommand { get; }
        public IRelayCommand ApplyDivisionCommand { get; }

        public RoofLoopAnalyzerViewModel(Document doc, RoofBase roof)
        {
            _doc = doc;
            _roof = roof;
            _geometryService = new RoofGeometryService();
            _divisionService = new c();
            Loops = new ObservableCollection<RoofLoopModel>();

            AnalyzeCommand = new RelayCommand(AnalyzeRoof);
            ApplyDivisionCommand = new RelayCommand(ApplyDivisions);
        }

        private void AnalyzeRoof()
        {
            Loops.Clear();
            var loops = _geometryService.ExtractCircularLoops(_roof);

            var innerLoops = loops.Where(l => l.LoopType == "Inner");

            foreach (var loop in innerLoops)
            {
                if (loop.LoopShapeType == "Circular")
                {
                    loop.RecommendedPoints = 2;
                }
                else
                {
                    loop.RecommendedPoints = 0;
                }

                Loops.Add(loop);
            }

            int total = Loops.Count;
            int circular = Loops.Count(l => l.LoopShapeType == "Circular");
            int rectangles = Loops.Count(l => l.LoopShapeType == "Rectangle");
            int others = Loops.Count(l => l.LoopShapeType == "Other");

            Summary = $"✅ Roof Analysis Complete\n" +
                      $"Inner Loops: {total}\n" +
                      $"Circular: {circular}, Rectangle: {rectangles}, Other: {others}";
        }


        private void ApplyDivisions()
        {
            var validLoops = Loops.Where(l => l.RecommendedPoints >= 1 && l.IsCircular).ToList();

            int totalPoints = validLoops.Sum(l => l.RecommendedPoints);

            _divisionService.AddDivisionPoints(_doc, _roof, validLoops.Cast<Revit22_Plugin.PDCV1.Models.RoofLoopModel>());

            Summary += $"\n\n📌 Division Points Applied Successfully.\n" +
                       $"Total Points Added: {totalPoints}";
        }
    }
}
