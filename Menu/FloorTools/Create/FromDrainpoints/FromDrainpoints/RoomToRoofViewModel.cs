using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;

namespace Revit26_Plugin.RoomToRoofUI
{
    public class RoofTypeOption
    {
        public string Name { get; set; }
        public ElementId Id { get; set; }
        public override string ToString() => Name;
    }

    public class RoomToRoofViewModel : INotifyPropertyChanged
    {
        private UIApplication _uiApp;
        private Document _doc;
        private Dictionary<string, Document> _linkedDocsByName;

        public ObservableCollection<string> LinkedModels { get; set; }
        public ObservableCollection<string> AvailableLevels { get; set; }
        public ObservableCollection<RoofTypeOption> RoofTypes { get; set; }

        private string _selectedLinkedModel;
        public string SelectedLinkedModel
        {
            get => _selectedLinkedModel;
            set
            {
                _selectedLinkedModel = value;
                OnPropertyChanged(nameof(SelectedLinkedModel));
                LoadLevelsWithRooms();
            }
        }

        private string _selectedLevel;
        public string SelectedLevel
        {
            get => _selectedLevel;
            set
            {
                _selectedLevel = value;
                OnPropertyChanged(nameof(SelectedLevel));
                LoadRoomsForLevel();
            }
        }

        private RoofTypeOption _selectedRoofType;
        public RoofTypeOption SelectedRoofType
        {
            get => _selectedRoofType;
            set
            {
                _selectedRoofType = value;
                OnPropertyChanged(nameof(SelectedRoofType));
            }
        }

        public ObservableCollection<RoomViewModel> Rooms { get; set; }
        public ObservableCollection<RoomViewModel> SelectedRooms { get; set; }

        public ICommand CreateRoofsCommand { get; set; }

        private string _lastUsedRoofTypeName;

        public RoomToRoofViewModel(UIApplication uiApp)
        {
            _uiApp = uiApp;
            _doc = uiApp.ActiveUIDocument.Document;
            _linkedDocsByName = new Dictionary<string, Document>();

            LinkedModels = new ObservableCollection<string>();
            AvailableLevels = new ObservableCollection<string>();
            RoofTypes = new ObservableCollection<RoofTypeOption>();
            Rooms = new ObservableCollection<RoomViewModel>();
            SelectedRooms = new ObservableCollection<RoomViewModel>();

            LoadLinkedModelsWithRooms();
            LoadAvailableRoofTypes();

            CreateRoofsCommand = new RelayCommand(p => ExecuteRoofCreation());
        }

        private void LoadLinkedModelsWithRooms()
        {
            var links = new FilteredElementCollector(_doc)
                        .OfClass(typeof(RevitLinkInstance))
                        .Cast<RevitLinkInstance>();

            foreach (var linkInstance in links)
            {
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null) continue;

                bool hasRooms = new FilteredElementCollector(linkDoc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Any();

                if (hasRooms)
                {
                    string name = linkDoc.Title;
                    LinkedModels.Add(name);
                    _linkedDocsByName[name] = linkDoc;
                }
            }

            if (LinkedModels.Count > 0)
                SelectedLinkedModel = LinkedModels.First();
        }

        private void LoadLevelsWithRooms()
        {
            AvailableLevels.Clear();
            Rooms.Clear();

            if (!_linkedDocsByName.ContainsKey(SelectedLinkedModel))
                return;

            Document linkDoc = _linkedDocsByName[SelectedLinkedModel];

            var rooms = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Level != null)
                .ToList();

            var levels = rooms
                .Select(r => r.Level)
                .Distinct(new ElementIdComparer())
                .ToList();

            foreach (var lvl in levels)
            {
                if (lvl != null)
                    AvailableLevels.Add(lvl.Name);
            }

            if (AvailableLevels.Count > 0)
                SelectedLevel = AvailableLevels.First();
        }

        private void LoadRoomsForLevel()
        {
            Rooms.Clear();

            if (!_linkedDocsByName.ContainsKey(SelectedLinkedModel))
                return;

            Document linkDoc = _linkedDocsByName[SelectedLinkedModel];

            var allRooms = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Level != null && r.Level.Name == SelectedLevel)
                .ToList();

