using System;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;

namespace MonoGame.Extended.ViewportAdapters
{
    public class DefaultViewportAdapter : ViewportAdapter
    {
        public DefaultViewportAdapter(Window window) : base(window)
        { }

        public override Matrix4x4 GetScaleMatrix()
        {
            return Matrix4x4.Identity;
        }

        public override Vector2 PointToScreen(int x, int y)
        {
            return BasicPointToScreen(x, y, GetScaleMatrix());
        }

        protected override void OnWindowResize_UpdateViewport(
            uint newWidth, uint newHeight)
        {
            Viewport = new Viewport(0, 0, newWidth, newHeight);
        }
    }
}