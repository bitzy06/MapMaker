using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MapMaker.Models
{
    /// <summary>
    /// Base implementation for layers
    /// </summary>
    public abstract class LayerBase : ILayer
    {
        protected LayerBase(string name)
        {
            Id = Guid.NewGuid().ToString();
            Name = name;
            Visible = true;
            Locked = false;
            Opacity = 1.0;
            ZOrder = 0;
        }

        public string Id { get; }
        public string Name { get; set; }
        public bool Visible { get; set; }
        public bool Locked { get; set; }
        public double Opacity { get; set; }
        public abstract Envelope? Bounds { get; }
        public int ZOrder { get; set; }
    }

    /// <summary>
    /// Implementation of vector layer containing geographic features
    /// </summary>
    public class VectorLayer : LayerBase, IVectorLayer
    {
        private readonly List<Feature> _features;
        private readonly SimpleSpatialIndex<Feature> _spatialIndex;
        private Envelope? _bounds;

        public VectorLayer(string name) : base(name)
        {
            _features = new List<Feature>();
            _spatialIndex = new SimpleSpatialIndex<Feature>();
            Style = new LayerStyle();
        }

        public IList<Feature> Features => _features;
        public LayerStyle Style { get; set; }
        public ISpatialIndex<Feature> SpatialIndex => _spatialIndex;

        public override Envelope? Bounds
        {
            get
            {
                if (_bounds == null && _features.Count > 0)
                {
                    _bounds = new Envelope();
                    foreach (var feature in _features)
                    {
                        _bounds.ExpandToInclude(feature.Bounds);
                    }
                }
                return _bounds;
            }
        }

        public void AddFeature(Feature feature)
        {
            _features.Add(feature);
            _spatialIndex.Insert(feature.Bounds, feature);
            _bounds = null; // Invalidate cached bounds
        }

        public bool RemoveFeature(Feature feature)
        {
            var removed = _features.Remove(feature);
            if (removed)
            {
                _spatialIndex.Remove(feature.Bounds, feature);
                _bounds = null; // Invalidate cached bounds
            }
            return removed;
        }

        public IEnumerable<Feature> QueryFeatures(Envelope envelope)
        {
            return _spatialIndex.Query(envelope);
        }

        public IEnumerable<Feature> QueryFeatures(Geometry geometry)
        {
            var candidates = _spatialIndex.Query(geometry.EnvelopeInternal);
            return candidates.Where(f => f.Geometry.Intersects(geometry));
        }

        public void ClearFeatures()
        {
            _features.Clear();
            _spatialIndex.Clear();
            _bounds = null;
        }
    }

    /// <summary>
    /// Implementation of overlay layer for transient drawing elements
    /// </summary>
    public class OverlayLayer : LayerBase, IOverlayLayer
    {
        private readonly List<OverlayElement> _elements;

        public OverlayLayer(string name) : base(name)
        {
            _elements = new List<OverlayElement>();
        }

        public IList<OverlayElement> Elements => _elements;

        public override Envelope? Bounds
        {
            get
            {
                Envelope? bounds = null;
                foreach (var element in _elements.Where(e => e.Visible))
                {
                    if (element is GeometryOverlay geomOverlay)
                    {
                        if (bounds == null)
                        {
                            bounds = new Envelope(geomOverlay.Geometry.EnvelopeInternal);
                        }
                        else
                        {
                            bounds.ExpandToInclude(geomOverlay.Geometry.EnvelopeInternal);
                        }
                    }
                }
                return bounds;
            }
        }

        public void AddElement(OverlayElement element)
        {
            _elements.Add(element);
        }

        public bool RemoveElement(OverlayElement element)
        {
            return _elements.Remove(element);
        }

        public void ClearElements()
        {
            _elements.Clear();
        }
    }

    /// <summary>
    /// Simple spatial index implementation using a list (can be replaced with R-tree later)
    /// </summary>
    public class SimpleSpatialIndex<T> : ISpatialIndex<T>
    {
        private readonly List<(Envelope envelope, T item)> _items;

        public SimpleSpatialIndex()
        {
            _items = new List<(Envelope envelope, T item)>();
        }

        public int Count => _items.Count;

        public void Insert(Envelope envelope, T item)
        {
            _items.Add((new Envelope(envelope), item));
        }

        public bool Remove(Envelope envelope, T item)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (EqualityComparer<T>.Default.Equals(_items[i].item, item))
                {
                    _items.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        public IEnumerable<T> Query(Envelope envelope)
        {
            return _items
                .Where(x => x.envelope.Intersects(envelope))
                .Select(x => x.item);
        }

        public void Clear()
        {
            _items.Clear();
        }
    }
}