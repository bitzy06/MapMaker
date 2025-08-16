using System;
using Avalonia;
using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Tools
{
    /// <summary>
    /// Tool for selecting features - Simplified version for current system
    /// </summary>
    public class SelectTool : ToolBase
    {
        private bool _isDragging;
        private AvaloniaPoint _dragStartPoint;
        private Rect _selectionRect;

        public override string Name => "Select";
        public override string Description => "Select features and vertices";

        public override void OnMouseDown(PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                return;

            var screenPoint = e.GetCurrentPoint(null).Position;
            
            // For now, just start a selection rectangle
            _isDragging = true;
            _dragStartPoint = screenPoint;
            _selectionRect = new Rect(screenPoint, screenPoint);
            e.Handled = true;
        }

        public override void OnMouseMove(PointerEventArgs e)
        {
            if (_isDragging)
            {
                var currentPoint = e.GetCurrentPoint(null).Position;
                
                // Update marquee selection rectangle
                var left = Math.Min(_dragStartPoint.X, currentPoint.X);
                var top = Math.Min(_dragStartPoint.Y, currentPoint.Y);
                var right = Math.Max(_dragStartPoint.X, currentPoint.X);
                var bottom = Math.Max(_dragStartPoint.Y, currentPoint.Y);
                
                _selectionRect = new Rect(left, top, right - left, bottom - top);
                e.Handled = true;
            }
        }

        public override void OnMouseUp(PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                // Complete selection - for now just log it
                System.Console.WriteLine($"Selected area: {_selectionRect}");
                
                _isDragging = false;
                _selectionRect = default;
                e.Handled = true;
            }
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    // Clear selection
                    System.Console.WriteLine("Selection cleared");
                    e.Handled = true;
                    break;
            }
        }

        public override string GetStatusText()
        {
            return "Select - Click and drag to select features, Esc to clear selection";
        }
    }
}