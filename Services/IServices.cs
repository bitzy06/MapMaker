using Avalonia;
using MapMaker.Models;
using NetTopologySuite.Geometries;
using System.Collections.Generic;
using System.Threading.Tasks;
using AvaloniaPoint = Avalonia.Point;
using NTSGeometry = NetTopologySuite.Geometries.Geometry;

namespace MapMaker.Services
{
    /// <summary>
    /// Service for coordinate transformations between world and screen space
    /// </summary>
    public interface ICoordinateTransform
    {
        /// <summary>
        /// Transform a world coordinate to screen point
        /// </summary>
        AvaloniaPoint Transform(Coordinate worldCoord);

        /// <summary>
        /// Transform a screen point to world coordinate
        /// </summary>
        Coordinate Transform(AvaloniaPoint screenPoint);
    }

    /// <summary>
    /// Service for geometry operations using NetTopologySuite
    /// </summary>
    public interface IGeometryService
    {
        /// <summary>
        /// Split a geometry with a line
        /// </summary>
        Geometry[] Split(Geometry geometry, LineString splitter);

        /// <summary>
        /// Merge multiple geometries into one
        /// </summary>
        Geometry Merge(params Geometry[] geometries);

        /// <summary>
        /// Create a buffer around a geometry
        /// </summary>
        Geometry Buffer(Geometry geometry, double distance);

        /// <summary>
        /// Simplify a geometry by removing unnecessary vertices
        /// </summary>
        Geometry Simplify(Geometry geometry, double tolerance);

        /// <summary>
        /// Test if a geometry is valid
        /// </summary>
        bool IsValid(Geometry geometry);

        /// <summary>
        /// Fix invalid geometry
        /// </summary>
        Geometry MakeValid(Geometry geometry);

        /// <summary>
        /// Get the intersection of two geometries
        /// </summary>
        Geometry Intersection(Geometry a, Geometry b);

        /// <summary>
        /// Get the union of two geometries
        /// </summary>
        Geometry Union(Geometry a, Geometry b);

        /// <summary>
        /// Get the difference between two geometries
        /// </summary>
        Geometry Difference(Geometry a, Geometry b);
    }

    /// <summary>
    /// Service for hit testing and spatial queries
    /// </summary>
    public interface IHitTestService
    {
        /// <summary>
        /// Find features at a screen point
        /// </summary>
        IEnumerable<Feature> HitTest(AvaloniaPoint screenPoint, double tolerance = 5.0);

        /// <summary>
        /// Find features within a screen rectangle
        /// </summary>
        IEnumerable<Feature> HitTest(Rect screenRect);

        /// <summary>
        /// Find the closest vertex to a screen point
        /// </summary>
        (Feature feature, int vertexIndex, Coordinate vertex)? FindClosestVertex(AvaloniaPoint screenPoint, double tolerance = 10.0);

        /// <summary>
        /// Find the closest edge to a screen point
        /// </summary>
        (Feature feature, int edgeIndex, Coordinate closestPoint)? FindClosestEdge(AvaloniaPoint screenPoint, double tolerance = 10.0);

        /// <summary>
        /// Find snap targets near a screen point
        /// </summary>
        IEnumerable<SnapTarget> FindSnapTargets(AvaloniaPoint screenPoint, SnapMode snapMode, double tolerance = 10.0);
    }

    /// <summary>
    /// Service for undo/redo operations using the Command pattern
    /// </summary>
    public interface IUndoRedoService
    {
        /// <summary>
        /// Whether undo is available
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Whether redo is available
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// Execute a command and add it to the undo stack
        /// </summary>
        void Execute(IEditorCommand command);

        /// <summary>
        /// Undo the last command
        /// </summary>
        void Undo();

        /// <summary>
        /// Redo the last undone command
        /// </summary>
        void Redo();

        /// <summary>
        /// Clear the command history
        /// </summary>
        void Clear();

        /// <summary>
        /// Get the description of the next undo command
        /// </summary>
        string? GetUndoDescription();

        /// <summary>
        /// Get the description of the next redo command
        /// </summary>
        string? GetRedoDescription();
    }

    /// <summary>
    /// Service for loading and saving map documents and shapefiles
    /// </summary>
    public interface IPersistenceService
    {
        /// <summary>
        /// Load a map document from file
        /// </summary>
        Task<MapDocument> LoadDocumentAsync(string filePath);

        /// <summary>
        /// Save a map document to file
        /// </summary>
        Task SaveDocumentAsync(MapDocument document, string filePath);

        /// <summary>
        /// Load shapefile into a vector layer
        /// </summary>
        Task<VectorLayer> LoadShapefileAsync(string shapefilePath, string layerName);

        /// <summary>
        /// Save a vector layer as shapefile
        /// </summary>
        Task SaveShapefileAsync(VectorLayer layer, string outputPath);

        /// <summary>
        /// Export current map data to output directory
        /// </summary>
        Task ExportToOutputAsync(MapDocument document, string outputDirectory, string mapType);
    }

    /// <summary>
    /// Service for rendering operations and coordinate transforms
    /// </summary>
    public interface IRenderService
    {
        /// <summary>
        /// Get the current world-to-screen transform
        /// </summary>
        ICoordinateTransform GetWorldToScreenTransform();

        /// <summary>
        /// Get the current screen-to-world transform
        /// </summary>
        ICoordinateTransform GetScreenToWorldTransform();

        /// <summary>
        /// Set the viewport bounds and zoom level
        /// </summary>
        void SetViewport(Rect screenBounds, Envelope worldBounds, double zoomLevel, AvaloniaPoint panOffset);

        /// <summary>
        /// Invalidate the render cache for a layer
        /// </summary>
        void InvalidateLayer(ILayer layer);

        /// <summary>
        /// Invalidate the render cache for a rectangular area
        /// </summary>
        void InvalidateArea(Rect screenArea);

        /// <summary>
        /// Get the current viewport bounds in world coordinates
        /// </summary>
        Envelope GetWorldViewport();

        /// <summary>
        /// Get the current screen bounds
        /// </summary>
        Rect GetScreenBounds();
    }

    /// <summary>
    /// Interface for editor commands in the undo/redo system
    /// </summary>
    public interface IEditorCommand
    {
        /// <summary>
        /// Description of the command for UI display
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Execute the command
        /// </summary>
        void Do();

        /// <summary>
        /// Undo the command
        /// </summary>
        void Undo();
    }
}