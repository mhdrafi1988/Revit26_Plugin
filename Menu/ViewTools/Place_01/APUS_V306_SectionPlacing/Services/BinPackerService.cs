using System.Collections.Generic;

namespace Revit26_Plugin.APUS_V306.Services
{
    /// <summary>
    /// Deterministic row-based bin packer.
    /// Preserves input order.
    /// </summary>
    internal class BinPackerService
    {
        private readonly double _sheetWidth;
        private readonly double _sheetHeight;

        private double _cursorX;
        private double _cursorY;
        private double _rowMaxHeight;

        public BinPackerService(double width, double height)
        {
            _sheetWidth = width;
            _sheetHeight = height;

            _cursorX = 0;
            _cursorY = 0;
            _rowMaxHeight = 0;
        }

        public bool TryPlace(
            double w,
            double h,
            out double x,
            out double y)
        {
            // New row if X overflow
            if (_cursorX + w > _sheetWidth)
            {
                _cursorX = 0;
                _cursorY += _rowMaxHeight;
                _rowMaxHeight = 0;
            }

            // Stop if Y overflow
            if (_cursorY + h > _sheetHeight)
            {
                x = y = 0;
                return false;
            }

            x = _cursorX;
            y = _cursorY;

            _cursorX += w;
            _rowMaxHeight = System.Math.Max(_rowMaxHeight, h);

            return true;
        }
    }
}
