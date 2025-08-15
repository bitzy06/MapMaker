using System;
using System.Linq;
using NetTopologySuite.Geometries;
using MapMaker.Models;
using MapMaker.Services;

namespace MapMaker
{
    /// <summary>
    /// Simple test to verify grid system functionality
    /// </summary>
    public class GridSystemTest
    {
        public static void RunTest()
        {
            Console.WriteLine("=== Grid System Test ===");
            
            // Create a simple grid system
            var origin = new NetTopologySuite.Geometries.Point(0, 0);
            var gridSystem = new GridSystem(origin);
            
            // Create a simple polygon (square)
            var factory = new GeometryFactory();
            var square = factory.CreatePolygon(new Coordinate[]
            {
                new Coordinate(5, 5),
                new Coordinate(25, 5),
                new Coordinate(25, 25),
                new Coordinate(5, 25),
                new Coordinate(5, 5)
            });
            
            // Create a feature
            var feature = new Feature("test_square", square);
            feature.Attributes["owner"] = "player1";
            feature.Attributes["terrain"] = "grassland";
            
            Console.WriteLine($"Original square bounds: {square.EnvelopeInternal}");
            Console.WriteLine($"Grid size: {GridSystem.GRID_SIZE}m");
            
            // Convert to grid
            var converter = new GeometryToGridConverter();
            var options = new GridConversionOptions
            {
                MinCoverage = 0.1,
                CalculateExactCoverage = false,
                PreserveAttributes = true
            };
            
            var gridCells = converter.ConvertGeometryToGrid(square, gridSystem, options).ToList();
            
            Console.WriteLine($"Converted to {gridCells.Count} grid cells");
            
            // Create grid layer
            var gridLayer = new GridLayer("test_layer", gridSystem);
            converter.ConvertFeatureToGrid(feature, gridLayer, options);
            
            var stats = gridLayer.GetStats();
            Console.WriteLine($"Grid layer stats: {stats}");
            
            // Test coordinate conversion
            var testCoord = new Coordinate(10, 15);
            var gridCoord = gridSystem.WorldToGrid(testCoord);
            var backToWorld = gridSystem.GridToWorld(gridCoord);
            
            Console.WriteLine($"World coord {testCoord} -> Grid coord {gridCoord} -> Back to world {backToWorld}");
            
            Console.WriteLine("=== Grid System Test Complete ===");
        }
    }
}