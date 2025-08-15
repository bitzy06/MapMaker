using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using MapMaker.Models;
using MapMaker.Services;
using NetTopologySuite.Geometries;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Tools
{
    /// <summary>
    /// Tool for selecting features and vertices
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
            var worldPoint = ScreenToWorld(screenPoint);
            
            // Update cursor position
            if (Context?.EditorState != null)
            {
                Context.EditorState.CursorScreenPosition = screenPoint;
                Context.EditorState.CursorWorldPosition = worldPoint;
            }

            // Check if we hit any features
            var hitFeatures = GetFeaturesUnderPoint(screenPoint);
            
            var isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);

            if (hitFeatures.Any())
            {
                // Feature selection
                var topFeature = hitFeatures.First();
                
                if (isCtrlPressed || isShiftPressed)
                {
                    // Toggle selection
                    Context?.EditorState.Selection.ToggleFeature(topFeature);
                }
                else if (Context?.EditorState?.Selection.Features.Contains(topFeature) != true)
                {
                    // Clear previous selection and select this feature
                    Context?.EditorState?.Selection.Clear();
                    Context?.EditorState?.Selection.AddFeature(topFeature);
                }
                
                e.Handled = true;
            }
            else
            {
                // Start marquee selection
                if (!isShiftPressed && !isCtrlPressed)
                {
                    Context?.EditorState.Selection.Clear();
                }

                _isDragging = true;
                _dragStartPoint = screenPoint;
                _selectionRect = new Rect(screenPoint, screenPoint);
                e.Handled = true;
            }
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

            if (_isDragging)
            {
                // Update marquee selection rectangle
                var left = Math.Min(_dragStartPoint.X, screenPoint.X);
                var top = Math.Min(_dragStartPoint.Y, screenPoint.Y);
                var right = Math.Max(_dragStartPoint.X, screenPoint.X);
                var bottom = Math.Max(_dragStartPoint.Y, screenPoint.Y);
                
                _selectionRect = new Rect(left, top, right - left, bottom - top);
                e.Handled = true;
            }
            else
            {
                // Update hover target
                var hitFeatures = GetFeaturesUnderPoint(screenPoint);
                var hoverTarget = hitFeatures.FirstOrDefault();
                Context?.EditorState.SetHoverTarget(hoverTarget);
            }
        }

        public override void OnMouseUp(PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                // Complete marquee selection
                var featuresInRect = GetFeaturesInRect(_selectionRect);
                
                var isShiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
                var isCtrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);

                if (isShiftPressed || isCtrlPressed)
                {
                    // Add to selection
                    Context?.EditorState.Selection.AddFeatures(featuresInRect);
                }
                else
                {
                    // Replace selection
                    Context?.EditorState.Selection.Clear();
                    Context?.EditorState.Selection.AddFeatures(featuresInRect);
                }

                _isDragging = false;
                _selectionRect = default;
                e.Handled = true;
            }
        }

        public override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.A when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                    // Select all features in active layer
                    SelectAllFeatures();
                    e.Handled = true;
                    break;
                    
                case Key.Escape:
                    // Clear selection
                    Context?.EditorState.Selection.Clear();
                    e.Handled = true;
                    break;
                    
                case Key.Delete:
                    // Delete selected features
                    DeleteSelectedFeatures();
                    e.Handled = true;
                    break;
            }
        }

        public override void DrawOverlay(IDrawContext drawContext)
        {
            // Draw marquee selection rectangle
            if (_isDragging && _selectionRect.Width > 0 && _selectionRect.Height > 0)
            {
                drawContext.DrawRectangle(_selectionRect, Avalonia.Media.Colors.Blue, filled: false);
            }

            // Draw selection handles for selected features
            DrawSelectionHandles(drawContext);
        }

        public override string GetStatusText()
        {
            var selection = Context?.EditorState.Selection;
            if (selection != null && selection.FeatureCount > 0)
            {
                var worldPos = Context?.EditorState?.CursorWorldPosition;
                var posText = worldPos != null ? $" at ({worldPos.X:F2}, {worldPos.Y:F2})" : "";
                return $"Select - {selection.FeatureCount} feature(s) selected{posText} | Click to select, drag for marquee";
            }

            var worldPos2 = Context?.EditorState?.CursorWorldPosition;
            var posText2 = worldPos2 != null ? $" - World: ({worldPos2.X:F2}, {worldPos2.Y:F2})" : "";
            return $"Select{posText2} | Click to select features, Ctrl+A for all, Del to delete";
        }

        private IEnumerable<Feature> GetFeaturesUnderPoint(AvaloniaPoint screenPoint)
        {
            if (Context?.EditorState.ActiveLayer == null)
                return Enumerable.Empty<Feature>();

            // Simple implementation - use spatial index with a small tolerance
            var worldPoint = ScreenToWorld(screenPoint);
            var tolerance = 10.0 / GetScreenScale(); // 10 pixels tolerance in world units
            var queryEnv = new Envelope(
                worldPoint.X - tolerance, worldPoint.X + tolerance,
                worldPoint.Y - tolerance, worldPoint.Y + tolerance);

            return Context.EditorState.ActiveLayer.QueryFeatures(queryEnv)
                .Where(f => f.Geometry.Distance(new NetTopologySuite.Geometries.Point(worldPoint)) <= tolerance);
        }

        private IEnumerable<Feature> GetFeaturesInRect(Rect screenRect)
        {
            if (Context?.EditorState.ActiveLayer == null)
                return Enumerable.Empty<Feature>();

            // Convert screen rectangle to world envelope
            var topLeft = ScreenToWorld(screenRect.TopLeft);
            var bottomRight = ScreenToWorld(screenRect.BottomRight);
            var queryEnv = new Envelope(
                Math.Min(topLeft.X, bottomRight.X), Math.Max(topLeft.X, bottomRight.X),
                Math.Min(topLeft.Y, bottomRight.Y), Math.Max(topLeft.Y, bottomRight.Y));

            return Context.EditorState.ActiveLayer.QueryFeatures(queryEnv);
        }

        private void SelectAllFeatures()
        {
            if (Context?.EditorState.ActiveLayer == null)
                return;

            Context.EditorState.Selection.Clear();
            Context.EditorState.Selection.AddFeatures(Context.EditorState.ActiveLayer.Features);
        }

        private void DeleteSelectedFeatures()
        {
            var selection = Context?.EditorState.Selection;
            var activeLayer = Context?.EditorState.ActiveLayer;
            
            if (selection == null || activeLayer == null || selection.FeatureCount == 0)
                return;

            // Create a delete command for undo/redo
            var featuresToDelete = selection.Features.ToList();
            var deleteCommand = new DeleteFeaturesCommand(activeLayer, featuresToDelete);
            Context?.UndoRedoService?.Execute(deleteCommand);

            // Clear selection since features are deleted
            selection.Clear();
        }

        private void DrawSelectionHandles(IDrawContext drawContext)
        {
            var selection = Context?.EditorState.Selection;
            if (selection == null || selection.FeatureCount == 0)
                return;

            const double handleSize = 6.0;
            var handleColor = Avalonia.Media.Colors.Yellow;

            foreach (var feature in selection.Features)
            {
                if (feature.Geometry is Polygon poly)
                {
                    // Draw handles at vertices
                    DrawPolygonHandles(drawContext, poly, handleSize, handleColor);
                }
                else if (feature.Geometry is LineString line)
                {
                    // Draw handles at vertices
                    DrawLineStringHandles(drawContext, line, handleSize, handleColor);
                }
                else if (feature.Geometry is NetTopologySuite.Geometries.Point point)
                {
                    // Draw handle at point
                    var screenPoint = WorldToScreen(point.Coordinate);
                    drawContext.DrawCircle(screenPoint, handleSize, handleColor, filled: true);
                }
            }
        }

        private void DrawPolygonHandles(IDrawContext drawContext, Polygon poly, double handleSize, Avalonia.Media.Color handleColor)
        {
            // Exterior ring vertices
            var coords = poly.ExteriorRing.Coordinates;
            foreach (var coord in coords)
            {
                var screenPoint = WorldToScreen(coord);
                drawContext.DrawCircle(screenPoint, handleSize, handleColor, filled: true);
            }

            // Interior ring vertices
            for (int i = 0; i < poly.NumInteriorRings; i++)
            {
                var interiorCoords = poly.GetInteriorRingN(i).Coordinates;
                foreach (var coord in interiorCoords)
                {
                    var screenPoint = WorldToScreen(coord);
                    drawContext.DrawCircle(screenPoint, handleSize, handleColor, filled: true);
                }
            }
        }

        private void DrawLineStringHandles(IDrawContext drawContext, LineString line, double handleSize, Avalonia.Media.Color handleColor)
        {
            var coords = line.Coordinates;
            foreach (var coord in coords)
            {
                var screenPoint = WorldToScreen(coord);
                drawContext.DrawCircle(screenPoint, handleSize, handleColor, filled: true);
            }
        }

        private double GetScreenScale()
        {
            // Simple approximation - in a real implementation this would come from the render service
            return Context?.Document?.ZoomLevel ?? 1.0;
        }
    }

    /// <summary>
    /// Command to delete features with undo support
    /// </summary>
    public class DeleteFeaturesCommand : EditorCommandBase
    {
        private readonly IVectorLayer _layer;
        private readonly List<Feature> _features;

        public DeleteFeaturesCommand(IVectorLayer layer, List<Feature> features)
            : base($"Delete {features.Count} feature(s)")
        {
            _layer = layer;
            _features = new List<Feature>(features);
        }

        public override void Do()
        {
            foreach (var feature in _features)
            {
                _layer.RemoveFeature(feature);
            }
        }

        public override void Undo()
        {
            foreach (var feature in _features)
            {
                _layer.AddFeature(feature);
            }
        }
    }
}