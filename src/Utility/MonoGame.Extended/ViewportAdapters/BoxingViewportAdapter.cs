using System;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;

namespace MonoGame.Extended.ViewportAdapters
{
    public enum BoxingMode
    {
        None,
        Letterbox,
        Pillarbox
    }

    public class BoxingViewportAdapter : ScalingViewportAdapter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BoxingViewportAdapter" />.
        /// </summary>
        public BoxingViewportAdapter(
            Window window,
            int horizontalBleed = 0, 
            int verticalBleed = 0
            ) : base(window)
        { 
            HorizontalBleed = horizontalBleed;
            VerticalBleed = verticalBleed;
        }

        /// <summary>
        /// Size of horizontal bleed areas (from left and right edges) 
        /// which can be safely cut off, i.e. it's not gameplay-essential.
        /// </summary>
        public int HorizontalBleed { get; }

        /// <summary>
        /// Size of vertical bleed areas (from top and bottom edges) 
        /// which can be safely cut off, i.e. it's not gameplay-essential.
        /// </summary>
        public int VerticalBleed { get; }

        public BoxingMode BoxingMode { get; private set; }

        protected override void OnWindowResize_UpdateViewport(
            uint newWidth, uint newHeight)
        {            
            var worldScaleX = (float)newWidth / GameWidth;
            var worldScaleY = (float)newHeight / GameHeight;

            var safeScaleX = (float)newWidth / (GameWidth - HorizontalBleed);
            var safeScaleY = (float)newHeight / (GameHeight - VerticalBleed);

            var worldScale = Math.Max(worldScaleX, worldScaleY);
            var safeScale = Math.Min(safeScaleX, safeScaleY);
            var scale = Math.Min(worldScale, safeScale);

            // FIXME: Account for scale from DPI scaling?

            var scaledGameWidth = (int)((scale * GameWidth) + 0.5f);
            var scaledGameHeight = (int)((scale * GameHeight) + 0.5f);

            if (scaledGameHeight >= newHeight 
                && scaledGameWidth < newWidth)
            {
                BoxingMode = BoxingMode.Pillarbox;
            }
            else
            {
                if (scaledGameWidth >= newHeight 
                    && scaledGameHeight <= newHeight)
                {
                    BoxingMode = BoxingMode.Letterbox;
                }
                else
                {
                    BoxingMode = BoxingMode.None;
                }
            }

            var x = (newWidth / 2) - (scaledGameWidth / 2);
            var y = (newHeight / 2) - (scaledGameHeight / 2);
            Viewport = new Viewport(x, y, scaledGameWidth, scaledGameHeight);
        }

        public override Vector2 PointToScreen(int x, int y)
        {
            return BasicPointToScreen(
                x - (int)Viewport.X, 
                y - (int)Viewport.Y, 
                GetScaleMatrix()
            );
        }
    }
}