using Avalonia;
using Avalonia.Input;
using AvaloniaPoint = Avalonia.Point;

namespace MapMaker.Tools
{
    /// <summary>
    /// Simplified tool interface for the current system
    /// </summary>
    public interface ITool
    {
        string Name { get; }
        string Description { get; }
        bool IsActive { get; }

        void Activate();
        void Deactivate();
        void OnMouseDown(PointerPressedEventArgs e);
        void OnMouseMove(PointerEventArgs e);
        void OnMouseUp(PointerReleasedEventArgs e);
        void OnKeyDown(KeyEventArgs e);
        void OnKeyUp(KeyEventArgs e);
        string GetStatusText();
    }

    /// <summary>
    /// Base class for tools
    /// </summary>
    public abstract class ToolBase : ITool
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public bool IsActive { get; private set; }

        public virtual void Activate()
        {
            IsActive = true;
        }

        public virtual void Deactivate()
        {
            IsActive = false;
        }

        public virtual void OnMouseDown(PointerPressedEventArgs e) { }
        public virtual void OnMouseMove(PointerEventArgs e) { }
        public virtual void OnMouseUp(PointerReleasedEventArgs e) { }
        public virtual void OnKeyDown(KeyEventArgs e) { }
        public virtual void OnKeyUp(KeyEventArgs e) { }
        public virtual string GetStatusText() { return $"{Name} tool active"; }
    }
}