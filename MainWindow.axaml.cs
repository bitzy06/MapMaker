using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using NetTopologySuite.IO;
using MapMaker.Tools;

namespace MapMaker
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, ITool> _tools = new();
        private ITool? _activeTool;
        private readonly List<ToggleButton> _toolButtons = new();
        
        // UI elements
        private ToggleButton? _navigateToolButton;
        private ToggleButton? _selectToolButton;
        private ToggleButton? _moveToolButton;
        private TextBlock? _toolStatusText;

        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            // Get UI elements
            _navigateToolButton = this.FindControl<ToggleButton>("NavigateToolButton");
            _selectToolButton = this.FindControl<ToggleButton>("SelectToolButton");
            _moveToolButton = this.FindControl<ToggleButton>("MoveToolButton");
            _toolStatusText = this.FindControl<TextBlock>("ToolStatusText");
            
            InitializeTools();
            
            // Load the default map (country)
            MapViewerControl.LoadMap("country");
            StatusText.Text = "Country map loaded";

            // Connect the map viewer to the tool system
            MapViewerControl.SetToolManager(this);
        }

        private void InitializeTools()
        {
            // Create tool instances
            _tools["Navigate"] = new NavigateTool();
            _tools["Select"] = new SelectTool();
            _tools["Move"] = new MoveTool();

            // Store tool buttons for easier management
            if (_navigateToolButton != null) _toolButtons.Add(_navigateToolButton);
            if (_selectToolButton != null) _toolButtons.Add(_selectToolButton);
            if (_moveToolButton != null) _toolButtons.Add(_moveToolButton);

            // Set default tool
            SetActiveTool("Navigate");
        }

        private void OnMapTypeChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.IsChecked == true)
            {
                var mapType = radioButton.Content?.ToString()?.ToLower();
                if (mapType != null)
                {
                    MapViewerControl.LoadMap(mapType);
                    StatusText.Text = $"{char.ToUpper(mapType[0])}{mapType.Substring(1)} map loaded";
                }
            }
        }

        private void OnToolSelected(object? sender, RoutedEventArgs e)
        {
            if (sender is ToggleButton button && button.Tag is string toolName)
            {
                // Uncheck all other tool buttons
                foreach (var toolButton in _toolButtons)
                {
                    if (toolButton != button)
                        toolButton.IsChecked = false;
                }

                // Ensure the clicked button stays checked
                button.IsChecked = true;

                SetActiveTool(toolName);
            }
        }

        private void SetActiveTool(string toolName)
        {
            // Deactivate current tool
            _activeTool?.Deactivate();

            // Activate new tool
            if (_tools.TryGetValue(toolName, out var tool))
            {
                _activeTool = tool;
                // Note: We'll create a proper ToolContext when we have the services set up
                // For now, we'll handle this in the MapViewer
                if (_toolStatusText != null)
                    _toolStatusText.Text = tool.GetStatusText();
            }
        }

        public ITool? GetActiveTool()
        {
            return _activeTool;
        }

        private async void OnOutputClicked(object? sender, RoutedEventArgs e)
        {
            try
            {
                StatusText.Text = "Exporting...";
                await Task.Run(() => ExportCurrentMap());
                StatusText.Text = "Export completed successfully!";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Export failed: {ex.Message}";
            }
        }

        private void ExportCurrentMap()
        {
            var currentMapType = MapViewerControl.GetCurrentMapType();
            var shapes = MapViewerControl.GetCurrentShapes();
            
            if (shapes.Count == 0)
            {
                throw new InvalidOperationException("No shapes to export");
            }

            var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "output");
            Directory.CreateDirectory(outputDir);

            var outputPath = Path.Combine(outputDir, $"{currentMapType}_output.shp");
            
            // For now, we'll copy the original files to output directory
            // In the future, this could be enhanced to modify the shapes
            var sourceDir = Path.Combine(Directory.GetCurrentDirectory(), currentMapType);
            if (Directory.Exists(sourceDir))
            {
                var sourceFiles = Directory.GetFiles(sourceDir);
                foreach (var sourceFile in sourceFiles)
                {
                    var fileName = Path.GetFileName(sourceFile);
                    var outputFile = Path.Combine(outputDir, $"{currentMapType}_output{Path.GetExtension(fileName)}");
                    File.Copy(sourceFile, outputFile, overwrite: true);
                }
            }
        }
    }
}