using System;
using MoonWorks;
using MoonWorks.Graphics;

namespace RollAndCash;

public abstract class GameState
{
    public abstract void Start();
    public abstract void Update(TimeSpan delta);
    public abstract void Draw(CommandBuffer commandBuffer, Texture swapchainTexture, Window window, double alpha);
    public abstract void End();
}
