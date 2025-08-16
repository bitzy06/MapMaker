using Avalonia;
using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Tools
{
    /// <summary>
    /// Tool for navigating the map (pan and zoom) - Simplified version
    /// </summary>
    public class NavigateTool : ToolBase
    {
        public override string Name => "Navigate";
        public override string Description => "Pan and zoom the map";

        public override string GetStatusText()
        {
            return "Navigate - Click and drag to pan, mouse wheel to zoom";
        }
    }
}