using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using MapMaker.Models;
using NetTopologySuite.Geometries;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Services
{
    /// <summary>
    /// Service for rendering grid-based data to Avalonia canvas
    /// </summary>
    public interface IGridRenderer
    {
        /// <summary>
        /// Render a grid layer to the specified canvas
        /// </summary>
        void RenderGridLayer(GridLayer gridLayer, Canvas canvas, GridRenderOptions? options = null);

        /// <summary>
        /// Render individual grid cells to the canvas
        /// </summary>
        void RenderGridCells(IEnumerable<GridCell> cells, GridSystem gridSystem, Canvas canvas, GridRenderOptions? options = null);

        /// <summary>
        /// Clear all grid-rendered elements from the canvas
        /// </summary>
        void ClearGridFromCanvas(Canvas canvas);

        /// <summary>
        /// Update the world-to-screen transform used for rendering
        /// </summary>
        void UpdateTransform(ICoordinateTransform coordinateTransform);
    }

    /// <summary>
    /// Options for grid rendering
    /// </summary>
    public class GridRenderOptions
    {
        /// <summary>
        /// Fill brush for occupied cells
        /// </summary>
        public IBrush? Fill { get; set; } = new SolidColorBrush(Colors.Blue, 0.7);

        /// <summary>
        /// Stroke brush for cell borders
        /// </summary>
        public IBrush? Stroke { get; set; } = new SolidColorBrush(Colors.DarkBlue);

        /// <summary>
        /// Stroke thickness for cell borders
        /// </summary>
        public double StrokeThickness { get; set; } = 0.5;

        /// <summary>
        /// Whether to show cell borders
        /// </summary>
        public bool ShowCellBorders { get; set; } = true;

        /// <summary>
        /// Whether to show only occupied cells or all cells
        /// </summary>
        public bool ShowOnlyOccupied { get; set; } = true;

        /// <summary>
        /// Minimum zoom level to show individual cells (prevents performance issues at low zoom)
        /// </summary>
        public double MinZoomForCells { get; set; } = 0.1;

        /// <summary>
        /// Minimum screen pixels per cell before switching to block rendering
        /// </summary>
        public double MinScreenPixelsPerCell { get; set; } = 2.0;

        /// <summary>
        /// Custom fill brush function based on cell properties
        /// </summary>
        public Func<GridCell, IBrush?>? CellFillFunction { get; set; }

        /// <summary>
        /// Custom stroke brush function based on cell properties
        /// </summary>
        public Func<GridCell, IBrush?>? CellStrokeFunction { get; set; }

        /// <summary>
        /// Whether to use coverage-based alpha blending
        /// </summary>
        public bool UseCoverageAlpha { get; set; } = true;
    }

    /// <summary>
    /// Implementation of grid renderer service
    /// </summary>
    public class GridRenderer : IGridRenderer
    {
        private ICoordinateTransform? _coordinateTransform;
        private double _currentZoomLevel = 1.0;

        public void UpdateTransform(ICoordinateTransform coordinateTransform)
        {
            _coordinateTransform = coordinateTransform;
            
            // Try to extract zoom level if the transform supports it
            if (coordinateTransform is CoordinateTransform ct)
            {
                _currentZoomLevel = ct.ZoomLevel;
            }
        }

        public void RenderGridLayer(GridLayer gridLayer, Canvas canvas, GridRenderOptions? options = null)
        {
            if (_coordinateTransform == null)
                throw new InvalidOperationException("Coordinate transform must be set before rendering");

            options ??= new GridRenderOptions();

            // Update grid system resolution based on current zoom level
            gridLayer.GridSystem.UpdateGridResolution(_currentZoomLevel);

            // Regenerate grid data with new resolution if needed
            RegenerateGridDataForZoomLevel(gridLayer);

            // Check if zoom level is sufficient to show individual cells
            var minScreenCellSize = CalculateMinimumScreenCellSize(gridLayer.GridSystem);
            if (minScreenCellSize < options.MinScreenPixelsPerCell)
            {
                RenderGridLayerAsBlocks(gridLayer, canvas, options);
                return;
            }

            var cellsToRender = options.ShowOnlyOccupied 
                ? gridLayer.GetOccupiedCells() 
                : gridLayer.GridData.Cells.Values;

            RenderGridCells(cellsToRender, gridLayer.GridSystem, canvas, options);
        }

        public void RenderGridCells(IEnumerable<GridCell> cells, GridSystem gridSystem, Canvas canvas, GridRenderOptions? options = null)
        {
            if (_coordinateTransform == null)
                throw new InvalidOperationException("Coordinate transform must be set before rendering");

            options ??= new GridRenderOptions();

            foreach (var cell in cells)
            {
                if (!options.ShowOnlyOccupied || cell.IsOccupied)
                {
                    RenderGridCell(cell, gridSystem, canvas, options);
                }
            }
        }

        public void ClearGridFromCanvas(Canvas canvas)
        {
            // Remove all elements that have the grid marker tag
            var gridElements = canvas.Children.OfType<Shape>()
                .Where(s => s.Tag?.ToString() == "GridCell")
                .ToList();

            foreach (var element in gridElements)
            {
                canvas.Children.Remove(element);
            }
        }

        /// <summary>
        /// Render a single grid cell to the canvas
        /// </summary>
        private void RenderGridCell(GridCell cell, GridSystem gridSystem, Canvas canvas, GridRenderOptions options)
        {
            var cellEnvelope = gridSystem.GetCellEnvelope(cell.Coordinate);
            
            // Convert world coordinates to screen coordinates
            var topLeft = _coordinateTransform!.Transform(new Coordinate(cellEnvelope.MinX, cellEnvelope.MaxY));
            var bottomRight = _coordinateTransform.Transform(new Coordinate(cellEnvelope.MaxX, cellEnvelope.MinY));

            var screenRect = new Rect(topLeft, bottomRight);
            
            // Skip cells that are too small to see
            if (screenRect.Width < 1 || screenRect.Height < 1)
                return;

            var rectangle = new Rectangle
            {
                Width = Math.Max(1, screenRect.Width),
                Height = Math.Max(1, screenRect.Height),
                Tag = "GridCell"
            };

            // Set fill brush
            var fillBrush = options.CellFillFunction?.Invoke(cell) ?? options.Fill;
            if (fillBrush != null && options.UseCoverageAlpha)
            {
                fillBrush = AdjustBrushOpacity(fillBrush, cell.Coverage);
            }
            rectangle.Fill = fillBrush;

            // Set stroke brush
            if (options.ShowCellBorders)
            {
                var strokeBrush = options.CellStrokeFunction?.Invoke(cell) ?? options.Stroke;
                rectangle.Stroke = strokeBrush;
                rectangle.StrokeThickness = options.StrokeThickness;
            }

            // Position the rectangle
            Canvas.SetLeft(rectangle, screenRect.Left);
            Canvas.SetTop(rectangle, screenRect.Top);

            canvas.Children.Add(rectangle);
        }

        /// <summary>
        /// Render grid layer as larger blocks when zoomed out (for performance)
        /// </summary>
        private void RenderGridLayerAsBlocks(GridLayer gridLayer, Canvas canvas, GridRenderOptions options)
        {
            // Group nearby cells into larger blocks for efficient rendering at low zoom
            var occupiedCells = gridLayer.GetOccupiedCells().ToList();
            if (!occupiedCells.Any()) return;

            // Calculate block size based on zoom level
            var blockSize = Math.Max(1, (int)(options.MinZoomForCells / _currentZoomLevel));
            
            var blocks = GroupCellsIntoBlocks(occupiedCells, blockSize);
            
            foreach (var block in blocks)
            {
                RenderCellBlock(block, gridLayer.GridSystem, canvas, options);
            }
        }

        /// <summary>
        /// Calculate the minimum screen size in pixels for a grid cell at the current zoom level
        /// </summary>
        private double CalculateMinimumScreenCellSize(GridSystem gridSystem)
        {
            if (_coordinateTransform == null) return 0;

            // Create a sample cell at origin to measure its screen size
            var cellEnvelope = gridSystem.GetCellEnvelope(new GridCoordinate(0, 0));
            var topLeft = _coordinateTransform.Transform(new Coordinate(cellEnvelope.MinX, cellEnvelope.MaxY));
            var bottomRight = _coordinateTransform.Transform(new Coordinate(cellEnvelope.MaxX, cellEnvelope.MinY));

            var width = Math.Abs(bottomRight.X - topLeft.X);
            var height = Math.Abs(bottomRight.Y - topLeft.Y);
            
            return Math.Min(width, height);
        }

        /// <summary>
        /// Regenerate grid data with new resolution level if needed
        /// </summary>
        private void RegenerateGridDataForZoomLevel(GridLayer gridLayer)
        {
            // For now, we'll keep the original grid data from base resolution
            // In a more sophisticated implementation, we could regenerate the grid
            // data at different resolutions, but this would require caching the original features
            
            // This is a placeholder for future enhancement where we might want to
            // subdivide or aggregate grid cells based on zoom level
        }

        /// <summary>
        /// Group cells into larger blocks for efficient rendering
        /// </summary>
        private List<CellBlock> GroupCellsIntoBlocks(IEnumerable<GridCell> cells, int blockSize)
        {
            var blockMap = new Dictionary<GridCoordinate, CellBlock>();

            foreach (var cell in cells)
            {
                var blockCoord = new GridCoordinate(
                    cell.Coordinate.X / blockSize * blockSize,
                    cell.Coordinate.Y / blockSize * blockSize
                );

                if (!blockMap.TryGetValue(blockCoord, out var block))
                {
                    block = new CellBlock(blockCoord, blockSize);
                    blockMap[blockCoord] = block;
                }

                block.AddCell(cell);
            }

            return blockMap.Values.ToList();
        }

        /// <summary>
        /// Render a block of cells as a single shape
        /// </summary>
        private void RenderCellBlock(CellBlock block, GridSystem gridSystem, Canvas canvas, GridRenderOptions options)
        {
            var blockEnvelope = new Envelope(
                gridSystem.Origin.X + block.Coordinate.X * gridSystem.EffectiveGridSize,
                gridSystem.Origin.X + (block.Coordinate.X + block.Size) * gridSystem.EffectiveGridSize,
                gridSystem.Origin.Y + block.Coordinate.Y * gridSystem.EffectiveGridSize,
                gridSystem.Origin.Y + (block.Coordinate.Y + block.Size) * gridSystem.EffectiveGridSize
            );

            var topLeft = _coordinateTransform!.Transform(new Coordinate(blockEnvelope.MinX, blockEnvelope.MaxY));
            var bottomRight = _coordinateTransform.Transform(new Coordinate(blockEnvelope.MaxX, blockEnvelope.MinY));

            var screenRect = new Rect(topLeft, bottomRight);

            var rectangle = new Rectangle
            {
                Width = Math.Max(1, screenRect.Width),
                Height = Math.Max(1, screenRect.Height),
                Fill = options.Fill,
                Stroke = options.ShowCellBorders ? options.Stroke : null,
                StrokeThickness = options.StrokeThickness,
                Tag = "GridBlock"
            };

            Canvas.SetLeft(rectangle, screenRect.Left);
            Canvas.SetTop(rectangle, screenRect.Top);

            canvas.Children.Add(rectangle);
        }

        /// <summary>
        /// Adjust brush opacity based on coverage
        /// </summary>
        private IBrush AdjustBrushOpacity(IBrush brush, double coverage)
        {
            if (brush is SolidColorBrush solidBrush)
            {
                var color = solidBrush.Color;
                var newAlpha = (byte)(color.A * coverage);
                return new SolidColorBrush(Color.FromArgb(newAlpha, color.R, color.G, color.B));
            }
            return brush;
        }
    }

    /// <summary>
    /// Represents a block of grid cells for efficient rendering
    /// </summary>
    internal class CellBlock
    {
        public GridCoordinate Coordinate { get; }
        public int Size { get; }
        public List<GridCell> Cells { get; }

        public CellBlock(GridCoordinate coordinate, int size)
        {
            Coordinate = coordinate;
            Size = size;
            Cells = new List<GridCell>();
        }

        public void AddCell(GridCell cell)
        {
            Cells.Add(cell);
        }
    }

    /// <summary>
    /// Extension methods for coordinate conversion
    /// </summary>
    internal static class CoordinateExtensions
    {
        public static AvaloniaPoint ToAvalonia(this Coordinate coord)
        {
            return new AvaloniaPoint(coord.X, coord.Y);
        }
    }
}