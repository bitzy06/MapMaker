using Avalonia.Media;
using NetTopologySuite.Geometries;
using NTSGeometry = NetTopologySuite.Geometries.Geometry;

namespace MapMaker.Models
{
    /// <summary>
    /// Style settings for rendering vector layers
    /// </summary>
    public class LayerStyle
    {
        public LayerStyle()
        {
            FillColor = Colors.LightGreen;
            FillOpacity = 0.6;
            StrokeColor = Colors.DarkGreen;
            StrokeWidth = 1.0;
            PointSize = 4.0;
            PointColor = Colors.Red;
        }

        /// <summary>
        /// Fill color for polygons
        /// </summary>
        public Color FillColor { get; set; }

        /// <summary>
        /// Fill opacity (0.0 to 1.0)
        /// </summary>
        public double FillOpacity { get; set; }

        /// <summary>
        /// Stroke color for lines and polygon outlines
        /// </summary>
        public Color StrokeColor { get; set; }

        /// <summary>
        /// Stroke width in pixels
        /// </summary>
        public double StrokeWidth { get; set; }

        /// <summary>
        /// Point size in pixels
        /// </summary>
        public double PointSize { get; set; }

        /// <summary>
        /// Point color
        /// </summary>
        public Color PointColor { get; set; }

        /// <summary>
        /// Selection highlight color
        /// </summary>
        public Color SelectionColor { get; set; } = Colors.Yellow;

        /// <summary>
        /// Hover highlight color
        /// </summary>
        public Color HoverColor { get; set; } = Colors.Orange;
    }

    /// <summary>
    /// Represents an overlay element for transient drawing
    /// </summary>
    public abstract class OverlayElement
    {
        protected OverlayElement(string id)
        {
            Id = id;
        }

        /// <summary>
        /// Unique identifier for the overlay element
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Whether the element is visible
        /// </summary>
        public bool Visible { get; set; } = true;

        /// <summary>
        /// Z-order for rendering overlay elements
        /// </summary>
        public int ZOrder { get; set; } = 0;
    }

    /// <summary>
    /// Geometric overlay element (handles, selection marquee, etc.)
    /// </summary>
    public class GeometryOverlay : OverlayElement
    {
        public GeometryOverlay(string id, NTSGeometry geometry) : base(id)
        {
            Geometry = geometry;
            Style = new LayerStyle();
        }

        /// <summary>
        /// The geometry to render
        /// </summary>
        public NTSGeometry Geometry { get; set; }

        /// <summary>
        /// Style for rendering the geometry
        /// </summary>
        public LayerStyle Style { get; set; }
    }

    /// <summary>
    /// Text overlay element for labels and UI text
    /// </summary>
    public class TextOverlay : OverlayElement
    {
        public TextOverlay(string id, string text, Coordinate position) : base(id)
        {
            Text = text;
            Position = position;
            FontSize = 12;
            Color = Colors.Black;
        }

        /// <summary>
        /// Text to display
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Position in world coordinates
        /// </summary>
        public Coordinate Position { get; set; }

        /// <summary>
        /// Font size in pixels
        /// </summary>
        public double FontSize { get; set; }

        /// <summary>
        /// Text color
        /// </summary>
        public Color Color { get; set; }

        /// <summary>
        /// Background color (null for no background)
        /// </summary>
        public Color? BackgroundColor { get; set; }
    }
}