using System;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;

namespace MonoGame.Extended.ViewportAdapters
{
    public class WindowViewportAdapter : ViewportAdapter
    {

        public WindowViewportAdapter(
            Window window
        ) : base(window)
        { }

        public override Vector2 Scale => Vector2.One;
        public override Matrix4x4 GetScaleMatrix()
        {
            return Matrix4x4.Identity;
        }

        public override Vector2 PointToScreen(int x, int y)
        {
            return BasicPointToScreen(x, y, GetScaleMatrix());
        }

        public override void OnWindowResize_UpdateViewport(uint w, uint h)
        {
            Viewport = new Viewport(0, 0, w, h);
        }
    }
}