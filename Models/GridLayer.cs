using System;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite.Geometries;

namespace MapMaker.Models
{
    /// <summary>
    /// Interface for grid-based layers
    /// </summary>
    public interface IGridLayer : ILayer
    {
        GridData GridData { get; }
        GridSystem GridSystem { get; }
        IEnumerable<GridCell> QueryCells(Envelope envelope);
        GridCell? GetCell(GridCoordinate coordinate);
    }

    /// <summary>
    /// Layer implementation that stores spatial data in a grid format
    /// </summary>
    public class GridLayer : LayerBase, IGridLayer
    {
        private readonly GridData _gridData;

        public GridLayer(string name, GridSystem gridSystem) : base(name)
        {
            _gridData = new GridData(gridSystem);
        }

        public GridData GridData => _gridData;
        public GridSystem GridSystem => _gridData.GridSystem;

        public override Envelope? Bounds => _gridData.Bounds;

        public IEnumerable<GridCell> QueryCells(Envelope envelope)
        {
            return _gridData.QueryCells(envelope);
        }

        public GridCell? GetCell(GridCoordinate coordinate)
        {
            return _gridData.GetCell(coordinate);
        }

        public GridCell GetOrCreateCell(GridCoordinate coordinate)
        {
            return _gridData.GetOrCreateCell(coordinate);
        }

        public void SetCell(GridCoordinate coordinate, GridCell cell)
        {
            _gridData.SetCell(coordinate, cell);
        }

        public bool RemoveCell(GridCoordinate coordinate)
        {
            return _gridData.RemoveCell(coordinate);
        }

        public void ClearCells()
        {
            _gridData.Clear();
        }

        public GridDataStats GetStats()
        {
            return _gridData.GetStats();
        }

        /// <summary>
        /// Get all occupied cells as an enumerable
        /// </summary>
        public IEnumerable<GridCell> GetOccupiedCells()
        {
            return _gridData.GetOccupiedCells();
        }
    }
}