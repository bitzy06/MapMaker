using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Input;
using NetTopologySuite.Geometries;
using MapMaker.Tools;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Models
{
    /// <summary>
    /// Manages the current editing state of the map editor
    /// </summary>
    public class EditorState
    {
        public EditorState()
        {
            Selection = new Selection();
            SnapTargets = new List<SnapTarget>();
            ModifierKeys = KeyModifiers.None;
        }

        /// <summary>
        /// Currently active tool
        /// </summary>
        public ITool? ActiveTool { get; set; }

        /// <summary>
        /// Current selection set
        /// </summary>
        public Selection Selection { get; }

        /// <summary>
        /// Currently hovered feature
        /// </summary>
        public Feature? HoverTarget { get; set; }

        /// <summary>
        /// Available snap targets
        /// </summary>
        public List<SnapTarget> SnapTargets { get; }

        /// <summary>
        /// Currently active layer for editing
        /// </summary>
        public IVectorLayer? ActiveLayer { get; set; }

        /// <summary>
        /// Current modifier keys state
        /// </summary>
        public KeyModifiers ModifierKeys { get; set; }

        /// <summary>
        /// Current brush settings for paint tools
        /// </summary>
        public BrushSettings CurrentBrush { get; set; } = new BrushSettings();

        /// <summary>
        /// Whether snapping is currently enabled
        /// </summary>
        public bool SnapEnabled { get; set; } = true;

        /// <summary>
        /// Current snap mode
        /// </summary>
        public SnapMode SnapMode { get; set; } = SnapMode.Vertex | SnapMode.Edge;

        /// <summary>
        /// Current coordinate under cursor in world coordinates
        /// </summary>
        public Coordinate? CursorWorldPosition { get; set; }

        /// <summary>
        /// Current coordinate under cursor in screen coordinates
        /// </summary>
        public AvaloniaPoint CursorScreenPosition { get; set; }

        /// <summary>
        /// Event raised when selection changes
        /// </summary>
        public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;

        /// <summary>
        /// Event raised when hover target changes
        /// </summary>
        public event EventHandler<HoverChangedEventArgs>? HoverChanged;

        /// <summary>
        /// Event raised when active tool changes
        /// </summary>
        public event EventHandler<ToolChangedEventArgs>? ActiveToolChanged;

        internal void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            SelectionChanged?.Invoke(this, e);
        }

        internal void OnHoverChanged(HoverChangedEventArgs e)
        {
            HoverChanged?.Invoke(this, e);
        }

        internal void OnActiveToolChanged(ToolChangedEventArgs e)
        {
            ActiveToolChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Set the active tool
        /// </summary>
        public void SetActiveTool(ITool? tool)
        {
            var previousTool = ActiveTool;
            ActiveTool?.Deactivate();
            ActiveTool = tool;
            
            var eventArgs = new ToolChangedEventArgs(previousTool, tool);
            OnActiveToolChanged(eventArgs);
        }

        /// <summary>
        /// Set the hover target
        /// </summary>
        public void SetHoverTarget(Feature? feature)
        {
            if (HoverTarget == feature) return;
            
            var previousTarget = HoverTarget;
            if (previousTarget != null)
                previousTarget.IsHighlighted = false;
            
            HoverTarget = feature;
            if (HoverTarget != null)
                HoverTarget.IsHighlighted = true;

            var eventArgs = new HoverChangedEventArgs(previousTarget, feature);
            OnHoverChanged(eventArgs);
        }
    }

    /// <summary>
    /// Manages selected features and vertices
    /// </summary>
    public class Selection
    {
        private readonly HashSet<Feature> _selectedFeatures;
        private readonly Dictionary<Feature, HashSet<int>> _selectedVertices;

        public Selection()
        {
            _selectedFeatures = new HashSet<Feature>();
            _selectedVertices = new Dictionary<Feature, HashSet<int>>();
        }

        /// <summary>
        /// Selected features
        /// </summary>
        public IReadOnlySet<Feature> Features => _selectedFeatures;

        /// <summary>
        /// Selected vertices by feature
        /// </summary>
        public IReadOnlyDictionary<Feature, HashSet<int>> Vertices => _selectedVertices;

        /// <summary>
        /// Number of selected features
        /// </summary>
        public int FeatureCount => _selectedFeatures.Count;

        /// <summary>
        /// Total number of selected vertices across all features
        /// </summary>
        public int VertexCount => _selectedVertices.Values.Sum(v => v.Count);

        /// <summary>
        /// Bounding box of selected features
        /// </summary>
        public Envelope? BoundingBox
        {
            get
            {
                if (_selectedFeatures.Count == 0) return null;
                
                Envelope? bounds = null;
                foreach (var feature in _selectedFeatures)
                {
                    if (bounds == null)
                    {
                        bounds = new Envelope(feature.Bounds);
                    }
                    else
                    {
                        bounds.ExpandToInclude(feature.Bounds);
                    }
                }
                return bounds;
            }
        }

        /// <summary>
        /// Add a feature to the selection
        /// </summary>
        public bool AddFeature(Feature feature)
        {
            if (_selectedFeatures.Add(feature))
            {
                feature.IsSelected = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Remove a feature from the selection
        /// </summary>
        public bool RemoveFeature(Feature feature)
        {
            if (_selectedFeatures.Remove(feature))
            {
                feature.IsSelected = false;
                _selectedVertices.Remove(feature);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Toggle feature selection
        /// </summary>
        public bool ToggleFeature(Feature feature)
        {
            return _selectedFeatures.Contains(feature) ? RemoveFeature(feature) : AddFeature(feature);
        }

        /// <summary>
        /// Add multiple features to the selection
        /// </summary>
        public void AddFeatures(IEnumerable<Feature> features)
        {
            foreach (var feature in features)
            {
                AddFeature(feature);
            }
        }

        /// <summary>
        /// Clear all selections
        /// </summary>
        public void Clear()
        {
            foreach (var feature in _selectedFeatures)
            {
                feature.IsSelected = false;
            }
            _selectedFeatures.Clear();
            _selectedVertices.Clear();
        }

        /// <summary>
        /// Select vertices of a feature
        /// </summary>
        public void SelectVertices(Feature feature, IEnumerable<int> vertexIndices)
        {
            if (!_selectedFeatures.Contains(feature))
                AddFeature(feature);

            if (!_selectedVertices.TryGetValue(feature, out var vertices))
            {
                vertices = new HashSet<int>();
                _selectedVertices[feature] = vertices;
            }

            foreach (var index in vertexIndices)
            {
                vertices.Add(index);
            }
        }

        /// <summary>
        /// Deselect vertices of a feature
        /// </summary>
        public void DeselectVertices(Feature feature, IEnumerable<int> vertexIndices)
        {
            if (_selectedVertices.TryGetValue(feature, out var vertices))
            {
                foreach (var index in vertexIndices)
                {
                    vertices.Remove(index);
                }

                if (vertices.Count == 0)
                {
                    _selectedVertices.Remove(feature);
                }
            }
        }
    }

    /// <summary>
    /// Represents a snap target for vertex/edge snapping
    /// </summary>
    public class SnapTarget
    {
        public SnapTarget(Coordinate coordinate, SnapType type, Feature? feature = null, int? vertexIndex = null)
        {
            Coordinate = coordinate;
            Type = type;
            Feature = feature;
            VertexIndex = vertexIndex;
        }

        public Coordinate Coordinate { get; }
        public SnapType Type { get; }
        public Feature? Feature { get; }
        public int? VertexIndex { get; }
    }

    /// <summary>
    /// Settings for paint/brush tools
    /// </summary>
    public class BrushSettings
    {
        public double Size { get; set; } = 10.0;
        public string AttributeKey { get; set; } = GameAttributes.OwnerId;
        public object? AttributeValue { get; set; }
        public bool UseSelection { get; set; } = false;
    }

    /// <summary>
    /// Snap types for snapping system
    /// </summary>
    [Flags]
    public enum SnapMode
    {
        None = 0,
        Vertex = 1,
        Edge = 2,
        Midpoint = 4,
        Perpendicular = 8,
        Grid = 16,
        All = Vertex | Edge | Midpoint | Perpendicular | Grid
    }

    public enum SnapType
    {
        Vertex,
        EdgeMidpoint,
        Edge,
        Perpendicular,
        Grid
    }

    // Event argument classes
    public class SelectionChangedEventArgs : EventArgs
    {
        public SelectionChangedEventArgs(Selection selection)
        {
            Selection = selection;
        }

        public Selection Selection { get; }
    }

    public class HoverChangedEventArgs : EventArgs
    {
        public HoverChangedEventArgs(Feature? previousTarget, Feature? newTarget)
        {
            PreviousTarget = previousTarget;
            NewTarget = newTarget;
        }

        public Feature? PreviousTarget { get; }
        public Feature? NewTarget { get; }
    }

    public class ToolChangedEventArgs : EventArgs
    {
        public ToolChangedEventArgs(ITool? previousTool, ITool? newTool)
        {
            PreviousTool = previousTool;
            NewTool = newTool;
        }

        public ITool? PreviousTool { get; }
        public ITool? NewTool { get; }
    }
}