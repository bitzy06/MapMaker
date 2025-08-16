using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using MapMaker.Models;
using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Valid;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Services
{
    /// <summary>
    /// Implementation of geometry service using NetTopologySuite
    /// </summary>
    public class GeometryService : IGeometryService
    {
        public Geometry[] Split(Geometry geometry, LineString splitter)
        {
            // Basic implementation - more sophisticated splitting can be added later
            var result = new List<Geometry>();
            
            if (geometry is Polygon polygon)
            {
                try
                {
                    var difference = geometry.Difference(splitter.Buffer(0.0001));
                    if (difference is MultiPolygon multi)
                    {
                        for (int i = 0; i < multi.NumGeometries; i++)
                        {
                            result.Add(multi.GetGeometryN(i));
                        }
                    }
                    else
                    {
                        result.Add(difference);
                    }
                }
                catch
                {
                    // If splitting fails, return original geometry
                    result.Add(geometry);
                }
            }
            else
            {
                result.Add(geometry);
            }

            return result.ToArray();
        }

        public Geometry Merge(params Geometry[] geometries)
        {
            if (geometries.Length == 0)
                throw new ArgumentException("No geometries provided for merge");

            if (geometries.Length == 1)
                return geometries[0];

            var result = geometries[0];
            for (int i = 1; i < geometries.Length; i++)
            {
                result = result.Union(geometries[i]);
            }

            return result;
        }

        public Geometry Buffer(Geometry geometry, double distance)
        {
            return geometry.Buffer(distance);
        }

        public Geometry Simplify(Geometry geometry, double tolerance)
        {
            return NetTopologySuite.Simplify.DouglasPeuckerSimplifier.Simplify(geometry, tolerance);
        }

        public bool IsValid(Geometry geometry)
        {
            return geometry.IsValid;
        }

        public Geometry MakeValid(Geometry geometry)
        {
            if (geometry.IsValid)
                return geometry;

            // Simple fix using buffer(0) technique
            try
            {
                return geometry.Buffer(0);
            }
            catch
            {
                return geometry;
            }
        }

        public Geometry Intersection(Geometry a, Geometry b)
        {
            return a.Intersection(b);
        }

        public Geometry Union(Geometry a, Geometry b)
        {
            return a.Union(b);
        }

        public Geometry Difference(Geometry a, Geometry b)
        {
            return a.Difference(b);
        }
    }

    /// <summary>
    /// Implementation of undo/redo service using command stack
    /// </summary>
    public class UndoRedoService : IUndoRedoService
    {
        private readonly Stack<IEditorCommand> _undoStack;
        private readonly Stack<IEditorCommand> _redoStack;
        private readonly int _maxCommandHistory;

        public UndoRedoService(int maxCommandHistory = 100)
        {
            _undoStack = new Stack<IEditorCommand>();
            _redoStack = new Stack<IEditorCommand>();
            _maxCommandHistory = maxCommandHistory;
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void Execute(IEditorCommand command)
        {
            command.Do();
            _undoStack.Push(command);
            _redoStack.Clear(); // Clear redo stack when new command is executed

            // Limit history size
            while (_undoStack.Count > _maxCommandHistory)
            {
                var oldest = _undoStack.ToArray()[_maxCommandHistory - 1];
                var temp = new Stack<IEditorCommand>();
                
                // Keep only the most recent commands
                for (int i = 0; i < _maxCommandHistory - 1; i++)
                {
                    temp.Push(_undoStack.Pop());
                }
                
                _undoStack.Clear();
                while (temp.Count > 0)
                {
                    _undoStack.Push(temp.Pop());
                }
            }
        }

        public void Undo()
        {
            if (!CanUndo) return;

            var command = _undoStack.Pop();
            command.Undo();
            _redoStack.Push(command);
        }

        public void Redo()
        {
            if (!CanRedo) return;

            var command = _redoStack.Pop();
            command.Do();
            _undoStack.Push(command);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }

        public string? GetUndoDescription()
        {
            return CanUndo ? _undoStack.Peek().Description : null;
        }

        public string? GetRedoDescription()
        {
            return CanRedo ? _redoStack.Peek().Description : null;
        }
    }

    /// <summary>
    /// Coordinate transform implementation
    /// </summary>
    public class CoordinateTransform : ICoordinateTransform
    {
        private readonly Envelope _worldBounds;
        private readonly Rect _screenBounds;
        private readonly double _zoomLevel;
        private readonly AvaloniaPoint _panOffset;

        public double ZoomLevel => _zoomLevel;

        public CoordinateTransform(Envelope worldBounds, Rect screenBounds, double zoomLevel, AvaloniaPoint panOffset)
        {
            _worldBounds = worldBounds;
            _screenBounds = screenBounds;
            _zoomLevel = zoomLevel;
            _panOffset = panOffset;
        }

        public AvaloniaPoint Transform(Coordinate worldCoord)
        {
            if (_worldBounds.Width <= 0 || _worldBounds.Height <= 0)
                return new AvaloniaPoint(0, 0);

            // Calculate base scale to fit world bounds in screen
            var scaleX = _screenBounds.Width / _worldBounds.Width;
            var scaleY = _screenBounds.Height / _worldBounds.Height;
            var baseScale = Math.Min(scaleX, scaleY);

            // Apply zoom
            var scale = baseScale * _zoomLevel;

            // Transform to screen coordinates
            var x = (worldCoord.X - _worldBounds.MinX) * scale;
            var y = (_worldBounds.MaxY - worldCoord.Y) * scale; // Flip Y

            // Apply centering and pan offset
            var centerX = _screenBounds.Width / 2;
            var centerY = _screenBounds.Height / 2;
            
            x = x - (_worldBounds.Width * scale) / 2 + centerX + _panOffset.X;
            y = y - (_worldBounds.Height * scale) / 2 + centerY + _panOffset.Y;

            return new AvaloniaPoint(x, y);
        }

        public Coordinate Transform(AvaloniaPoint screenPoint)
        {
            if (_worldBounds.Width <= 0 || _worldBounds.Height <= 0)
                return new Coordinate(0, 0);

            // Calculate base scale to fit world bounds in screen
            var scaleX = _screenBounds.Width / _worldBounds.Width;
            var scaleY = _screenBounds.Height / _worldBounds.Height;
            var baseScale = Math.Min(scaleX, scaleY);

            // Apply zoom
            var scale = baseScale * _zoomLevel;

            if (scale <= 0) return new Coordinate(0, 0);

            // Remove centering and pan offset
            var centerX = _screenBounds.Width / 2;
            var centerY = _screenBounds.Height / 2;
            
            var x = screenPoint.X - centerX - _panOffset.X + (_worldBounds.Width * scale) / 2;
            var y = screenPoint.Y - centerY - _panOffset.Y + (_worldBounds.Height * scale) / 2;

            // Transform to world coordinates
            var worldX = x / scale + _worldBounds.MinX;
            var worldY = _worldBounds.MaxY - y / scale; // Flip Y back

            return new Coordinate(worldX, worldY);
        }
    }

    /// <summary>
    /// Base class for editor commands
    /// </summary>
    public abstract class EditorCommandBase : IEditorCommand
    {
        protected EditorCommandBase(string description)
        {
            Description = description;
        }

        public string Description { get; }

        public abstract void Do();
        public abstract void Undo();
    }
}