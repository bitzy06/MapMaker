using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using MapMaker.Tools;
using AvaloniaPoint = Avalonia.Point;
using FilePath = System.IO.Path;
using NTSGeometry = NetTopologySuite.Geometries.Geometry;
using NTSPoint = NetTopologySuite.Geometries.Point;

namespace MapMaker.Controls
{
    public partial class MapViewer : UserControl
    {
        private bool _isDragging = false;
        private AvaloniaPoint _lastPointerPosition;
        private double _zoomFactor = 1.0;
        private AvaloniaPoint _panOffset = new AvaloniaPoint(0, 0);
        
        private readonly List<NTSGeometry> _currentShapes = new List<NTSGeometry>();
        private string _currentMapType = "country";

        // Rendering state
        private Canvas? _shapeCanvas; // holds drawn shapes so we can transform it independently
        private Envelope? _dataEnvelope; // world data bounds
        private double _fitScale = 1.0; // world->screen scale to fit
        private AvaloniaPoint _fitOffset = new AvaloniaPoint(0, 0); // world->screen translation to fit
        private int _shapeCount = 0; // total number of geometries loaded

        // Tool system integration
        private MainWindow? _toolManager;

        public MapViewer()
        {
            InitializeComponent();
            SetupEventHandlers();

            // Create a dedicated canvas for shapes
            _shapeCanvas = new Canvas();
            MapCanvas.Children.Add(_shapeCanvas);

            // Redraw when size changes
            MapCanvas.SizeChanged += (_, __) =>
            {
                RecomputeFitTransform();
                RedrawShapes();
                UpdateTransform();
            };
        }

        public void SetToolManager(MainWindow toolManager)
        {
            _toolManager = toolManager;
        }

        private void SetupEventHandlers()
        {
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerWheelChanged += OnPointerWheelChanged;
            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Try to delegate to active tool first
            var activeTool = _toolManager?.GetActiveTool();
            if (activeTool != null && activeTool.Name != "Navigate")
            {
                activeTool.OnMouseDown(e);
                if (e.Handled) return;
            }

            // Fall back to default navigation behavior
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
            // Try to delegate to active tool first
            var activeTool = _toolManager?.GetActiveTool();
            if (activeTool != null && activeTool.Name != "Navigate")
            {
                activeTool.OnMouseMove(e);
                if (e.Handled) return;
            }

            // Fall back to default navigation behavior
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
            // Try to delegate to active tool first
            var activeTool = _toolManager?.GetActiveTool();
            if (activeTool != null && activeTool.Name != "Navigate")
            {
                activeTool.OnMouseUp(e);
                if (e.Handled) return;
            }

            // Fall back to default navigation behavior
            _isDragging = false;
            this.Cursor = new Cursor(StandardCursorType.Arrow);
            e.Handled = true;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            // Delegate to active tool
            var activeTool = _toolManager?.GetActiveTool();
            activeTool?.OnKeyDown(e);
        }

        private void OnKeyUp(object? sender, KeyEventArgs e)
        {
            // Delegate to active tool
            var activeTool = _toolManager?.GetActiveTool();
            activeTool?.OnKeyUp(e);
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
            // Apply pan/zoom to the shapes canvas, around the center of the visible area
            if (_shapeCanvas == null)
                return;

            var width = MapCanvas.Bounds.Width;
            var height = MapCanvas.Bounds.Height;
            if (width <= 0 || height <= 0)
                return;

            var cx = width / 2.0;
            var cy = height / 2.0;

            var m = Matrix.Identity;
            m *= Matrix.CreateTranslation(-cx, -cy);
            m *= Matrix.CreateScale(_zoomFactor, _zoomFactor);
            m *= Matrix.CreateTranslation(cx + _panOffset.X, cy + _panOffset.Y);

            _shapeCanvas.RenderTransform = new MatrixTransform(m);
        }

