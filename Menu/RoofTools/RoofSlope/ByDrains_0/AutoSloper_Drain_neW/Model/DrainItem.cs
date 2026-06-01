// =======================================================
// File: Models/DrainItem.cs
// Description: Drain/opening data model for DataGrid
// =======================================================

using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit26_Plugin.AutoSlopeByDrain_21.Models
{
    /// <summary>
    /// Represents a drain or roof opening detected in Revit
    /// </summary>
    public class DrainItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private int _drainId;
        public int DrainId
        {
            get => _drainId;
            set { _drainId = value; OnPropertyChanged(); }
        }

        private string _shapeType;
        public string ShapeType
        {
            get => _shapeType;
            set { _shapeType = value; OnPropertyChanged(); }
        }

        private string _sizeCategory;
        public string SizeCategory
        {
            get => _sizeCategory;
            set { _sizeCategory = value; OnPropertyChanged(); }
        }

        private double _width;
        public double Width
        {
            get => _width;
            set { _width = value; OnPropertyChanged(); }
        }

        private double _height;
        public double Height
        {
            get => _height;
            set { _height = value; OnPropertyChanged(); }
        }

        private Point3D _centerPoint;
        public Point3D CenterPoint
        {
            get => _centerPoint;
            set { _centerPoint = value; OnPropertyChanged(); }
        }

        private List<Point3D> _drainVertices;
        public List<Point3D> DrainVertices
        {
            get => _drainVertices;
            set { _drainVertices = value; OnPropertyChanged(); }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        // Revit ElementId (store as string for UI, convert when needed)
        private string _elementId;
        public string ElementId
        {
            get => _elementId;
            set { _elementId = value; OnPropertyChanged(); }
        }

        public DrainItem()
        {
            DrainVertices = new List<Point3D>();
            CenterPoint = new Point3D();
            IsSelected = true; // Default to selected
        }
    }
}