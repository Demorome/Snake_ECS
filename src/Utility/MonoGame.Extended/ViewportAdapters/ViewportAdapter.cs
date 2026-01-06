using System;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;
using RollAndCash;
using RollAndCash.Components;

namespace MonoGame.Extended.ViewportAdapters
{
    public abstract class ViewportAdapter
    {
        public readonly Window Window;
        public Viewport Viewport;

        public ViewportAdapter(
            Window window
        )
        {
            Window = window;

            // Init viewport.
            OnWindowResize_UpdateViewport(Window.Width, Window.Height);
        }

        public uint ViewportWidth => (uint)Viewport.W;
        public uint ViewportHeight => (uint)Viewport.H;

        public uint GameWidth => Dimensions.GAME_W;
        public uint GameHeight => Dimensions.GAME_H;
        public Rectangle GameRect => 
            new Rectangle(0, 0, (int)GameWidth, (int)GameHeight);
        public Vector2 GameCenter => new Vector2(GameWidth / 2, GameHeight / 2);

        public abstract Matrix4x4 GetScaleMatrix();

        /// <summary>
        /// NEVER use this if you're also using a camera!
        /// </summary>
        public abstract Vector2 PointToScreen(int x, int y);
        /// <summary>
        /// NEVER use this if you're also using a camera!
        /// </summary>
        public Vector2 PointToScreen(Vector2 point)
        {
            return PointToScreen((int)point.X, (int)point.Y);
        }

        protected static Vector2 BasicPointToScreen(
            int x, 
            int y, 
            Matrix4x4 scaleMatrix
        )
        {
            Matrix4x4 invertedMatrix;
            if (!Matrix4x4.Invert(scaleMatrix, out invertedMatrix))
            {
                throw new Exception("Unable to invert matrix!");
            }
            return Vector2.Transform(new Vector2(x, y), invertedMatrix);
        }

        /// <summary>
        /// Avoid using Window.Width/Height: they may be outdated. <br/>
        /// Currently the engine updates it before this func is called,
        /// but that behavior may change if they want to support 
        /// checking the old size, to compare the size change.
        /// </summary>
        public abstract void OnWindowResize_UpdateViewport( 
            uint newWidth, 
            uint newHeight
        );

        protected static void BasicOnWindowResize(
            uint newWidth, 
            uint newHeight,
            ref Viewport viewport
        )
        {
            viewport.W = newWidth;
            viewport.H = newHeight;
        }
    }
}