        public void LoadMap(string mapType)
        {
            Console.WriteLine($"LoadMap called with mapType: {mapType}");
            _currentMapType = mapType;
            _currentShapes.Clear();
            _dataEnvelope = null;
            _fitScale = 1.0;
            _fitOffset = new AvaloniaPoint(0, 0);
            _shapeCount = 0; // Reset counter

            // Reset UI
            MapCanvas.Children.Clear();
            if (_shapeCanvas == null)
                _shapeCanvas = new Canvas();
            else
                _shapeCanvas.Children.Clear();
            MapCanvas.Children.Add(_shapeCanvas);

            // Try multiple approaches to find the correct base directory
            var possibleBasePaths = new List<string>();
            
            // Current working directory
            possibleBasePaths.Add(Directory.GetCurrentDirectory());
            Console.WriteLine($"Current working directory: {Directory.GetCurrentDirectory()}");
            
            // Assembly location directory (where the executable is)
            var assemblyLocation = Assembly.GetExecutingAssembly().Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var assemblyDir = FilePath.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDir))
                {
                    possibleBasePaths.Add(assemblyDir);
                    Console.WriteLine($"Assembly directory: {assemblyDir}");
                    
                    // Also try parent directories in case we're in bin/Debug/net8.0
                    var parentDir = Directory.GetParent(assemblyDir);
                    if (parentDir != null)
                    {
                        possibleBasePaths.Add(parentDir.FullName);
                        var grandParentDir = Directory.GetParent(parentDir.FullName);
                        if (grandParentDir != null)
                        {
                            possibleBasePaths.Add(grandParentDir.FullName);
                            var greatGrandParentDir = Directory.GetParent(grandParentDir.FullName);
                            if (greatGrandParentDir != null)
                            {
                                possibleBasePaths.Add(greatGrandParentDir.FullName);
                            }
                        }
                    }
                }
            }

            // Debug: Show all paths we're checking
            var debugText = $"Debug Info - Looking for '{mapType}' directory:\n";
            
            string? foundMapDirectory = null;
            foreach (var basePath in possibleBasePaths)
            {
                var mapDirectory = FilePath.Combine(basePath, mapType);
                var exists = Directory.Exists(mapDirectory);
                Console.WriteLine($"Checking: {mapDirectory} - Exists: {exists}");
                debugText += $"Checking: {mapDirectory} - Exists: {exists}\n";
                
                if (exists)
                {
                    foundMapDirectory = mapDirectory;
                    Console.WriteLine($"? Found map directory: {foundMapDirectory}");
                    debugText += $"? Found map directory: {foundMapDirectory}\n";
                    break;
                }
            }

            if (foundMapDirectory == null)
            {
                Console.WriteLine($"ERROR: No {mapType} directory found in any of the search paths");
                // Create placeholder text if directory doesn't exist
                var textBlock = new TextBlock
                {
                    Text = $"No {mapType} data found. Place shapefiles in '{mapType}' folder.\n\n{debugText}",
                    Foreground = Brushes.Red,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 700
                };
                Canvas.SetLeft(textBlock, 10);
                Canvas.SetTop(textBlock, 10);
                MapCanvas.Children.Add(textBlock);
                return;
            }

            var shapeFiles = Directory.GetFiles(foundMapDirectory, "*.shp");
            var allFiles = Directory.GetFiles(foundMapDirectory);
            Console.WriteLine($"Found {allFiles.Length} total files, {shapeFiles.Length} shapefiles in {foundMapDirectory}");
            debugText += $"Files in directory:\n";
            foreach (var file in allFiles)
            {
                Console.WriteLine($"  File: {FilePath.GetFileName(file)}");
                debugText += $"  - {FilePath.GetFileName(file)}\n";
            }
            debugText += $"Shapefile (.shp) count: {shapeFiles.Length}\n";

            if (shapeFiles.Length == 0)
            {
                Console.WriteLine($"ERROR: No .shp files found in {foundMapDirectory}");
                var textBlock = new TextBlock
                {
                    Text = $"No .shp files found in '{mapType}' folder.\n\n{debugText}",
                    Foreground = Brushes.Orange,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 700
                };
                Canvas.SetLeft(textBlock, 10);
                Canvas.SetTop(textBlock, 10);
                MapCanvas.Children.Add(textBlock);
                return;
            }

            try
            {
                Console.WriteLine($"Attempting to load {shapeFiles.Length} shapefile(s)");
                foreach (var shapeFile in shapeFiles)
                {
                    Console.WriteLine($"Loading shapefile: {shapeFile}");
                    LoadShapeFile(shapeFile);
                }

                // Prepare transforms and draw
                RecomputeFitTransform();
                RedrawShapes();
                UpdateTransform();
                
                Console.WriteLine($"Successfully loaded {_shapeCount} total shapes");
                ShowShapeFileInfo(debugText);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR loading shapefiles: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                var textBlock = new TextBlock
                {
                    Text = $"Error loading shapefiles: {ex.Message}\n\n{debugText}\n\nStack Trace:\n{ex.StackTrace}",
                    Foreground = Brushes.Red,
                    FontSize = 12,
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 700
                };
                Canvas.SetLeft(textBlock, 10);
                Canvas.SetTop(textBlock, 10);
                MapCanvas.Children.Add(textBlock);
            }
        }

        private void LoadShapeFile(string filePath)
        {
            Console.WriteLine($"LoadShapeFile called for: {filePath}");
            try
            {
                var reader = new ShapefileReader(filePath);
                var shapeHeader = reader.Header;
                Console.WriteLine($"Shapefile header loaded, type: {shapeHeader.ShapeType}");
                
                var geoms = reader.ReadAll();
                foreach (var g in geoms)
                {
                    if (g == null) continue;
                    _currentShapes.Add(g);
                    _dataEnvelope ??= new Envelope(g.EnvelopeInternal);
                    _dataEnvelope.ExpandToInclude(g.EnvelopeInternal);
                }
                
                Console.WriteLine($"Added {geoms.Count} geometries from {FilePath.GetFileName(filePath)}");
                _shapeCount += geoms.Count;
            }
            catch (Exception ex)
            {
                // Log error details for debugging
                Console.WriteLine($"ERROR loading shapefile {filePath}: {ex.Message}");
                Console.WriteLine($"Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
                System.Diagnostics.Debug.WriteLine($"Error loading shapefile {filePath}: {ex.Message}");
                _shapeCount = -1;
                throw new Exception($"Failed to load shapefile '{FilePath.GetFileName(filePath)}': {ex.Message}", ex);
            }
        }

        private void RecomputeFitTransform()
        {
            if (_dataEnvelope == null)
                return;

            var width = MapCanvas.Bounds.Width;
            var height = MapCanvas.Bounds.Height;
            if (width <= 0 || height <= 0)
                return;

            var worldWidth = _dataEnvelope.Width;
            var worldHeight = _dataEnvelope.Height;
            if (worldWidth <= 0 || worldHeight <= 0)
            {
                _fitScale = 1.0;
                _fitOffset = new AvaloniaPoint(0, 0);
                return;
            }

            var scaleX = width / worldWidth;
            var scaleY = height / worldHeight;
            _fitScale = Math.Min(scaleX, scaleY);

            var screenWorldWidth = worldWidth * _fitScale;
            var screenWorldHeight = worldHeight * _fitScale;
            var dx = (width - screenWorldWidth) / 2.0;
            var dy = (height - screenWorldHeight) / 2.0;
            _fitOffset = new AvaloniaPoint(dx, dy);
        }

        private AvaloniaPoint WorldToScreen(Coordinate c)
        {
            // Map world (lon/lat or projected) to screen coordinates that fit MapCanvas
            if (_dataEnvelope == null)
                return new AvaloniaPoint(0, 0);

            // Flip Y so that higher latitudes go up on screen
            var x = (c.X - _dataEnvelope.MinX) * _fitScale + _fitOffset.X;
            var y = (_dataEnvelope.MaxY - c.Y) * _fitScale + _fitOffset.Y;
            return new AvaloniaPoint(x, y);
        }

        private void RedrawShapes()
        {
            if (_shapeCanvas == null)
                return;

            _shapeCanvas.Children.Clear();
            if (_currentShapes.Count == 0 || _dataEnvelope == null)
                return;

            foreach (var geom in _currentShapes)
            {
                switch (geom)
                {
                    case Polygon poly:
                        DrawPolygon(poly);
                        break;
                    case MultiPolygon mpoly:
                        for (int i = 0; i < mpoly.NumGeometries; i++)
                        {
                            if (mpoly.GetGeometryN(i) is Polygon p)
                                DrawPolygon(p);
                        }
                        break;
                    case LineString line:
                        DrawLineString(line);
                        break;
                    case MultiLineString mline:
                        for (int i = 0; i < mline.NumGeometries; i++)
                        {
                            if (mline.GetGeometryN(i) is LineString l)
                                DrawLineString(l);
                        }
                        break;
                    case NTSPoint pt:
                        DrawPoint(pt);
                        break;
                    case MultiPoint mpt:
                        for (int i = 0; i < mpt.NumGeometries; i++)
                        {
                            if (mpt.GetGeometryN(i) is NTSPoint p)
                                DrawPoint(p);
                        }
                        break;
                }
            }
        }

        private void DrawPolygon(Polygon poly)
        {
            var path = new Avalonia.Controls.Shapes.Path
            {
                Stroke = Brushes.DarkGreen,
                StrokeThickness = 1,
                Fill = new SolidColorBrush(Color.FromArgb(130, 144, 238, 144)) // semi-transparent light green
            };

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                // Exterior ring
                DrawCoordinateSequenceAsFigure(ctx, poly.ExteriorRing.CoordinateSequence, close: true);
                // Holes
                for (int i = 0; i < poly.NumInteriorRings; i++)
                {
                    DrawCoordinateSequenceAsFigure(ctx, poly.GetInteriorRingN(i).CoordinateSequence, close: true);
                }
            }
            path.Data = geom;
            _shapeCanvas!.Children.Add(path);
        }

        private void DrawLineString(LineString line)
        {
            var path = new Avalonia.Controls.Shapes.Path
            {
                Stroke = Brushes.DarkSlateGray,
                StrokeThickness = 1,
                Fill = null
            };

            var geom = new StreamGeometry();
            using (var ctx = geom.Open())
            {
                DrawCoordinateSequenceAsFigure(ctx, line.CoordinateSequence, close: false);
            }
            path.Data = geom;
            _shapeCanvas!.Children.Add(path);
        }

        private void DrawPoint(NTSPoint point)
        {
            var screen = WorldToScreen(point.Coordinate);
            var radius = Math.Max(2, 2 * _fitScale * 0.001); // size relative to scale
            var ellipse = new Avalonia.Controls.Shapes.Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = Brushes.Maroon,
                Stroke = Brushes.White,
                StrokeThickness = 1
            };
            Canvas.SetLeft(ellipse, screen.X - radius);
            Canvas.SetTop(ellipse, screen.Y - radius);
            _shapeCanvas!.Children.Add(ellipse);
        }

        private void DrawCoordinateSequenceAsFigure(StreamGeometryContext ctx, CoordinateSequence seq, bool close)
        {
            if (seq.Count == 0)
                return;

            var first = WorldToScreen(seq.GetCoordinate(0));
            ctx.BeginFigure(first, isFilled: true);
            for (int i = 1; i < seq.Count; i++)
            {
                var p = WorldToScreen(seq.GetCoordinate(i));
                ctx.LineTo(p);
            }
            ctx.EndFigure(isClosed: close);
        }

        private void ShowShapeFileInfo(string debugInfo = "")
        {
            var infoText = $"Loaded map data from {_currentMapType} directory.\n" +
                          "Pan: Click and drag\nZoom: Mouse wheel\n" +
                          "Use toggles above to switch between country/state data.\n\n";
            
            if (!string.IsNullOrEmpty(debugInfo))
            {
                infoText += $"Debug Information:\n{debugInfo}\n";
            }
            
            if (_shapeCount > 0)
            {
                infoText += $"Successfully loaded {_shapeCount} shapes from shapefile(s).";
            }
            else if (_shapeCount == 0)
            {
                infoText += "No shapes found in shapefile(s).";
            }
            else
            {
                infoText += "Error reading shapefile(s).";
            }
            
            var textBlock = new TextBlock
            {
                Text = infoText,
                Foreground = Brushes.DarkBlue,
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 700
            };
            Canvas.SetLeft(textBlock, 20);
            Canvas.SetTop(textBlock, 20);
            MapCanvas.Children.Add(textBlock);
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