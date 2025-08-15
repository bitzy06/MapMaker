using System;
using System.Collections.Generic;
using Avalonia;
using NetTopologySuite.Geometries;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Models
{
    /// <summary>
    /// Represents a map project document containing layers, projection settings, and view state
    /// </summary>
    public class MapDocument
    {
        public MapDocument()
        {
            Layers = new List<ILayer>();
            ProjectionInfo = "EPSG:4326"; // Default to WGS84
            ZoomLevel = 1.0;
            PanOffset = new AvaloniaPoint(0, 0);
            GridEnabled = false;
            SnapEnabled = true;
        }

        /// <summary>
        /// List of layers in the document, ordered from bottom to top
        /// </summary>
        public List<ILayer> Layers { get; set; }

        /// <summary>
        /// Coordinate reference system / projection information
        /// </summary>
        public string ProjectionInfo { get; set; }

        /// <summary>
        /// Current zoom level of the view
        /// </summary>
        public double ZoomLevel { get; set; }

        /// <summary>
        /// Current pan offset of the view
        /// </summary>
        public AvaloniaPoint PanOffset { get; set; }

        /// <summary>
        /// Whether grid is enabled for snapping and display
        /// </summary>
        public bool GridEnabled { get; set; }

        /// <summary>
        /// Grid size in world units
        /// </summary>
        public double GridSize { get; set; } = 1.0;

        /// <summary>
        /// Whether snapping is enabled
        /// </summary>
        public bool SnapEnabled { get; set; }

        /// <summary>
        /// Snapping tolerance in pixels
        /// </summary>
        public double SnapTolerance { get; set; } = 10.0;

        /// <summary>
        /// Document bounds encompassing all layers
        /// </summary>
        public Envelope? Bounds { get; set; }

        /// <summary>
        /// Add a layer to the document
        /// </summary>
        public void AddLayer(ILayer layer)
        {
            Layers.Add(layer);
            UpdateBounds();
        }

        /// <summary>
        /// Remove a layer from the document
        /// </summary>
        public bool RemoveLayer(ILayer layer)
        {
            var result = Layers.Remove(layer);
            if (result)
            {
                UpdateBounds();
            }
            return result;
        }

        /// <summary>
        /// Move a layer to a new position in the layer order
        /// </summary>
        public void MoveLayer(ILayer layer, int newIndex)
        {
            if (Layers.Remove(layer))
            {
                Layers.Insert(newIndex, layer);
            }
        }

        /// <summary>
        /// Update document bounds based on all layers
        /// </summary>
        private void UpdateBounds()
        {
            Bounds = null;
            foreach (var layer in Layers)
            {
                if (layer.Bounds != null)
                {
                    if (Bounds == null)
                    {
                        Bounds = new Envelope(layer.Bounds);
                    }
                    else
                    {
                        Bounds.ExpandToInclude(layer.Bounds);
                    }
                }
            }
        }
    }
}