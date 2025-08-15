using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NetTopologySuite.IO;

namespace MapMaker
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Loaded += OnWindowLoaded;
        }

        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            // Load the default map (country)
            MapViewerControl.LoadMap("country");
            StatusText.Text = "Country map loaded";
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