# MapMaker - Economy Sim Map Viewer

MapMaker is a basic map viewer application built with Avalonia UI for viewing and exporting geographic data used in Economy Sim.

## Features

- **Map Type Toggles**: Switch between Country and State/Province level maps
- **Interactive Viewing**: Pan (click & drag) and zoom (mouse wheel) functionality
- **Shapefile Support**: Loads .shp files automatically from designated folders
- **Export Functionality**: Output processed shapefiles to output folder

## Directory Structure

```
MapMaker/
├── country/          # Place country-level shapefiles here (cshapes data)
├── state/           # Place state/province-level shapefiles here (NE admin-1 data)
├── output/          # Exported/processed shapefiles will be saved here
└── Controls/        # MapViewer control implementation
```

## Usage

1. **Place Data Files**: 
   - Put country-level shapefiles (.shp, .dbf, .shx, etc.) in the `country/` folder
   - Put state/province-level shapefiles in the `state/` folder

2. **Run Application**: 
   ```bash
   dotnet run
   ```

3. **Navigate Maps**:
   - Use radio buttons to toggle between Country and State maps
   - Click and drag to pan around the map
   - Use mouse wheel to zoom in/out

4. **Export Data**:
   - Click "Export to Output" button to save current map data to `output/` folder
   - Files are prefixed with map type (e.g., `country_output.shp`)

## Data Sources

This application is designed to work with:
- **cshapes**: Country-level boundary data
- **Natural Earth Admin-1**: State/province-level boundary data

Place the appropriate .shp files in the corresponding folders for automatic loading.

## Dependencies

- **.NET 8.0**: Cross-platform runtime
- **Avalonia UI 11.3.2**: Cross-platform UI framework  
- **NetTopologySuite**: Spatial geometry library
- **NetTopologySuite.IO.ShapeFile**: Shapefile reading/writing

## Building

```bash
dotnet restore
dotnet build
```

## Notes

- The application currently shows placeholders for map rendering
- Actual shapefile geometry rendering can be enhanced in future versions
- Pan and zoom transformations are implemented but simplified for now
- Export functionality copies files with new naming convention