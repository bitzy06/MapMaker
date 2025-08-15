using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Input;
using MapMaker.Models;
using MapMaker.Services;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Utilities;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Tools
{
    /// <summary>
    /// Tool for moving selected features and vertices
    /// </summary>
    public class MoveTool : ToolBase
    {
        private bool _isMoving;
        private AvaloniaPoint _moveStartPoint;
        private Coordinate? _moveStartWorldPoint;
        private List<(Feature feature, Geometry originalGeometry)> _originalGeometries = new();

        public override string Name => "Move";
        public override string Description => "Move selected features and vertices";

        protected override void OnActivated()
        {
            _originalGeometries.Clear();
        }

        public override void OnMouseDown(PointerPressedEventArgs e)
        {
            if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed)
                return;

            var screenPoint = e.GetCurrentPoint(null).Position;
            var worldPoint = ScreenToWorld(screenPoint);
            
            // Update cursor position
            if (Context?.EditorState != null)
            {
                Context.EditorState.CursorScreenPosition = screenPoint;
                Context.EditorState.CursorWorldPosition = worldPoint;
            }

            // Check if we have selected features
            var selection = Context?.EditorState.Selection;
            if (selection == null || selection.FeatureCount == 0)
            {
                // No selection - switch to select tool behavior or show message
                return;
            }

            // Start move operation
            _isMoving = true;
            _moveStartPoint = screenPoint;
            _moveStartWorldPoint = worldPoint;
            
            // Store original geometries for undo
            _originalGeometries.Clear();
            foreach (var feature in selection.Features)
            {
                _originalGeometries.Add((feature, feature.Geometry.Copy()));
            }

            e.Handled = true;
        }

        public override void OnMouseMove(PointerEventArgs e)
        {
            var screenPoint = e.GetCurrentPoint(null).Position;
            var worldPoint = ScreenToWorld(screenPoint);
            
            // Update cursor position
            if (Context?.EditorState != null)
            {
                Context.EditorState.CursorScreenPosition = screenPoint;
                Context.EditorState.CursorWorldPosition = worldPoint;
            }

            if (_isMoving)
            {
                // Calculate move delta
                var deltaX = worldPoint.X - (_moveStartWorldPoint?.X ?? 0);
                var deltaY = worldPoint.Y - (_moveStartWorldPoint?.Y ?? 0);

                // Apply snapping if enabled
                if (Context?.EditorState.SnapEnabled == true)
                {
                    var snappedPoint = ApplySnapping(worldPoint);
                    deltaX = snappedPoint.X - (_moveStartWorldPoint?.X ?? 0);
                    deltaY = snappedPoint.Y - (_moveStartWorldPoint?.Y ?? 0);
                }

                // Move selected features
                MoveSelectedFeatures(deltaX, deltaY);
                e.Handled = true;
            }
        }

        public override void OnMouseUp(PointerReleasedEventArgs e)
        {
            if (_isMoving)
            {
                // Complete move operation
                var screenPoint = e.GetCurrentPoint(null).Position;
                var worldPoint = ScreenToWorld(screenPoint);
                
                var deltaX = worldPoint.X - (_moveStartWorldPoint?.X ?? 0);
                var deltaY = worldPoint.Y - (_moveStartWorldPoint?.Y ?? 0);

                // Apply snapping if enabled
                if (Context?.EditorState.SnapEnabled == true)
                {
                    var snappedPoint = ApplySnapping(worldPoint);
                    deltaX = snappedPoint.X - (_moveStartWorldPoint?.X ?? 0);
                    deltaY = snappedPoint.Y - (_moveStartWorldPoint?.Y ?? 0);
                }

                // Create move command for undo/redo if there was actual movement
                if (Math.Abs(deltaX) > 1e-10 || Math.Abs(deltaY) > 1e-10)
                {
                    var moveCommand = new MoveFeatureCommand(_originalGeometries, deltaX, deltaY);
                    Context?.UndoRedoService?.Execute(moveCommand);
                }

                _isMoving = false;
                _originalGeometries.Clear();
                e.Handled = true;
            }
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            if (e.Key == Key.Escape && _isMoving)
            {
                // Cancel move operation - restore original geometries
                foreach (var (feature, originalGeometry) in _originalGeometries)
                {
                    feature.Geometry = originalGeometry;
                }
                
                _isMoving = false;
                _originalGeometries.Clear();
                e.Handled = true;
            }
        }

        public override void DrawOverlay(IDrawContext drawContext)
        {
            // Draw move preview or snap indicators
            if (_isMoving && Context?.EditorState != null)
            {
                // Could draw movement vector or other visual feedback
                var currentPoint = Context.EditorState.CursorScreenPosition;
                drawContext.DrawLine(_moveStartPoint, currentPoint, Avalonia.Media.Colors.Orange, 2);
            }
        }

        public override string GetStatusText()
        {
            var selection = Context?.EditorState.Selection;
            var worldPos = Context?.EditorState?.CursorWorldPosition;
            var posText = worldPos != null ? $" at ({worldPos.X:F2}, {worldPos.Y:F2})" : "";

            if (_isMoving)
            {
                var deltaX = (worldPos?.X ?? 0) - (_moveStartWorldPoint?.X ?? 0);
                var deltaY = (worldPos?.Y ?? 0) - (_moveStartWorldPoint?.Y ?? 0);
                return $"Move - Moving {selection?.FeatureCount ?? 0} feature(s) by ({deltaX:F2}, {deltaY:F2}) | ESC to cancel";
            }

            if (selection != null && selection.FeatureCount > 0)
            {
                return $"Move - {selection.FeatureCount} feature(s) selected{posText} | Click and drag to move";
            }

            return $"Move{posText} | Select features first, then drag to move";
        }

        private void MoveSelectedFeatures(double deltaX, double deltaY)
        {
            var selection = Context?.EditorState.Selection;
            if (selection == null) return;

            var transform = AffineTransformation.TranslationInstance(deltaX, deltaY);

            foreach (var (feature, originalGeometry) in _originalGeometries)
            {
                feature.Geometry = transform.Transform(originalGeometry);
            }
        }

        private Coordinate ApplySnapping(Coordinate worldPoint)
        {
            // Simple snapping implementation - can be enhanced with the snapping service
            if (Context?.EditorState.SnapEnabled != true)
                return worldPoint;

            // For now, just return the original point
            // In a full implementation, this would use the IHitTestService to find snap targets
            return worldPoint;
        }
    }

    /// <summary>
    /// Command to move features with undo support
    /// </summary>
    public class MoveFeatureCommand : EditorCommandBase
    {
        private readonly List<(Feature feature, Geometry originalGeometry)> _originalGeometries;
        private readonly double _deltaX;
        private readonly double _deltaY;

        public MoveFeatureCommand(List<(Feature feature, Geometry originalGeometry)> originalGeometries, double deltaX, double deltaY)
            : base($"Move {originalGeometries.Count} feature(s)")
        {
            _originalGeometries = new List<(Feature, Geometry)>(originalGeometries);
            _deltaX = deltaX;
            _deltaY = deltaY;
        }

        public override void Do()
        {
            var transform = AffineTransformation.TranslationInstance(_deltaX, _deltaY);
            foreach (var (feature, originalGeometry) in _originalGeometries)
            {
                feature.Geometry = transform.Transform(originalGeometry);
            }
        }

        public override void Undo()
        {
            foreach (var (feature, originalGeometry) in _originalGeometries)
            {
                feature.Geometry = originalGeometry.Copy();
            }
        }
    }
}