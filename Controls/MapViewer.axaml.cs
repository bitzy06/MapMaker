using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using AvaloniaPoint = Avalonia.Point;
using FilePath = System.IO.Path;
using NTSGeometry = NetTopologySuite.Geometries.Geometry;

namespace MapMaker.Controls
{
    public partial class MapViewer : UserControl
    {
        private bool _isDragging = false;
        private AvaloniaPoint _lastPointerPosition;
        private double _zoomFactor = 1.0;
        private AvaloniaPoint _panOffset = new AvaloniaPoint(0, 0);
        
        private List<NTSGeometry> _currentShapes = new List<NTSGeometry>();
        private string _currentMapType = "country";

        public MapViewer()
        {
            InitializeComponent();
            SetupEventHandlers();
        }

        private void SetupEventHandlers()
        {
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerWheelChanged += OnPointerWheelChanged;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                _lastPointerPosition = e.GetCurrentPoint(this).Position;
                this.Cursor = new Cursor(StandardCursorType.Hand);
                e.Handled = true;
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                var currentPosition = e.GetCurrentPoint(this).Position;
                var delta = currentPosition - _lastPointerPosition;
                
                _panOffset = new AvaloniaPoint(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
                UpdateTransform();
                
                _lastPointerPosition = currentPosition;
                e.Handled = true;
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            _isDragging = false;
            this.Cursor = new Cursor(StandardCursorType.Arrow);
            e.Handled = true;
        }

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            var delta = e.Delta.Y;
            var zoomScale = delta > 0 ? 1.1 : 0.9;
            
            _zoomFactor *= zoomScale;
            _zoomFactor = Math.Max(0.1, Math.Min(10.0, _zoomFactor)); // Clamp zoom
            
            UpdateTransform();
            e.Handled = true;
        }

        private void UpdateTransform()
        {
            // Simple transform without CompositeTransform for now
            // This is a placeholder - we'll implement proper transforms later
        }

        public void LoadMap(string mapType)
        {
            _currentMapType = mapType;
            _currentShapes.Clear();
            MapCanvas.Children.Clear();

            var mapDirectory = FilePath.Combine(Directory.GetCurrentDirectory(), mapType);
            if (!Directory.Exists(mapDirectory))
            {
                // Create placeholder text if directory doesn't exist
                var textBlock = new TextBlock
                {
                    Text = $"No {mapType} data found. Place shapefiles in '{mapType}' folder.",
                    Foreground = Brushes.Red,
                    FontSize = 16,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };
                Canvas.SetLeft(textBlock, 50);
                Canvas.SetTop(textBlock, 50);
                MapCanvas.Children.Add(textBlock);
                return;
            }

            var shapeFiles = Directory.GetFiles(mapDirectory, "*.shp");
            if (shapeFiles.Length == 0)
            {
                var textBlock = new TextBlock
                {
                    Text = $"No .shp files found in '{mapType}' folder.",
                    Foreground = Brushes.Orange,
                    FontSize = 16
                };
                Canvas.SetLeft(textBlock, 50);
                Canvas.SetTop(textBlock, 50);
                MapCanvas.Children.Add(textBlock);
                return;
            }

            try
            {
                foreach (var shapeFile in shapeFiles)
                {
                    LoadShapeFile(shapeFile);
                }
                
                ShowShapeFileInfo();
            }
            catch (Exception ex)
            {
                var textBlock = new TextBlock
                {
                    Text = $"Error loading shapefiles: {ex.Message}",
                    Foreground = Brushes.Red,
                    FontSize = 14,
                    TextWrapping = TextWrapping.Wrap
                };
                Canvas.SetLeft(textBlock, 10);
                Canvas.SetTop(textBlock, 10);
                MapCanvas.Children.Add(textBlock);
            }
        }

        private void LoadShapeFile(string filePath)
        {
            try
            {
                var reader = new ShapefileReader(filePath);
                var shapeHeader = reader.Header;
                
                // For now, just count the shapes we would load
                var geometryCount = 0;
                foreach (var feature in reader.ReadAll())
                {
                    if (feature != null)
                    {
                        geometryCount++;
                    }
                }
                
                // Create a simple placeholder geometry for counting purposes
                for (int i = 0; i < geometryCount; i++)
                {
                    // We'll add actual geometry loading later
                    // For now, just track that we loaded something
                }
                
                // Store the shape count for display purposes
                _shapeCount = geometryCount;
            }
            catch (Exception)
            {
                // Log error but don't fail completely
                _shapeCount = -1;
            }
        }
        
        private int _shapeCount = 0;

        private void ShowShapeFileInfo()
        {
            var infoText = $"Loaded map data from {_currentMapType} directory.\n" +
                          "Pan: Click and drag\nZoom: Mouse wheel\n" +
                          "Use toggles above to switch between country/state data.";
            
            var textBlock = new TextBlock
            {
                Text = infoText,
                Foreground = Brushes.DarkBlue,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 400
            };
            Canvas.SetLeft(textBlock, 20);
            Canvas.SetTop(textBlock, 20);
            MapCanvas.Children.Add(textBlock);

            // Add a simple rectangle to represent the map area
            var rect = new Avalonia.Controls.Shapes.Rectangle
            {
                Width = 600,
                Height = 400,
                Fill = Brushes.LightGreen,
                Stroke = Brushes.DarkGreen,
                StrokeThickness = 2,
                Opacity = 0.3
            };
            Canvas.SetLeft(rect, 100);
            Canvas.SetTop(rect, 100);
            MapCanvas.Children.Add(rect);
            
            var mapText = new TextBlock
            {
                Text = $"{_currentMapType.ToUpper()} MAP PLACEHOLDER\n(Shapefile data detected)",
                Foreground = Brushes.DarkGreen,
                FontSize = 18,
                FontWeight = FontWeight.Bold,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            Canvas.SetLeft(mapText, 250);
            Canvas.SetTop(mapText, 280);
            MapCanvas.Children.Add(mapText);
        }

        public List<NTSGeometry> GetCurrentShapes()
        {
            return new List<NTSGeometry>(_currentShapes);
        }

        public string GetCurrentMapType()
        {
            return _currentMapType;
        }
    }
}