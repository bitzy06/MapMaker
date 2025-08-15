using System;
using System.Collections.Generic;
using System.Linq;
using MapMaker.Models;
using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Prepared;

namespace MapMaker.Services
{
    /// <summary>
    /// Service for converting vector geometries to grid-based representation
    /// </summary>
    public interface IGeometryToGridConverter
    {
        /// <summary>
        /// Convert a feature with vector geometry to grid cells
        /// </summary>
        void ConvertFeatureToGrid(Feature feature, GridLayer gridLayer, GridConversionOptions? options = null);

        /// <summary>
        /// Convert a geometry to grid cells
        /// </summary>
        IEnumerable<GridCell> ConvertGeometryToGrid(Geometry geometry, GridSystem gridSystem, GridConversionOptions? options = null);

        /// <summary>
        /// Convert multiple features to a grid layer
        /// </summary>
        GridLayer ConvertFeaturesToGridLayer(IEnumerable<Feature> features, string layerName, GridSystem gridSystem, GridConversionOptions? options = null);
    }

    /// <summary>
    /// Options for geometry to grid conversion
    /// </summary>
    public class GridConversionOptions
    {
        /// <summary>
        /// Minimum coverage percentage for a cell to be considered occupied (0.0 to 1.0)
        /// </summary>
        public double MinCoverage { get; set; } = 0.01; // 1% minimum coverage

        /// <summary>
        /// Whether to calculate exact coverage or use simplified intersection test
        /// </summary>
        public bool CalculateExactCoverage { get; set; } = false;

        /// <summary>
        /// Whether to preserve attributes from the source feature
        /// </summary>
        public bool PreserveAttributes { get; set; } = true;

        /// <summary>
        /// Custom attribute mapping function
        /// </summary>
        public Func<Feature, Dictionary<string, object>>? AttributeMapper { get; set; }
    }

    /// <summary>
    /// Implementation of geometry to grid conversion service
    /// </summary>
    public class GeometryToGridConverter : IGeometryToGridConverter
    {
        public void ConvertFeatureToGrid(Feature feature, GridLayer gridLayer, GridConversionOptions? options = null)
        {
            options ??= new GridConversionOptions();
            
            if (feature.Geometry == null) return;

            var gridCells = ConvertGeometryToGrid(feature.Geometry, gridLayer.GridSystem, options);
            
            foreach (var cell in gridCells)
            {
                // Set source feature reference
                cell.SourceFeature = feature;
                
                // Copy attributes if requested
                if (options.PreserveAttributes)
                {
                    if (options.AttributeMapper != null)
                    {
                        var mappedAttributes = options.AttributeMapper(feature);
                        foreach (var kvp in mappedAttributes)
                        {
                            cell.SetAttribute(kvp.Key, kvp.Value);
                        }
                    }
                    else
                    {
                        // Copy all attributes from feature
                        foreach (var kvp in feature.Attributes)
                        {
                            cell.SetAttribute(kvp.Key, kvp.Value);
                        }
                    }
                }
                
                gridLayer.SetCell(cell.Coordinate, cell);
            }
        }

        public IEnumerable<GridCell> ConvertGeometryToGrid(Geometry geometry, GridSystem gridSystem, GridConversionOptions? options = null)
        {
            options ??= new GridConversionOptions();
            
            if (geometry == null || geometry.IsEmpty)
                yield break;

            // Prepare geometry for efficient intersection testing
            var preparedGeometry = PreparedGeometryFactory.Prepare(geometry);
            
            // Get all grid cells that might intersect with the geometry
            var envelope = geometry.EnvelopeInternal;
            var gridCells = gridSystem.GetGridCells(envelope);

            foreach (var gridCoord in gridCells)
            {
                var cellEnvelope = gridSystem.GetCellEnvelope(gridCoord);
                
                // Quick envelope intersection test
                if (!envelope.Intersects(cellEnvelope))
                    continue;

                // Create cell geometry for intersection testing
                var cellGeometry = CreateCellGeometry(cellEnvelope);
                
                // Test for intersection
                if (!preparedGeometry.Intersects(cellGeometry))
                    continue;

                var coverage = CalculateCoverage(geometry, cellGeometry, options.CalculateExactCoverage);
                
                // Only include cells that meet minimum coverage threshold
                if (coverage < options.MinCoverage)
                    continue;

                var gridCell = new GridCell(gridCoord)
                {
                    IsOccupied = true,
                    Coverage = coverage
                };

                yield return gridCell;
            }
        }

        public GridLayer ConvertFeaturesToGridLayer(IEnumerable<Feature> features, string layerName, GridSystem gridSystem, GridConversionOptions? options = null)
        {
            var gridLayer = new GridLayer(layerName, gridSystem);
            
            foreach (var feature in features)
            {
                ConvertFeatureToGrid(feature, gridLayer, options);
            }
            
            return gridLayer;
        }

        /// <summary>
        /// Create a polygon geometry representing a grid cell
        /// </summary>
        private Geometry CreateCellGeometry(Envelope cellEnvelope)
        {
            var factory = new GeometryFactory();
            var coords = new[]
            {
                new Coordinate(cellEnvelope.MinX, cellEnvelope.MinY),
                new Coordinate(cellEnvelope.MaxX, cellEnvelope.MinY),
                new Coordinate(cellEnvelope.MaxX, cellEnvelope.MaxY),
                new Coordinate(cellEnvelope.MinX, cellEnvelope.MaxY),
                new Coordinate(cellEnvelope.MinX, cellEnvelope.MinY) // Close the ring
            };
            
            return factory.CreatePolygon(coords);
        }

        /// <summary>
        /// Calculate the coverage percentage of a geometry within a cell
        /// </summary>
        private double CalculateCoverage(Geometry geometry, Geometry cellGeometry, bool exactCoverage)
        {
            if (!exactCoverage)
            {
                // Simple intersection test - return 1.0 if intersects, 0.0 otherwise
                return geometry.Intersects(cellGeometry) ? 1.0 : 0.0;
            }

            try
            {
                // Calculate exact coverage by finding intersection area
                var intersection = geometry.Intersection(cellGeometry);
                if (intersection.IsEmpty)
                    return 0.0;

                var cellArea = cellGeometry.Area;
                var intersectionArea = intersection.Area;
                
                return Math.Min(1.0, intersectionArea / cellArea);
            }
            catch (Exception)
            {
                // Fall back to simple intersection test on geometry errors
                return geometry.Intersects(cellGeometry) ? 1.0 : 0.0;
            }
        }
    }

    /// <summary>
    /// Extension methods for easier grid conversion
    /// </summary>
    public static class GridConversionExtensions
    {
        /// <summary>
        /// Convert a vector layer to a grid layer
        /// </summary>
        public static GridLayer ToGridLayer(this IVectorLayer vectorLayer, GridSystem gridSystem, GridConversionOptions? options = null)
        {
            var converter = new GeometryToGridConverter();
            return converter.ConvertFeaturesToGridLayer(vectorLayer.Features, vectorLayer.Name + "_Grid", gridSystem, options);
        }

        /// <summary>
        /// Convert a feature collection to a grid layer
        /// </summary>
        public static GridLayer ToGridLayer(this IEnumerable<Feature> features, string layerName, GridSystem gridSystem, GridConversionOptions? options = null)
        {
            var converter = new GeometryToGridConverter();
            return converter.ConvertFeaturesToGridLayer(features, layerName, gridSystem, options);
        }
    }
}