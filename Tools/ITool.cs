using Avalonia.Input;
using MapMaker.Models;
using MapMaker.Services;
using AvaloniaPoint = Avalonia.Point;
using NTSGeometry = NetTopologySuite.Geometries.Geometry;

namespace MapMaker.Tools
{
    /// <summary>
    /// Interface for all map editing tools
    /// </summary>
    public interface ITool
    {
        /// <summary>
        /// Tool name for display in UI
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Tool description for tooltips
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Whether the tool is currently active
        /// </summary>
        bool IsActive { get; }

        /// <summary>
        /// Activate the tool with the given context
        /// </summary>
        void Activate(ToolContext context);

        /// <summary>
        /// Deactivate the tool
        /// </summary>
        void Deactivate();

        /// <summary>
        /// Handle mouse down event
        /// </summary>
        void OnMouseDown(PointerPressedEventArgs e);

        /// <summary>
        /// Handle mouse move event
        /// </summary>
        void OnMouseMove(PointerEventArgs e);

        /// <summary>
        /// Handle mouse up event
        /// </summary>
        void OnMouseUp(PointerReleasedEventArgs e);

        /// <summary>
        /// Handle key down event
        /// </summary>
        void OnKeyDown(KeyEventArgs e);

        /// <summary>
        /// Handle key up event
        /// </summary>
        void OnKeyUp(KeyEventArgs e);

        /// <summary>
        /// Draw tool-specific overlays (gizmos, guides, etc.)
        /// </summary>
        void DrawOverlay(IDrawContext drawContext);

        /// <summary>
        /// Get status text for the tool
        /// </summary>
        string GetStatusText();
    }

    /// <summary>
    /// Context provided to tools for accessing services and state
    /// </summary>
    public class ToolContext
    {
        public ToolContext(
            MapDocument document,
            EditorState editorState,
            IGeometryService geometryService,
            IHitTestService hitTestService,
            IUndoRedoService undoRedoService,
            IPersistenceService persistenceService,
            IRenderService renderService)
        {
            Document = document;
            EditorState = editorState;
            GeometryService = geometryService;
            HitTestService = hitTestService;
            UndoRedoService = undoRedoService;
            PersistenceService = persistenceService;
            RenderService = renderService;
        }

        /// <summary>
        /// Current map document
        /// </summary>
        public MapDocument Document { get; }

        /// <summary>
        /// Current editor state
        /// </summary>
        public EditorState EditorState { get; }

        /// <summary>
        /// Geometry operations service
        /// </summary>
        public IGeometryService GeometryService { get; }

        /// <summary>
        /// Hit testing service
        /// </summary>
        public IHitTestService HitTestService { get; }

        /// <summary>
        /// Undo/redo service
        /// </summary>
        public IUndoRedoService UndoRedoService { get; }

        /// <summary>
        /// Persistence service
        /// </summary>
        public IPersistenceService PersistenceService { get; }

        /// <summary>
        /// Rendering service
        /// </summary>
        public IRenderService RenderService { get; }

        /// <summary>
        /// Get the current active layer for editing
        /// </summary>
        public IVectorLayer? GetActiveLayer()
        {
            return EditorState.ActiveLayer;
        }

        /// <summary>
        /// Get world-to-screen transform
        /// </summary>
        public ICoordinateTransform GetWorldToScreenTransform()
        {
            return RenderService.GetWorldToScreenTransform();
        }

        /// <summary>
        /// Get screen-to-world transform
        /// </summary>
        public ICoordinateTransform GetScreenToWorldTransform()
        {
            return RenderService.GetScreenToWorldTransform();
        }
    }

    /// <summary>
    /// Drawing context for tool overlays
    /// </summary>
    public interface IDrawContext
    {
        /// <summary>
        /// Draw a geometry with the specified style
        /// </summary>
        void DrawGeometry(NTSGeometry geometry, LayerStyle style);

        /// <summary>
        /// Draw text at the specified position
        /// </summary>
        void DrawText(string text, NetTopologySuite.Geometries.Coordinate position, Avalonia.Media.Color color, double fontSize = 12);

        /// <summary>
        /// Draw a line from start to end
        /// </summary>
        void DrawLine(AvaloniaPoint start, AvaloniaPoint end, Avalonia.Media.Color color, double thickness = 1);

        /// <summary>
        /// Draw a circle at the specified center with radius
        /// </summary>
        void DrawCircle(AvaloniaPoint center, double radius, Avalonia.Media.Color color, bool filled = false);

        /// <summary>
        /// Draw a rectangle
        /// </summary>
        void DrawRectangle(Avalonia.Rect rectangle, Avalonia.Media.Color color, bool filled = false);
    }

    /// <summary>
    /// Base class for tools providing common functionality
    /// </summary>
    public abstract class ToolBase : ITool
    {
        protected ToolContext? Context { get; private set; }

        public abstract string Name { get; }
        public abstract string Description { get; }
        public bool IsActive { get; private set; }

        public virtual void Activate(ToolContext context)
        {
            Context = context;
            IsActive = true;
            OnActivated();
        }

        public virtual void Deactivate()
        {
            OnDeactivated();
            IsActive = false;
            Context = null;
        }

        public virtual void OnMouseDown(PointerPressedEventArgs e) { }
        public virtual void OnMouseMove(PointerEventArgs e) { }
        public virtual void OnMouseUp(PointerReleasedEventArgs e) { }
        public virtual void OnKeyDown(KeyEventArgs e) { }
        public virtual void OnKeyUp(KeyEventArgs e) { }
        public virtual void DrawOverlay(IDrawContext drawContext) { }
        public virtual string GetStatusText() { return $"{Name} tool active"; }

        /// <summary>
        /// Called when the tool is activated
        /// </summary>
        protected virtual void OnActivated() { }

        /// <summary>
        /// Called when the tool is deactivated
        /// </summary>
        protected virtual void OnDeactivated() { }

        /// <summary>
        /// Get world coordinate from screen point
        /// </summary>
        protected NetTopologySuite.Geometries.Coordinate ScreenToWorld(AvaloniaPoint screenPoint)
        {
            if (Context == null) return new NetTopologySuite.Geometries.Coordinate(0, 0);
            return Context.GetScreenToWorldTransform().Transform(screenPoint);
        }

        /// <summary>
        /// Get screen point from world coordinate
        /// </summary>
        protected AvaloniaPoint WorldToScreen(NetTopologySuite.Geometries.Coordinate worldCoord)
        {
            if (Context == null) return new AvaloniaPoint(0, 0);
            return Context.GetWorldToScreenTransform().Transform(worldCoord);
        }
    }
}