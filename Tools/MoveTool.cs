using System;
using Avalonia;
using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Tools
{
    /// <summary>
    /// Tool for moving features - Simplified version for current system
    /// </summary>
    public class MoveTool : ToolBase
    {
        private bool _isMoving;
        private AvaloniaPoint _moveStartPoint;

        public override string Name => "Move";
        public override string Description => "Move selected features";

        public override void OnMouseDown(PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                return;

            var screenPoint = e.GetCurrentPoint(null).Position;
            
            // Start move operation
            _isMoving = true;
            _moveStartPoint = screenPoint;
            System.Console.WriteLine($"Starting move operation at {screenPoint}");
            
            e.Handled = true;
        }

        public override void OnMouseMove(PointerEventArgs e)
        {
            if (_isMoving)
            {
                var currentPoint = e.GetCurrentPoint(null).Position;
                var delta = currentPoint - _moveStartPoint;
                
                // Log the move delta - in a full implementation this would move selected features
                System.Console.WriteLine($"Move delta: {delta}");
                
                e.Handled = true;
            }
        }

        public override void OnMouseUp(PointerReleasedEventArgs e)
        {
            if (_isMoving)
            {
                var currentPoint = e.GetCurrentPoint(null).Position;
                var totalDelta = currentPoint - _moveStartPoint;
                
                System.Console.WriteLine($"Move completed. Total delta: {totalDelta}");
                
                _isMoving = false;
                e.Handled = true;
            }
        }

        public override string GetStatusText()
        {
            if (_isMoving)
                return "Move - Dragging features, release to complete";
            else
                return "Move - Click and drag to move selected features";
        }
    }
}