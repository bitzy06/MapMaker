using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;
using Avalonia;

namespace MapMaker.Models
{
    /// <summary>
    /// Grid coordinate system with 10-meter unit resolution
    /// </summary>
    public class GridSystem
    {
        /// <summary>
        /// Grid cell size in world units (10 meters)
        /// </summary>
        public const double GRID_SIZE = 10.0;

        /// <summary>
        /// Origin point of the grid system
        /// </summary>
        public NetTopologySuite.Geometries.Point Origin { get; }

        public GridSystem(NetTopologySuite.Geometries.Point origin)
        {
            Origin = origin ?? new NetTopologySuite.Geometries.Point(0, 0);
        }

        /// <summary>
        /// Convert world coordinates to grid coordinates
        /// </summary>
        public GridCoordinate WorldToGrid(Coordinate worldCoord)
        {
            var x = (int)Math.Floor((worldCoord.X - Origin.X) / GRID_SIZE);
            var y = (int)Math.Floor((worldCoord.Y - Origin.Y) / GRID_SIZE);
            return new GridCoordinate(x, y);
        }

        /// <summary>
        /// Convert grid coordinates to world coordinates (cell center)
        /// </summary>
        public Coordinate GridToWorld(GridCoordinate gridCoord)
        {
            var x = Origin.X + (gridCoord.X + 0.5) * GRID_SIZE;
            var y = Origin.Y + (gridCoord.Y + 0.5) * GRID_SIZE;
            return new Coordinate(x, y);
        }

        /// <summary>
        /// Get the world envelope of a grid cell
        /// </summary>
        public Envelope GetCellEnvelope(GridCoordinate gridCoord)
        {
            var x = Origin.X + gridCoord.X * GRID_SIZE;
            var y = Origin.Y + gridCoord.Y * GRID_SIZE;
            return new Envelope(x, x + GRID_SIZE, y, y + GRID_SIZE);
        }

        /// <summary>
        /// Get all grid coordinates that intersect with the given envelope
        /// </summary>
        public IEnumerable<GridCoordinate> GetGridCells(Envelope envelope)
        {
            var minGridX = (int)Math.Floor((envelope.MinX - Origin.X) / GRID_SIZE);
            var maxGridX = (int)Math.Floor((envelope.MaxX - Origin.X) / GRID_SIZE);
            var minGridY = (int)Math.Floor((envelope.MinY - Origin.Y) / GRID_SIZE);
            var maxGridY = (int)Math.Floor((envelope.MaxY - Origin.Y) / GRID_SIZE);

            for (int x = minGridX; x <= maxGridX; x++)
            {
                for (int y = minGridY; y <= maxGridY; y++)
                {
                    yield return new GridCoordinate(x, y);
                }
            }
        }
    }

    /// <summary>
    /// Represents a coordinate in the grid system
    /// </summary>
    public struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public int X { get; }
        public int Y { get; }