            int counter = 1;
            foreach (Room r in allRooms)
            {
                Rooms.Add(new RoomViewModel
                {
                    SerialNumber = counter++,
                    LevelName = r.Level.Name,
                    RoomName = r.Name,
                    RoomNumber = r.Number,
                    Status = "⏳"
                });
            }
        }

        private void LoadAvailableRoofTypes()
        {
            RoofTypes.Clear();

            var types = new FilteredElementCollector(_doc)
                .OfClass(typeof(RoofType))
                .Cast<RoofType>()
                .Select(rt => new RoofTypeOption
                {
                    Name = rt.Name,
                    Id = rt.Id
                })
                .ToList();

            if (!string.IsNullOrEmpty(_lastUsedRoofTypeName))
            {
                var last = types.FirstOrDefault(t => t.Name == _lastUsedRoofTypeName);
                if (last != null)
                {
                    types.Remove(last);
                    types.Insert(0, last);
                }
            }

            foreach (var rt in types)
                RoofTypes.Add(rt);

            SelectedRoofType = RoofTypes.FirstOrDefault();
        }

        private void ExecuteRoofCreation()
        {
            if (SelectedRooms == null || SelectedRooms.Count == 0 || SelectedRoofType == null)
            {
                TaskDialog.Show("Error", "Please select at least one Room and a Roof Type.");
                return;
            }

            Document doc = _uiApp.ActiveUIDocument.Document;
            Document linkDoc = _linkedDocsByName.ContainsKey(SelectedLinkedModel) ? _linkedDocsByName[SelectedLinkedModel] : null;
            if (linkDoc == null)
            {
                TaskDialog.Show("Error", "Linked model not found.");
                return;
            }

            RoofType roofType = doc.GetElement(SelectedRoofType.Id) as RoofType;
            if (roofType == null)
            {
                TaskDialog.Show("Error", "Selected Roof Type not found in active model.");
                return;
            }

            _lastUsedRoofTypeName = SelectedRoofType.Name;

            int successCount = 0;
            int fallbackCount = 0;
            int failureCount = 0;

            using (TransactionGroup tg = new TransactionGroup(doc, "Create Roofs from Rooms"))
            {
                tg.Start();

                foreach (RoomViewModel vm in SelectedRooms)
                {
                    var matchingRoom = new FilteredElementCollector(linkDoc)
                        .OfCategory(BuiltInCategory.OST_Rooms)
                        .WhereElementIsNotElementType()
                        .Cast<Room>()
                        .FirstOrDefault(r => r.Number == vm.RoomNumber && r.Level.Name == vm.LevelName);

                    if (matchingRoom == null)
                    {
                        vm.Status = "❌ Not Found";
                        failureCount++;
                        continue;
                    }

                    IList<IList<BoundarySegment>> boundaries = matchingRoom.GetBoundarySegments(new SpatialElementBoundaryOptions());
                    if (boundaries == null || boundaries.Count == 0)
                    {
                        vm.Status = "❌ No Boundary";
                        failureCount++;
                        continue;
                    }

                    List<Curve> curveLoop = new List<Curve>();
                    foreach (BoundarySegment segment in boundaries[0])
                    {
                        Curve c = segment.GetCurve();
                        if (c != null)
                            curveLoop.Add(c.CreateTransformed(GetLinkTransform(linkDoc)));
                    }

                    Level hostLevel = GetMatchingHostLevel(matchingRoom.Level.Name, doc);
                    if (hostLevel == null)
                    {
                        vm.Status = "❌ Host Level Missing";
                        failureCount++;
                        continue;
                    }

                    using (Transaction tx = new Transaction(doc, "Create Roof"))
                    {
                        tx.Start();

                        try
                        {
                            CurveArray curveArray = new CurveArray();
                            foreach (Curve c in curveLoop)
                                curveArray.Append(c);

                            ModelCurveArray footprint = new ModelCurveArray();
                            FootPrintRoof roof = doc.Create.NewFootPrintRoof(curveArray, hostLevel, roofType, out footprint);
                            vm.Status = "✅ Success";
                            successCount++;
                        }
                        catch
                        {
                            try
                            {
                                SketchPlane sketchPlane = SketchPlane.Create(doc, Plane.CreateByNormalAndOrigin(XYZ.BasisZ, XYZ.Zero));
                                foreach (Curve c in curveLoop)
                                    doc.Create.NewModelCurve(c, sketchPlane);

                                vm.Status = "⚠️ Drawn Only (Fallback)";
                                fallbackCount++;
                            }
                            catch
                            {
                                vm.Status = "❌ Failed";
                                failureCount++;
                            }
                        }

                        tx.Commit();
                    }
                }

                tg.Assimilate();
            }

            TaskDialog.Show("Summary", $"Roofs Created: {successCount}\nFallbacks: {fallbackCount}\nFailures: {failureCount}");
        }

        private Transform GetLinkTransform(Document linkDoc)
        {
            RevitLinkInstance instance = new FilteredElementCollector(_doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .FirstOrDefault(i => i.GetLinkDocument()?.Title == linkDoc.Title);

            return instance?.GetTransform() ?? Transform.Identity;
        }

        private Level GetMatchingHostLevel(string levelName, Document hostDoc)
        {
            return new FilteredElementCollector(hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name == levelName);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class ElementIdComparer : IEqualityComparer<Element>
    {
        public bool Equals(Element x, Element y) => x.Id.Value == y.Id.Value;
        public int GetHashCode(Element obj) => (int)obj.Id.Value;
    }
}