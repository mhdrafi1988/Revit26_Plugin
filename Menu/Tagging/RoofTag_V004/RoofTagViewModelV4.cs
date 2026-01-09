using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Revit22_Plugin.RoofTagV4.Models;

namespace Revit22_Plugin.RoofTagV4.ViewModels
{
    public class RoofTagViewModelV4 : INotifyPropertyChanged
    {
        private readonly UIDocument _uiDoc;
        private readonly RoofBase _roof;
        private readonly RoofLoopsModel _geom;

        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string name = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));


        // ============================================================
        //  CONSTRUCTOR — receives roof + geometry before UI opens
        // ============================================================
        public RoofTagViewModelV4(UIDocument uiDoc, RoofBase roof, RoofLoopsModel geom)
        {
            _uiDoc = uiDoc;
            _roof = roof;
            _geom = geom;

            LoadSpotTagTypes();

            // Defaults
            BendOffset = 300;
            EndOffset = 500;
            SelectedAngle = 45;
            BendInward = true;
            ClutterThreshold = 500;
            DrainThreshold = 300;
            UseLeader = true;

            // Preload Roof Info for UI
            VertexCount = geom.AllVertices.Count;
            BoundaryCount = geom.Boundary.Count;

            try
            {
                ShapeEditVertexCount = roof.GetSlabShapeEditor().SlabShapeVertices.Size;
            }
            catch
            {
                ShapeEditVertexCount = 0;
            }

            RoofInfo =
                $"Roof Selected\n" +
                $"Vertices: {VertexCount}\n" +
                $"Boundary Points: {BoundaryCount}\n" +
                $"Slab Shape Vertices: {ShapeEditVertexCount}";

            ResultMessage = "Ready.";
        }


        // ============================================================
        //  ROOF INFORMATION (for UI log display)
        // ============================================================
        public int VertexCount { get; set; }
        public int BoundaryCount { get; set; }
        public int ShapeEditVertexCount { get; set; }

        private string _roofInfo;
        public string RoofInfo
        {
            get => _roofInfo;
            set { _roofInfo = value; Notify(); }
        }


        // ============================================================
        //  RESULT LOGGING (for UI output)
        // ============================================================
        private string _resultMessage;
        public string ResultMessage
        {
            get => _resultMessage;
            set { _resultMessage = value; Notify(); }
        }

        public int SuccessCount { get; set; }
        public int FailCount { get; set; }


        // ============================================================
        //  BEND OFFSET (mm)
        // ============================================================
        private double _bendOffset;
        public double BendOffset
        {
            get => _bendOffset;
            set { _bendOffset = value; Notify(); Notify(nameof(BendOffsetFt)); }
        }
        public double BendOffsetFt => BendOffset / 304.8;


        // ============================================================
        //  END OFFSET (mm)
        // ============================================================
        private double _endOffset;
        public double EndOffset
        {
            get => _endOffset;
            set { _endOffset = value; Notify(); Notify(nameof(EndOffsetFt)); }
        }
        public double EndOffsetFt => EndOffset / 304.8;


        // ============================================================
        //  BEND ANGLE (30° or 45°)
        // ============================================================
        private double _selectedAngle;
        public double SelectedAngle
        {
            get => _selectedAngle;
            set { _selectedAngle = value; Notify(); }
        }

        public bool IsAngle30
        {
            get => SelectedAngle == 30;
            set { if (value) SelectedAngle = 30; Notify(); }
        }

        public bool IsAngle45
        {
            get => SelectedAngle == 45;
            set { if (value) SelectedAngle = 45; Notify(); }
        }


        // ============================================================
        //  BEND DIRECTION
        // ============================================================
        private bool _bendInward;
        public bool BendInward
        {
            get => _bendInward;
            set
            {
                _bendInward = value;
                Notify();
                Notify(nameof(BendOutward));
            }
        }

        public bool BendOutward
        {
            get => !_bendInward;
            set
            {
                _bendInward = !value;
                Notify();
                Notify(nameof(BendInward));
            }
        }


        // ============================================================
        //  LEADER SETTINGS
        // ============================================================
        private bool _useLeader;
        public bool UseLeader
        {
            get => _useLeader;
            set { _useLeader = value; Notify(); }
        }


        // ============================================================
        //  FILTER THRESHOLDS (mm)
        // ============================================================
        public double ClutterThreshold { get; set; }
        public double DrainThreshold { get; set; }


        // ============================================================
        //  SPOT TAG TYPE DROPDOWN
        // ============================================================
        public ObservableCollection<SpotTagTypeWrapper> SpotTagTypes { get; set; }
            = new ObservableCollection<SpotTagTypeWrapper>();

        private SpotTagTypeWrapper _selectedSpotTagType;
        public SpotTagTypeWrapper SelectedSpotTagType
        {
            get => _selectedSpotTagType;
            set { _selectedSpotTagType = value; Notify(); }
        }

        private void LoadSpotTagTypes()
        {
            SpotTagTypes.Clear();
            Document doc = _uiDoc.Document;

            var col = new FilteredElementCollector(doc)
                .OfClass(typeof(SpotDimensionType));

            foreach (SpotDimensionType type in col)
                SpotTagTypes.Add(new SpotTagTypeWrapper(type));

            if (SpotTagTypes.Count > 0)
                SelectedSpotTagType = SpotTagTypes[0];
        }
    }


    // ============================================================
    //  WRAPPER FOR TAG DROPDOWN
    // ============================================================
    public class SpotTagTypeWrapper
    {
        public SpotDimensionType TagType { get; }
        public string Name => TagType.Name;

        public SpotTagTypeWrapper(SpotDimensionType tag)
        {
            TagType = tag;
        }
    }
}
