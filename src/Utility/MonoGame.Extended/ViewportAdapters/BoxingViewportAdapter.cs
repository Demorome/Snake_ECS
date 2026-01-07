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
            bool forceIntegerScaling
            //uint horizontalBleed = 0, 
            //uint verticalBleed = 0
            ) : base(window)
        { 
            ForceIntegerScaling = forceIntegerScaling;
            //HorizontalBleed = horizontalBleed;
            //VerticalBleed = verticalBleed;
        }

        /// <summary>
        /// Size of horizontal bleed areas (from left and right edges) 
        /// which can be safely cut off, i.e. it's not gameplay-essential.
        /// </summary>
        //public uint HorizontalBleed;

        /// <summary>
        /// Size of vertical bleed areas (from top and bottom edges) 
        /// which can be safely cut off, i.e. it's not gameplay-essential.
        /// </summary>
        //public uint VerticalBleed;

        public bool ForceIntegerScaling;

        public BoxingMode BoxingMode { get; private set; }

        private float _ScaleUniform = float.NaN;
        public override Vector2 Scale 
            => new Vector2(_ScaleUniform, _ScaleUniform);

        public override void OnWindowResize_UpdateViewport(
            uint windowWidth, uint windowHeight)
        {            
            _ScaleUniform = Math.Min(
                (float)windowWidth / GameWidth, 
                (float)windowHeight / GameHeight
            );

            if (ForceIntegerScaling)
            {
                // Enforce integer scaling only if we maintain a minimum scale of 1.
                // Otherwise, allow non-integer scaling 
                // for whatever really small window someone wants to use.
                if (_ScaleUniform > 1f)
                {
                    _ScaleUniform = MathF.Floor(_ScaleUniform);
                }
            }

            // FIXME: Account for scale from DPI scaling?

            // FIXME: Use Vertical/HorizontalBleed, should we want it.
            // Current MonoGame.Extended code is broken:
            // https://github.com/MonoGame-Extended/Monogame-Extended/issues/1086
            // Perhaps using this Nez could would be better:
            // https://github.com/prime31/Nez/blob/master/Nez.Portable/ECS/Scene.cs#L692

            var scaledGameWidth = (int)(_ScaleUniform * GameWidth);
            var scaledGameHeight = (int)(_ScaleUniform * GameHeight);

            if (scaledGameWidth > windowWidth 
                || scaledGameHeight > windowHeight)
            {
                throw new Exception("WRONG BAD exceeded window size!");
            }

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

            if (x < 0 || y < 0)
            {
                throw new Exception("WRONG BAD negative viewport offset!");
            }

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