using Avalonia;
using Avalonia.Input;
using MapMaker.Services;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Tools
{
    /// <summary>
    /// Tool for navigating the map (pan and zoom)
    /// </summary>
    public class NavigateTool : ToolBase
    {
        private bool _isPanning;
        private AvaloniaPoint _lastPanPosition;

        public override string Name => "Navigate";
        public override string Description => "Pan and zoom the map";

        public override void OnMouseDown(PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
            {
                _isPanning = true;
                _lastPanPosition = e.GetCurrentPoint(null).Position;
                e.Handled = true;
            }
        }

        public override void OnMouseMove(PointerEventArgs e)
        {
            var currentPoint = e.GetCurrentPoint(null);
            
            // Update cursor world position for status display
            if (Context?.EditorState != null)
            {
                Context.EditorState.CursorScreenPosition = currentPoint.Position;
                Context.EditorState.CursorWorldPosition = ScreenToWorld(currentPoint.Position);
            }

            if (_isPanning)
            {
                var currentPosition = currentPoint.Position;
                var delta = currentPosition - _lastPanPosition;
                
                // Update document pan offset
                if (Context?.Document != null)
                {
                    Context.Document.PanOffset = new AvaloniaPoint(
                        Context.Document.PanOffset.X + delta.X,
                        Context.Document.PanOffset.Y + delta.Y
                    );
                }

                _lastPanPosition = currentPosition;
                e.Handled = true;
            }
        }

        public override void OnMouseUp(PointerReleasedEventArgs e)
        {
            _isPanning = false;
            e.Handled = true;
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            // Handle keyboard shortcuts for navigation
            if (Context?.Document != null)
            {
                const double panStep = 50.0;
                var panDelta = new AvaloniaPoint(0, 0);

                switch (e.Key)
                {
                    case Key.Left:
                        panDelta = new AvaloniaPoint(panStep, 0);
                        break;
                    case Key.Right:
                        panDelta = new AvaloniaPoint(-panStep, 0);
                        break;
                    case Key.Up:
                        panDelta = new AvaloniaPoint(0, panStep);
                        break;
                    case Key.Down:
                        panDelta = new AvaloniaPoint(0, -panStep);
                        break;
                }

                if (panDelta.X != 0 || panDelta.Y != 0)
                {
                    Context.Document.PanOffset = new AvaloniaPoint(
                        Context.Document.PanOffset.X + panDelta.X,
                        Context.Document.PanOffset.Y + panDelta.Y
                    );
                    e.Handled = true;
                }
            }
        }

        public override string GetStatusText()
        {
            var worldPos = Context?.EditorState?.CursorWorldPosition;
            if (worldPos != null)
            {
                return $"Navigate - World: ({worldPos.X:F2}, {worldPos.Y:F2}) | Click and drag to pan";
            }
            return "Navigate - Click and drag to pan, arrow keys to move, mouse wheel to zoom";
        }
    }
}