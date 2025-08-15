using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using MapMaker.Models;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Services
{
    /// <summary>
    /// Implementation of hit testing service for spatial queries
    /// </summary>
    public class HitTestService : IHitTestService
    {
        private readonly MapDocument _document;
        private readonly ICoordinateTransform _coordinateTransform;

        public HitTestService(MapDocument document, ICoordinateTransform coordinateTransform)
        {
            _document = document;
            _coordinateTransform = coordinateTransform;
        }

        public IEnumerable<Feature> HitTest(AvaloniaPoint screenPoint, double tolerance = 5.0)
        {
            var worldPoint = _coordinateTransform.Transform(screenPoint);
            var worldTolerance = tolerance / GetCurrentScale();
            
            var queryEnv = new Envelope(
                worldPoint.X - worldTolerance, worldPoint.X + worldTolerance,
                worldPoint.Y - worldTolerance, worldPoint.Y + worldTolerance);

            var results = new List<Feature>();
            foreach (var layer in _document.Layers.OfType<IVectorLayer>().Where(l => l.Visible))
            {
                var candidates = layer.QueryFeatures(queryEnv);
                var hits = candidates.Where(f => f.Geometry.Distance(new NetTopologySuite.Geometries.Point(worldPoint)) <= worldTolerance);
                results.AddRange(hits);
            }

            return results.OrderBy(f => f.Geometry.Distance(new NetTopologySuite.Geometries.Point(worldPoint)));
        }

        public IEnumerable<Feature> HitTest(Rect screenRect)
        {
            var topLeft = _coordinateTransform.Transform(screenRect.TopLeft);
            var bottomRight = _coordinateTransform.Transform(screenRect.BottomRight);
            
            var queryEnv = new Envelope(
                Math.Min(topLeft.X, bottomRight.X), Math.Max(topLeft.X, bottomRight.X),
                Math.Min(topLeft.Y, bottomRight.Y), Math.Max(topLeft.Y, bottomRight.Y));

            var results = new List<Feature>();
            foreach (var layer in _document.Layers.OfType<IVectorLayer>().Where(l => l.Visible))
            {
                results.AddRange(layer.QueryFeatures(queryEnv));
            }

            return results;
        }

        public (Feature feature, int vertexIndex, Coordinate vertex)? FindClosestVertex(AvaloniaPoint screenPoint, double tolerance = 10.0)
        {
            var worldPoint = _coordinateTransform.Transform(screenPoint);
            var worldTolerance = tolerance / GetCurrentScale();

            Feature? closestFeature = null;
            int closestVertexIndex = -1;
            Coordinate? closestVertex = null;
            double closestDistance = double.MaxValue;

            foreach (var layer in _document.Layers.OfType<IVectorLayer>().Where(l => l.Visible))
            {
                foreach (var feature in layer.Features)
                {
                    var vertices = GetGeometryVertices(feature.Geometry);
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        var distance = worldPoint.Distance(vertices[i]);
                        if (distance <= worldTolerance && distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestFeature = feature;
                            closestVertexIndex = i;
                            closestVertex = vertices[i];
                        }
                    }
                }
            }

            return closestFeature != null && closestVertex != null 
                ? (closestFeature, closestVertexIndex, closestVertex) 
                : null;
        }

        public (Feature feature, int edgeIndex, Coordinate closestPoint)? FindClosestEdge(AvaloniaPoint screenPoint, double tolerance = 10.0)
        {
            var worldPoint = _coordinateTransform.Transform(screenPoint);
            var worldTolerance = tolerance / GetCurrentScale();

            // Simplified implementation - find closest point on any line segment
            Feature? closestFeature = null;
            int closestEdgeIndex = -1;
            Coordinate? closestPoint = null;
            double closestDistance = double.MaxValue;

            foreach (var layer in _document.Layers.OfType<IVectorLayer>().Where(l => l.Visible))
            {
                foreach (var feature in layer.Features)
                {
                    var edges = GetGeometryEdges(feature.Geometry);
                    for (int i = 0; i < edges.Count; i++)
                    {
                        var edge = edges[i];
                        var closestOnEdge = GetClosestPointOnLineSegment(worldPoint, edge.p0, edge.p1);
                        var distance = worldPoint.Distance(closestOnEdge);
                        
                        if (distance <= worldTolerance && distance < closestDistance)
                        {
                            closestDistance = distance;
                            closestFeature = feature;
                            closestEdgeIndex = i;
                            closestPoint = closestOnEdge;
                        }
                    }
                }
            }

            return closestFeature != null && closestPoint != null 
                ? (closestFeature, closestEdgeIndex, closestPoint) 
                : null;
        }

        public IEnumerable<SnapTarget> FindSnapTargets(AvaloniaPoint screenPoint, SnapMode snapMode, double tolerance = 10.0)
        {
            var targets = new List<SnapTarget>();
            var worldPoint = _coordinateTransform.Transform(screenPoint);
            var worldTolerance = tolerance / GetCurrentScale();

            foreach (var layer in _document.Layers.OfType<IVectorLayer>().Where(l => l.Visible))
            {
                foreach (var feature in layer.Features)
                {
                    // Vertex snapping
                    if (snapMode.HasFlag(SnapMode.Vertex))
                    {
                        var vertices = GetGeometryVertices(feature.Geometry);
                        for (int i = 0; i < vertices.Count; i++)
                        {
                            if (worldPoint.Distance(vertices[i]) <= worldTolerance)
                            {
                                targets.Add(new SnapTarget(vertices[i], SnapType.Vertex, feature, i));
                            }
                        }
                    }

                    // Edge and midpoint snapping
                    if (snapMode.HasFlag(SnapMode.Edge) || snapMode.HasFlag(SnapMode.Midpoint))
                    {
                        var edges = GetGeometryEdges(feature.Geometry);
                        for (int i = 0; i < edges.Count; i++)
                        {
                            var edge = edges[i];
                            
                            if (snapMode.HasFlag(SnapMode.Edge))
                            {
                                var closestOnEdge = GetClosestPointOnLineSegment(worldPoint, edge.p0, edge.p1);
                                if (worldPoint.Distance(closestOnEdge) <= worldTolerance)
                                {
                                    targets.Add(new SnapTarget(closestOnEdge, SnapType.Edge, feature));
                                }
                            }

                            if (snapMode.HasFlag(SnapMode.Midpoint))
                            {
                                var midpoint = new Coordinate((edge.p0.X + edge.p1.X) / 2, (edge.p0.Y + edge.p1.Y) / 2);
                                if (worldPoint.Distance(midpoint) <= worldTolerance)
                                {
                                    targets.Add(new SnapTarget(midpoint, SnapType.EdgeMidpoint, feature));
                                }
                            }
                        }
                    }
                }
            }

            // Grid snapping
            if (snapMode.HasFlag(SnapMode.Grid) && _document.GridEnabled)
            {
                var gridX = Math.Round(worldPoint.X / _document.GridSize) * _document.GridSize;
                var gridY = Math.Round(worldPoint.Y / _document.GridSize) * _document.GridSize;
                var gridPoint = new Coordinate(gridX, gridY);
                
                if (worldPoint.Distance(gridPoint) <= worldTolerance)
                {
                    targets.Add(new SnapTarget(gridPoint, SnapType.Grid));
                }
            }

            return targets.OrderBy(t => worldPoint.Distance(t.Coordinate));
        }

        private double GetCurrentScale()
        {
            // Simple approximation - would be provided by render service in real implementation
            return _document.ZoomLevel;
        }

        private List<Coordinate> GetGeometryVertices(Geometry geometry)
        {
            var vertices = new List<Coordinate>();
            
            if (geometry is NetTopologySuite.Geometries.Point point)
            {
                vertices.Add(point.Coordinate);
            }
            else if (geometry is LineString lineString)
            {
                vertices.AddRange(lineString.Coordinates);
            }
            else if (geometry is Polygon polygon)
            {
                vertices.AddRange(polygon.ExteriorRing.Coordinates);
                for (int i = 0; i < polygon.NumInteriorRings; i++)
                {
                    vertices.AddRange(polygon.GetInteriorRingN(i).Coordinates);
                }
            }
            else if (geometry is GeometryCollection collection)
            {
                for (int i = 0; i < collection.NumGeometries; i++)
                {
                    vertices.AddRange(GetGeometryVertices(collection.GetGeometryN(i)));
                }
            }

            return vertices;
        }

        private List<(Coordinate p0, Coordinate p1)> GetGeometryEdges(Geometry geometry)
        {
            var edges = new List<(Coordinate p0, Coordinate p1)>();
            
            if (geometry is LineString lineString)
            {
                var coords = lineString.Coordinates;
                for (int i = 0; i < coords.Length - 1; i++)
                {
                    edges.Add((coords[i], coords[i + 1]));
                }
            }
            else if (geometry is Polygon polygon)
            {
                // Exterior ring
                var extCoords = polygon.ExteriorRing.Coordinates;
                for (int i = 0; i < extCoords.Length - 1; i++)
                {
                    edges.Add((extCoords[i], extCoords[i + 1]));
                }

                // Interior rings
                for (int ringIndex = 0; ringIndex < polygon.NumInteriorRings; ringIndex++)
                {
                    var intCoords = polygon.GetInteriorRingN(ringIndex).Coordinates;
                    for (int i = 0; i < intCoords.Length - 1; i++)
                    {
                        edges.Add((intCoords[i], intCoords[i + 1]));
                    }
                }
            }
            else if (geometry is GeometryCollection collection)
            {
                for (int i = 0; i < collection.NumGeometries; i++)
                {
                    edges.AddRange(GetGeometryEdges(collection.GetGeometryN(i)));
                }
            }

            return edges;
        }

        private Coordinate GetClosestPointOnLineSegment(Coordinate point, Coordinate lineStart, Coordinate lineEnd)
        {
            var A = point.X - lineStart.X;
            var B = point.Y - lineStart.Y;
            var C = lineEnd.X - lineStart.X;
            var D = lineEnd.Y - lineStart.Y;

            var dot = A * C + B * D;
            var lenSq = C * C + D * D;
            
            if (lenSq <= 0) return lineStart; // Line segment is a point

            var param = dot / lenSq;
            param = Math.Max(0, Math.Min(1, param)); // Clamp to line segment

            return new Coordinate(
                lineStart.X + param * C,
                lineStart.Y + param * D
            );
        }
    }

    /// <summary>
    /// Implementation of render service for coordinate transforms and viewport management
    /// </summary>
    public class RenderService : IRenderService
    {
        private CoordinateTransform? _worldToScreen;
        private CoordinateTransform? _screenToWorld;
        private Rect _screenBounds;
        private Envelope? _worldBounds;
        private double _zoomLevel = 1.0;
        private AvaloniaPoint _panOffset = new(0, 0);

        public ICoordinateTransform GetWorldToScreenTransform()
        {
            return _worldToScreen ?? new CoordinateTransform(
                new Envelope(0, 100, 0, 100), 
                new Rect(0, 0, 100, 100), 
                1.0, 
                new AvaloniaPoint(0, 0));
        }

        public ICoordinateTransform GetScreenToWorldTransform()
        {
            return _screenToWorld ?? new CoordinateTransform(
                new Envelope(0, 100, 0, 100), 
                new Rect(0, 0, 100, 100), 
                1.0, 
                new AvaloniaPoint(0, 0));
        }

        public void SetViewport(Rect screenBounds, Envelope worldBounds, double zoomLevel, AvaloniaPoint panOffset)
        {
            _screenBounds = screenBounds;
            _worldBounds = worldBounds;
            _zoomLevel = zoomLevel;
            _panOffset = panOffset;

            _worldToScreen = new CoordinateTransform(worldBounds, screenBounds, zoomLevel, panOffset);
            _screenToWorld = new CoordinateTransform(worldBounds, screenBounds, zoomLevel, panOffset);
        }

        public void InvalidateLayer(ILayer layer)
        {
            // In a full implementation, this would invalidate render cache for the layer
        }

        public void InvalidateArea(Rect screenArea)
        {
            // In a full implementation, this would invalidate render cache for the area
        }

        public Envelope GetWorldViewport()
        {
            return _worldBounds ?? new Envelope(0, 100, 0, 100);
        }

        public Rect GetScreenBounds()
        {
            return _screenBounds;
        }
    }

    /// <summary>
    /// Implementation of persistence service for document and shapefile I/O
    /// </summary>
    public class PersistenceService : IPersistenceService
    {
        public async Task<MapDocument> LoadDocumentAsync(string filePath)
        {
            // For now, create a new document
            // In full implementation, this would load from JSON project file
            await Task.Delay(1); // Simulate async operation
            return new MapDocument();
        }

        public async Task SaveDocumentAsync(MapDocument document, string filePath)
        {
            // For now, do nothing
            // In full implementation, this would save as JSON project file
            await Task.Delay(1); // Simulate async operation
        }

        public async Task<VectorLayer> LoadShapefileAsync(string shapefilePath, string layerName)
        {
            var layer = new VectorLayer(layerName);
            
            await Task.Run(() =>
            {
                var reader = new ShapefileReader(shapefilePath);
                var geometries = reader.ReadAll();
                
                for (int i = 0; i < geometries.Count; i++)
                {
                    var feature = new Feature($"feature_{i}", geometries[i]);
                    layer.AddFeature(feature);
                }
            });

            return layer;
        }

        public async Task SaveShapefileAsync(VectorLayer layer, string outputPath)
        {
            // Placeholder implementation - would need proper shapefile writing logic
            await Task.Delay(1);
            // TODO: Implement proper shapefile writing with attributes
        }

        public async Task ExportToOutputAsync(MapDocument document, string outputDirectory, string mapType)
        {
            // Simple implementation that exports all vector layers
            foreach (var layer in document.Layers.OfType<VectorLayer>())
            {
                var outputPath = System.IO.Path.Combine(outputDirectory, $"{mapType}_{layer.Name}.shp");
                await SaveShapefileAsync(layer, outputPath);
            }
        }
    }
}