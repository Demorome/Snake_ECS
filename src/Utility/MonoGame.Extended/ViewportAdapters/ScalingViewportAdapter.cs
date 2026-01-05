using System;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;

namespace MonoGame.Extended.ViewportAdapters
{
    public class ScalingViewportAdapter : ViewportAdapter
    {
        public ScalingViewportAdapter(Window window) 
            : base(window)
        { }

        public override Matrix4x4 GetScaleMatrix()
        {
            var scaleX = (float)ViewportWidth / GameWidth;
            var scaleY = (float)ViewportHeight / GameHeight;
            return Matrix4x4.CreateScale(scaleX, scaleY, 1.0f);
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