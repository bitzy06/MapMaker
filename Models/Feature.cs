using NetTopologySuite.Geometries;
using System.Collections.Generic;

namespace MapMaker.Models
{
    /// <summary>
    /// Represents a geographic feature with geometry and attributes
    /// </summary>
    public class Feature
    {
        public Feature(string id, Geometry geometry)
        {
            Id = id;
            Geometry = geometry;
            Attributes = new Dictionary<string, object?>();
        }

        /// <summary>
        /// Unique identifier for the feature
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The geometric representation of the feature
        /// </summary>
        public Geometry Geometry { get; set; }

        /// <summary>
        /// Key-value attributes associated with the feature
        /// </summary>
        public Dictionary<string, object?> Attributes { get; set; }

        /// <summary>
        /// Whether the feature is currently selected
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Whether the feature is currently highlighted (hovered)
        /// </summary>
        public bool IsHighlighted { get; set; }

        /// <summary>
        /// Get attribute value by key
        /// </summary>
        public T? GetAttribute<T>(string key)
        {
            if (Attributes.TryGetValue(key, out var value) && value is T typedValue)
            {
                return typedValue;
            }
            return default(T);
        }

        /// <summary>
        /// Set attribute value by key
        /// </summary>
        public void SetAttribute(string key, object? value)
        {
            Attributes[key] = value;
        }

        /// <summary>
        /// Remove attribute by key
        /// </summary>
        public bool RemoveAttribute(string key)
        {
            return Attributes.Remove(key);
        }

        /// <summary>
        /// Get the bounding envelope of the feature
        /// </summary>
        public Envelope Bounds => Geometry.EnvelopeInternal;
    }

    /// <summary>
    /// Represents game-specific attributes for strategy map features
    /// </summary>
    public static class GameAttributes
    {
        public const string OwnerId = "OWNER_ID";
        public const string TerrainType = "TERRAIN";
        public const string SupplyValue = "SUPPLY";
        public const string ProvinceId = "PROV_ID";
        public const string ProvinceName = "PROV_NAME";
        public const string Population = "POPULATION";
        public const string Wealth = "WEALTH";
        public const string Fortification = "FORT_LEVEL";
        public const string Culture = "CULTURE";
        public const string Religion = "RELIGION";
        public const string Development = "DEVELOPMENT";
    }

    /// <summary>
    /// Simple spatial index interface for efficient spatial queries
    /// </summary>
    public interface ISpatialIndex<T>
    {
        /// <summary>
        /// Insert an item with its bounding envelope
        /// </summary>
        void Insert(Envelope envelope, T item);

        /// <summary>
        /// Remove an item from the index
        /// </summary>
        bool Remove(Envelope envelope, T item);

        /// <summary>
        /// Query items that intersect with the given envelope
        /// </summary>
        IEnumerable<T> Query(Envelope envelope);

        /// <summary>
        /// Clear all items from the index
        /// </summary>
        void Clear();

        /// <summary>
        /// Get the number of items in the index
        /// </summary>
        int Count { get; }
    }
}