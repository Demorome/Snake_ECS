using System;
using System.Numerics;
using MoonWorks;
using MoonWorks.Graphics;

namespace MonoGame.Extended.ViewportAdapters
{
    public enum BoxingMode
    {
        /// <summary>
        /// Means the game integer-scaled perfectly to the window.
        /// </summary>
        None,

        Letterbox,
        Pillarbox
    }

    /// <summary>
    /// Used to maintain the same aspect ratio as the virtual game space,
    /// even as we scale up to any arbitrary resolution. <br/>
    /// Useful especially to maintain pixel-perfect rendering 
    /// (i.e. avoid stretched pixels). <br/>
    /// May pad the window with letterboxing / pillarboxing if needed,
    /// namely if the virtual game space doesn't integer-scale up perfectly 
    /// to the window's dimensions.
    /// </summary>
    public class BoxingViewportAdapter : ScalingViewportAdapter
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BoxingViewportAdapter" />.
        /// </summary>
        public BoxingViewportAdapter(
            Window window,
            uint horizontalBleed = 0, 
            uint verticalBleed = 0
            ) : base(window)
        { 
            HorizontalBleed = horizontalBleed;
            VerticalBleed = verticalBleed;
        }

        /// <summary>
        /// Size of horizontal bleed areas (from left and right edges) 
        /// which can be safely cut off, i.e. it's not gameplay-essential.
        /// </summary>
        /// FIXME: Unused!
        public uint HorizontalBleed;

        /// <summary>
        /// Size of vertical bleed areas (from top and bottom edges) 
        /// which can be safely cut off, i.e. it's not gameplay-essential.
        /// </summary>
        /// FIXME: Unused!
        public uint VerticalBleed;

        public BoxingMode BoxingMode { get; private set; }

        public override void OnWindowResize_UpdateViewport(
            uint windowWidth, uint windowHeight)
        {            
            var scale = Math.Min(
                (float)windowWidth / GameWidth, 
                (float)windowHeight / GameHeight
            );

            // FIXME: Account for scale from DPI scaling?

            // FIXME: Use Vertical/HorizontalBleed, should we want it.
            // Current MonoGame.Extended code is broken:
            // https://github.com/MonoGame-Extended/Monogame-Extended/issues/1086
            // Perhaps using this Nez could would be better:
            // https://github.com/prime31/Nez/blob/master/Nez.Portable/ECS/Scene.cs#L692

            var scaledGameWidth = (int)(scale * GameWidth);
            var scaledGameHeight = (int)(scale * GameHeight);

            // FIXME: Determine what Pillarbox vs Letterbox actually means, then fix this code.
            // FIXME: Could probably just use the boxing offsets.
            if (windowHeight > scaledGameHeight
                && windowWidth < scaledGameWidth)
            {
                BoxingMode = BoxingMode.Pillarbox;
            }
            else if (windowHeight > scaledGameWidth
                && windowHeight < scaledGameHeight)
            {
                BoxingMode = BoxingMode.Letterbox;
            }
            else
            {
                BoxingMode = BoxingMode.None;
            }

            // Boxing offsets.
            var x = (windowWidth / 2) - (scaledGameWidth / 2);
            var y = (windowHeight / 2) - (scaledGameHeight / 2);

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