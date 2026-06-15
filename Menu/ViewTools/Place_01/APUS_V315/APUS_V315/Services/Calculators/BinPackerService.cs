using System;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V315.Services.Calculators;

public sealed class BinPackerService
{
    private readonly List<FreeRect> _freeRects = new();
    private readonly double _binWidth;
    private readonly double _binHeight;

    public BinPackerService(double width, double height)
    {
        _binWidth = width;
        _binHeight = height;
        _freeRects.Add(new FreeRect(0, 0, width, height));
    }

    public bool TryPack(double width, double height, out double x, out double y)
    {
        x = y = 0;

        for (int i = 0; i < _freeRects.Count; i++)
        {
            var rect = _freeRects[i];

            if (width <= rect.Width && height <= rect.Height)
            {
                x = rect.X;
                y = rect.Y;
                SplitFreeRect(i, width, height);
                return true;
            }

            if (height <= rect.Width && width <= rect.Height)
            {
                x = rect.X;
                y = rect.Y;
                SplitFreeRect(i, height, width);
                return true;
            }
        }

        return false;
    }

    private void SplitFreeRect(int index, double placedWidth, double placedHeight)
    {
        var rect = _freeRects[index];
        _freeRects.RemoveAt(index);

        if (rect.Width - placedWidth > 0.001)
        {
            _freeRects.Add(new FreeRect(
                rect.X + placedWidth,
                rect.Y,
                rect.Width - placedWidth,
                placedHeight));
        }

        if (rect.Height - placedHeight > 0.001)
        {
            _freeRects.Add(new FreeRect(
                rect.X,
                rect.Y + placedHeight,
                rect.Width,
                rect.Height - placedHeight));
        }

        MergeFreeRects();
    }

    private void MergeFreeRects()
    {
        for (int i = 0; i < _freeRects.Count; i++)
        {
            for (int j = i + 1; j < _freeRects.Count; j++)
            {
                if (Math.Abs(_freeRects[i].Y - _freeRects[j].Y) < 0.001 &&
                    Math.Abs(_freeRects[i].Height - _freeRects[j].Height) < 0.001 &&
                    Math.Abs(_freeRects[i].X + _freeRects[i].Width - _freeRects[j].X) < 0.001)
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

        for (int i = 0; i < _freeRects.Count; i++)
        {
            for (int j = i + 1; j < _freeRects.Count; j++)
            {
                if (Math.Abs(_freeRects[i].X - _freeRects[j].X) < 0.001 &&
                    Math.Abs(_freeRects[i].Width - _freeRects[j].Width) < 0.001 &&
                    Math.Abs(_freeRects[i].Y + _freeRects[i].Height - _freeRects[j].Y) < 0.001)
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

    public double CalculateEfficiency(double totalAreaPlaced)
    {
        double binArea = _binWidth * _binHeight;
        return binArea > 0 ? totalAreaPlaced / binArea : 0;
    }

    public double GetFreeArea()
    {
        double area = 0;
        foreach (var rect in _freeRects)
            area += rect.Width * rect.Height;
        return area;
    }

    private readonly record struct FreeRect(double X, double Y, double Width, double Height);
}