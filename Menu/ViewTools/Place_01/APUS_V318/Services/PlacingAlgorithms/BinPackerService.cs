// File: BinPackerService.cs
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V318.Services
{
    /// <summary>
    /// Guillotine bin packer for optimal space utilization
    /// </summary>
    public class BinPackerService
    {
        private readonly List<FreeRect> _freeRects;
        private readonly double _binWidth;
        private readonly double _binHeight;

        public BinPackerService(double width, double height)
        {
            _binWidth = width;
            _binHeight = height;
            _freeRects = new List<FreeRect> { new FreeRect(0, 0, width, height) };
        }

        /// <summary>
        /// Try to pack a rectangle into the bin
        /// </summary>
        public bool TryPack(double width, double height, out double x, out double y)
        {
            // Try each free rectangle
            for (int i = 0; i < _freeRects.Count; i++)
            {
                var rect = _freeRects[i];

                // Check if rectangle fits
                if (width <= rect.Width && height <= rect.Height)
                {
                    // Place in bottom-left corner
                    x = rect.X;
                    y = rect.Y;

                    // Split the free rectangle
                    SplitFreeRect(i, width, height);
                    return true;
                }

                // Try rotated (if rotation is allowed)
                if (height <= rect.Width && width <= rect.Height)
                {
                    x = rect.X;
                    y = rect.Y;

                    SplitFreeRect(i, height, width);
                    return true;
                }
            }

            x = y = 0;
            return false;
        }

        /// <summary>
        /// Split free rectangle after placement
        /// </summary>
        private void SplitFreeRect(int index, double placedWidth, double placedHeight)
        {
            var rect = _freeRects[index];
            _freeRects.RemoveAt(index);

            // Split horizontally (right part)
            if (rect.Width - placedWidth > 0)
            {
                _freeRects.Add(new FreeRect(
                    rect.X + placedWidth,
                    rect.Y,
                    rect.Width - placedWidth,
                    placedHeight));
            }

            // Split vertically (top part)
            if (rect.Height - placedHeight > 0)
            {
                _freeRects.Add(new FreeRect(
                    rect.X,
                    rect.Y + placedHeight,
                    rect.Width,
                    rect.Height - placedHeight));
            }

            // Merge free rectangles
            MergeFreeRects();
        }

        /// <summary>
        /// Merge adjacent free rectangles
        /// </summary>
        private void MergeFreeRects()
        {
            // Merge horizontally adjacent
            for (int i = 0; i < _freeRects.Count; i++)
            {
                for (int j = i + 1; j < _freeRects.Count; j++)
                {
                    if (_freeRects[i].Y == _freeRects[j].Y &&
                        _freeRects[i].Height == _freeRects[j].Height &&
                        _freeRects[i].X + _freeRects[i].Width == _freeRects[j].X)
                    {
                        _freeRects[i] = new FreeRect(
                            _freeRects[i].X,
                            _freeRects[i].Y,
                            _freeRects[i].Width + _freeRects[j].Width,
                            _freeRects[i].Height);
                        _freeRects.RemoveAt(j);
                        j--;
                    }
                }
            }

            // Merge vertically adjacent
            for (int i = 0; i < _freeRects.Count; i++)
            {
                for (int j = i + 1; j < _freeRects.Count; j++)
                {
                    if (_freeRects[i].X == _freeRects[j].X &&
                        _freeRects[i].Width == _freeRects[j].Width &&
                        _freeRects[i].Y + _freeRects[i].Height == _freeRects[j].Y)
                    {
                        _freeRects[i] = new FreeRect(
                            _freeRects[i].X,
                            _freeRects[i].Y,
                            _freeRects[i].Width,
                            _freeRects[i].Height + _freeRects[j].Height);
                        _freeRects.RemoveAt(j);
                        j--;
                    }
                }
            }
        }

        /// <summary>
        /// Calculate packing efficiency (0-1)
        /// </summary>
        public double CalculateEfficiency(double totalAreaPlaced)
        {
            double binArea = _binWidth * _binHeight;
            return binArea > 0 ? totalAreaPlaced / binArea : 0;
        }

        /// <summary>
        /// Get remaining free area
        /// </summary>
        public double GetFreeArea()
        {
            double freeArea = 0;
            foreach (var rect in _freeRects)
            {
                freeArea += rect.Width * rect.Height;
            }
            return freeArea;
        }

        private class FreeRect
        {
            public double X { get; }
            public double Y { get; }
            public double Width { get; }
            public double Height { get; }

            public FreeRect(double x, double y, double width, double height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
            }
        }
    }
}