        public GridCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public bool Equals(GridCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object? obj)
        {
            return obj is GridCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public static bool operator ==(GridCoordinate left, GridCoordinate right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GridCoordinate left, GridCoordinate right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }

    /// <summary>
    /// Represents a cell in the grid with associated data
    /// </summary>
    public class GridCell
    {
        public GridCoordinate Coordinate { get; }
        public Dictionary<string, object> Attributes { get; }
        public bool IsOccupied { get; set; }
        
        /// <summary>
        /// Coverage percentage (0.0 to 1.0) indicating how much of the cell is covered by geometry
        /// </summary>
        public double Coverage { get; set; }

        /// <summary>
        /// Reference to the original feature that occupies this cell (if any)
        /// </summary>
        public Feature? SourceFeature { get; set; }

        public GridCell(GridCoordinate coordinate)
        {
            Coordinate = coordinate;
            Attributes = new Dictionary<string, object>();
            IsOccupied = false;
            Coverage = 0.0;
        }

        public T? GetAttribute<T>(string key, T? defaultValue = default)
        {
            if (Attributes.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return defaultValue;
        }

        public void SetAttribute(string key, object value)
        {
            Attributes[key] = value;
        }
    }

    /// <summary>
    /// Grid-based data structure for storing spatial information
    /// </summary>
    public class GridData
    {
        private readonly Dictionary<GridCoordinate, GridCell> _cells;
        private readonly GridSystem _gridSystem;

        public GridSystem GridSystem => _gridSystem;
        public IReadOnlyDictionary<GridCoordinate, GridCell> Cells => _cells;
        public Envelope? Bounds { get; private set; }

        public GridData(GridSystem gridSystem)
        {
            _gridSystem = gridSystem;
            _cells = new Dictionary<GridCoordinate, GridCell>();
        }

        /// <summary>
        /// Add or update a grid cell
        /// </summary>
        public void SetCell(GridCoordinate coordinate, GridCell cell)
        {
            _cells[coordinate] = cell;
            UpdateBounds(coordinate);
        }

        /// <summary>
        /// Get a grid cell at the specified coordinate
        /// </summary>
        public GridCell? GetCell(GridCoordinate coordinate)
        {
            return _cells.TryGetValue(coordinate, out var cell) ? cell : null;
        }

        /// <summary>
        /// Get or create a grid cell at the specified coordinate
        /// </summary>
        public GridCell GetOrCreateCell(GridCoordinate coordinate)
        {
            if (!_cells.TryGetValue(coordinate, out var cell))
            {
                cell = new GridCell(coordinate);
                _cells[coordinate] = cell;
                UpdateBounds(coordinate);
            }
            return cell;
        }

        /// <summary>
        /// Remove a grid cell
        /// </summary>
        public bool RemoveCell(GridCoordinate coordinate)
        {
            var removed = _cells.Remove(coordinate);
            if (removed)
            {
                RecalculateBounds();
            }
            return removed;
        }

        /// <summary>
        /// Get all occupied cells (cells with IsOccupied = true)
        /// </summary>
        public IEnumerable<GridCell> GetOccupiedCells()
        {
            return _cells.Values.Where(c => c.IsOccupied);
        }

        /// <summary>
        /// Query cells within the given envelope
        /// </summary>
        public IEnumerable<GridCell> QueryCells(Envelope envelope)
        {
            var gridCoords = _gridSystem.GetGridCells(envelope);
            foreach (var coord in gridCoords)
            {
                if (_cells.TryGetValue(coord, out var cell))
                {
                    yield return cell;
                }
            }
        }

        /// <summary>
        /// Clear all cells
        /// </summary>
        public void Clear()
        {
            _cells.Clear();
            Bounds = null;
        }

        /// <summary>
        /// Get statistics about the grid data
        /// </summary>
        public GridDataStats GetStats()
        {
            return new GridDataStats
            {
                TotalCells = _cells.Count,
                OccupiedCells = _cells.Values.Count(c => c.IsOccupied),
                AverageCoverage = _cells.Values.Count > 0 ? _cells.Values.Average(c => c.Coverage) : 0,
                Bounds = Bounds
            };
        }

        private void UpdateBounds(GridCoordinate coordinate)
        {
            var cellEnvelope = _gridSystem.GetCellEnvelope(coordinate);
            if (Bounds == null)
            {
                Bounds = new Envelope(cellEnvelope);
            }
            else
            {
                Bounds.ExpandToInclude(cellEnvelope);
            }
        }

        private void RecalculateBounds()
        {
            Bounds = null;
            foreach (var coord in _cells.Keys)
            {
                UpdateBounds(coord);
            }
        }
    }

    /// <summary>
    /// Statistics about grid data
    /// </summary>
    public class GridDataStats
    {
        public int TotalCells { get; set; }
        public int OccupiedCells { get; set; }
        public double AverageCoverage { get; set; }
        public Envelope? Bounds { get; set; }

        public override string ToString()
        {
            return $"Grid Stats: {TotalCells} total cells, {OccupiedCells} occupied ({(OccupiedCells * 100.0 / Math.Max(1, TotalCells)):F1}%), avg coverage: {AverageCoverage:F2}";
        }
    }
}