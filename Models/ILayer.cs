using NetTopologySuite.Geometries;
using System.Collections.Generic;

namespace MapMaker.Models
{
    /// <summary>
    /// Base interface for all layers in a MapDocument
    /// </summary>
    public interface ILayer
    {
        /// <summary>
        /// Unique identifier for the layer
        /// </summary>
        string Id { get; }

        /// <summary>
        /// Display name of the layer
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Whether the layer is visible
        /// </summary>
        bool Visible { get; set; }

        /// <summary>
        /// Whether the layer is locked for editing
        /// </summary>
        bool Locked { get; set; }

        /// <summary>
        /// Layer opacity (0.0 to 1.0)
        /// </summary>
        double Opacity { get; set; }

        /// <summary>
        /// Spatial bounds of the layer
        /// </summary>
        Envelope? Bounds { get; }

        /// <summary>
        /// Z-order for rendering (higher values render on top)
        /// </summary>
        int ZOrder { get; set; }
    }

    /// <summary>
    /// Layer containing vector features (polygons, lines, points)
    /// </summary>
    public interface IVectorLayer : ILayer
    {
        /// <summary>
        /// Features contained in this layer
        /// </summary>
        IList<Feature> Features { get; }

        /// <summary>
        /// Style settings for rendering features
        /// </summary>
        LayerStyle Style { get; set; }

        /// <summary>
        /// Spatial index for efficient querying (R-tree/QuadTree)
        /// </summary>
        ISpatialIndex<Feature> SpatialIndex { get; }

        /// <summary>
        /// Add a feature to the layer
        /// </summary>
        void AddFeature(Feature feature);

        /// <summary>
        /// Remove a feature from the layer
        /// </summary>
        bool RemoveFeature(Feature feature);

        /// <summary>
        /// Get features within a bounding envelope
        /// </summary>
        IEnumerable<Feature> QueryFeatures(Envelope envelope);

        /// <summary>
        /// Get features that intersect with a geometry
        /// </summary>
        IEnumerable<Feature> QueryFeatures(Geometry geometry);

        /// <summary>
        /// Clear all features from the layer
        /// </summary>
        void ClearFeatures();
    }

    /// <summary>
    /// Layer for transient drawing elements (selection marquee, gizmos, handles)
    /// </summary>
    public interface IOverlayLayer : ILayer
    {
        /// <summary>
        /// Overlay elements for transient display
        /// </summary>
        IList<OverlayElement> Elements { get; }

        /// <summary>
        /// Add an overlay element
        /// </summary>
        void AddElement(OverlayElement element);

        /// <summary>
        /// Remove an overlay element
        /// </summary>
        bool RemoveElement(OverlayElement element);

        /// <summary>
        /// Clear all overlay elements
        /// </summary>
        void ClearElements();
    }
}