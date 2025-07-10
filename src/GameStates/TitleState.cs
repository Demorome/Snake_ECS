using System;
using System.IO;
using MoonWorks;
using MoonWorks.Audio;
using MoonWorks.Graphics;
using RollAndCash.Content;
using RollAndCash.Utility;
using System.Numerics;

namespace RollAndCash.GameStates;

public class TitleState : GameState
{
    RollAndCashGame Game;
    GraphicsDevice GraphicsDevice;
    AudioDevice AudioDevice;
    GameState TransitionStateA;
    GameState TransitionStateB;

    SpriteBatch HiResSpriteBatch;
    Texture RenderTexture;
    Sampler LinearSampler;

    PersistentVoice MusicVoice;
    AudioDataQoa Music;

    float Time = 30.0f;
    private float Timer = 0.0f;

    public TitleState(RollAndCashGame game, GameState transitionStateA, GameState transitionStateB)
    {
        Game = game;
        GraphicsDevice = game.GraphicsDevice;
        AudioDevice = game.AudioDevice;
        TransitionStateA = transitionStateA;
        TransitionStateB = transitionStateB;

        LinearSampler = Sampler.Create(GraphicsDevice, SamplerCreateInfo.LinearClamp);
        HiResSpriteBatch = new SpriteBatch(GraphicsDevice, Game.RootTitleStorage, game.MainWindow.SwapchainFormat);

        RenderTexture = Texture.Create2D(GraphicsDevice, Dimensions.GAME_W, Dimensions.GAME_H, game.MainWindow.SwapchainFormat, TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);
    }

    public override void Start()
    {
        if (MusicVoice == null)
        {
            Music = StreamingAudio.Lookup(StreamingAudio.roll_n_cash_grocery_lords);
            Music.Loop = true;
            MusicVoice = AudioDevice.Obtain<PersistentVoice>(Music.Format);
        }

        Music.Seek(0);
        Music.SendTo(MusicVoice);
        MusicVoice.Play();

        var announcerSound = StaticAudio.Lookup(StaticAudio.RollAndCash);
        var announcerVoice = AudioDevice.Obtain<TransientVoice>(announcerSound.Format);
        announcerVoice.Submit(announcerSound);
        announcerVoice.SetVolume(1.8f);
        announcerVoice.Play();
    }

    public override void Update(TimeSpan delta)
    {
        Timer += (float)delta.TotalSeconds;

        if (Game.Inputs.AnyPressed)
        {
            Game.SetState(TransitionStateB);
        }
        else if (Timer >= Time)
        {
            Timer = 0.0f;
            Game.SetState(TransitionStateA);
        }
    }

    public void SetTransitionStateB(GameState state)
    {
        TransitionStateB = state;
    }

    public override void Draw(CommandBuffer commandBuffer, Texture swapchainTexture, Window window, double alpha)
    {
        var logoPosition = new Components.Position2D(Rando.Range(-1, 1), Rando.Range(-1, 1));

        HiResSpriteBatch.Start();

        var logoAnimation = SpriteAnimations.Screen_Title;
        var sprite = logoAnimation.Frames[0];
        HiResSpriteBatch.Add(
            new Vector3(logoPosition.X, logoPosition.Y, -1f),
            0,
            new Vector2(sprite.SliceRect.W, sprite.SliceRect.H),
            Color.White,
            sprite.UV.LeftTop, sprite.UV.Dimensions
        );

        HiResSpriteBatch.Upload(commandBuffer);

        var renderPass = commandBuffer.BeginRenderPass(new ColorTargetInfo(RenderTexture, Color.Black));

        var hiResViewProjectionMatrices = new ViewProjectionMatrices(Matrix4x4.Identity, GetHiResProjectionMatrix());

        HiResSpriteBatch.Render(
            renderPass,
            TextureAtlases.TP_HiRes.Texture,
            LinearSampler,
            hiResViewProjectionMatrices
        );

        commandBuffer.EndRenderPass(renderPass);

        commandBuffer.Blit(RenderTexture, swapchainTexture, Filter.Nearest, false);
    }

    public override void End()
    {
        Music.Disconnect();
    }

    private Matrix4x4 GetHiResProjectionMatrix()
    {
        return Matrix4x4.CreateOrthographicOffCenter(
            0,
            Dimensions.GAME_W,
            Dimensions.GAME_H,
            0,
            0.01f,
            1000
        );
    }
}
