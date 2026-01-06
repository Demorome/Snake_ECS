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

        public Vector2 Scale => new Vector2(
            (float)ViewportWidth / GameWidth,
            (float)ViewportHeight / GameHeight
        );

        public override Matrix4x4 GetScaleMatrix()
        {
            return Matrix4x4.CreateScale(new Vector3(Scale, 1.0f));
        }

        public override Vector2 PointToScreen(int x, int y)
        {
            return BasicPointToScreen(x, y, GetScaleMatrix());
        }

        public override void OnWindowResize_UpdateViewport(
            uint newWidth, uint newHeight)
        {
            Viewport = new Viewport(0, 0, newWidth, newHeight);
        }
    }
}