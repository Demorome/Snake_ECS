using System.Collections.Generic;
using RollAndCash.Components;
using RollAndCash.Content;
using MoonTools.ECS;
using MoonWorks;
using MoonWorks.Graphics;
using MoonWorks.Graphics.Font;
using System.Numerics;
using RollAndCash.Relations;
using MoonWorks.Storage;
using System;
using RollAndCash.Utility;
using MoonWorks.Math;
using CommandBuffer = MoonWorks.Graphics.CommandBuffer;

namespace RollAndCash;

public class Renderer : MoonTools.ECS.Renderer
{
	GraphicsDevice GraphicsDevice;
	GraphicsPipeline TextPipeline;
	TextBatch TextBatch;
	TriangleBatch TriangleBatch;

	SpriteBatch ArtSpriteBatch;
#if DEBUG
	public static bool DrawDebugColliders = false;
#endif

	Texture RenderTexture;
	Texture DepthTexture;

	Texture SpriteAtlasTexture;

	Sampler PointSampler;

	MoonTools.ECS.Filter DrawRectFilter;
	MoonTools.ECS.Filter TextFilter;
	MoonTools.ECS.Filter SpriteAnimationFilter;
	MoonTools.ECS.Filter DetectionConeFilter;
#if DEBUG
	MoonTools.ECS.Filter ColliderFilter;
#endif

	public Renderer(World world, GraphicsDevice graphicsDevice, TitleStorage titleStorage, TextureFormat swapchainFormat) : base(world)
	{
		GraphicsDevice = graphicsDevice;

		DrawRectFilter = FilterBuilder.Include<Rectangle>().Include<Position2D>().Include<DrawAsRectangle>().Build();
		TextFilter = FilterBuilder.Include<Text>().Include<Position2D>().Build();
		SpriteAnimationFilter = FilterBuilder.Include<SpriteAnimation>().Include<Position2D>().Build();
		DetectionConeFilter = FilterBuilder.Include<CanDetect>().Include<Position2D>().Include<DrawDetectionCone>().Build();
#if DEBUG
		ColliderFilter = FilterBuilder.Include<Rectangle>().Include<Position2D>().Build();
#endif

		RenderTexture = Texture.Create2D(GraphicsDevice, "Render Texture", Dimensions.GAME_W, Dimensions.GAME_H, swapchainFormat, TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);
		DepthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", Dimensions.GAME_W, Dimensions.GAME_H, TextureFormat.D16Unorm, TextureUsageFlags.DepthStencilTarget);

		SpriteAtlasTexture = TextureAtlases.TP_Sprites.Texture;

		TextPipeline = GraphicsPipeline.Create(
			GraphicsDevice,
			new GraphicsPipelineCreateInfo
			{
				TargetInfo = new GraphicsPipelineTargetInfo
				{
					DepthStencilFormat = TextureFormat.D16Unorm,
					HasDepthStencilTarget = true,
					ColorTargetDescriptions =
					[
						new ColorTargetDescription
						{
							Format = swapchainFormat,
							BlendState = ColorTargetBlendState.PremultipliedAlphaBlend
						}
					]
				},
				DepthStencilState = new DepthStencilState
				{
					EnableDepthTest = true,
					EnableDepthWrite = true,
					CompareOp = CompareOp.LessOrEqual
				},
				VertexShader = GraphicsDevice.TextVertexShader,
				FragmentShader = GraphicsDevice.TextFragmentShader,
				VertexInputState = GraphicsDevice.TextVertexInputState,
				RasterizerState = RasterizerState.CCW_CullNone,
				PrimitiveType = PrimitiveType.TriangleList,
				MultisampleState = MultisampleState.None,
				Name = "Text Pipeline"
			}
		);
		TextBatch = new TextBatch(GraphicsDevice);

		PointSampler = Sampler.Create(GraphicsDevice, SamplerCreateInfo.PointClamp);

		ArtSpriteBatch = new SpriteBatch(GraphicsDevice, titleStorage, swapchainFormat, TextureFormat.D16Unorm);

		TriangleBatch = new TriangleBatch(GraphicsDevice, titleStorage, swapchainFormat, TextureFormat.D16Unorm);
	}

	private Color GetColorBlend(Entity e)
	{
		var color = Color.White;
		if (HasOutRelation<ColorBlendOverride>(e))
		{
			// Assumes there would be at most 1 ColorBlendOverride at a time.
			var overridingE = OutRelationSingleton<ColorBlendOverride>(e);
			color = GetRelationData<ColorBlendOverride>(e, overridingE).Color;
		}
		else if (Has<ColorBlend>(e))
		{
			color = Get<ColorBlend>(e).Color;
		}

		if (Has<ColorFlicker>(e))
		{
			var colorFlicker = Get<ColorFlicker>(e);
			if (colorFlicker.ElapsedFrames % 2 == 0)
			{
				color = colorFlicker.Color;
			}
		}

		return color;
	}

	public void Render(CommandBuffer commandBuffer, Texture swapchainTexture, Window window, double alpha)
	{
		ArtSpriteBatch.Start();

		foreach (var entity in DrawRectFilter.Entities)
		{
			var position = Get<Position2D>(entity);
			var rectangle = Get<Rectangle>(entity);
			var orientation = Has<Angle>(entity) ? Get<Angle>(entity).Value : 0.0f;
			var color = GetColorBlend(entity); 
			var depth = -2f;
			if (Has<Depth>(entity))
			{
				depth = -Get<Depth>(entity).Value;
			}

			var sprite = SpriteAnimations.Pixel.Frames[0];
			ArtSpriteBatch.Add(
				new Vector3(position.X + rectangle.X, position.Y + rectangle.Y, depth),
				orientation,
				new Vector2(rectangle.Width, rectangle.Height),
				color,
				sprite.UV.LeftTop,
				sprite.UV.Dimensions
			);
		}

		foreach (var entity in SpriteAnimationFilter.Entities)
		{
			if (HasOutRelation<DontDraw>(entity))
				continue;

			var position = Get<Position2D>(entity);
			var animation = Get<SpriteAnimation>(entity);
			var sprite = animation.CurrentSprite;
			var origin = animation.Origin;
			var depth = -1f;
			var orientation = Has<Angle>(entity) ? Get<Angle>(entity).Value : 0.0f;
			var color = GetColorBlend(entity);

			foreach (var rotationEnforcingEntity in OutRelations<Rotated>(entity))
			{
				var rotationData = GetRelationData<Rotated>(entity, rotationEnforcingEntity);
				orientation += rotationData.Angle;
			}

			Vector2 scale = Vector2.One;
			if (Has<SpriteScale>(entity))
			{
				scale = Get<SpriteScale>(entity).Scale;
			}
			if ((OutRelationCount<FlippedHorizontally>(entity) % 2) == 1)
			{
				scale.X *= -1;
			}
			if ((OutRelationCount<FlippedVertically>(entity) % 2) == 1)
			{
				scale.Y *= -1;
			}
			origin *= scale;

			if (orientation != 0.0f)
			{
				//var rotationMatrix = Matrix3x2.CreateRotation(orientation);
				//origin = Vector2.Transform(origin, rotationMatrix);
				origin = MathUtilities.Rotate(origin, orientation);
			}

			var offset = -origin - new Vector2(sprite.FrameRect.X, sprite.FrameRect.Y) * scale;

			if (Has<Alpha>(entity))
			{
				color.A = Get<Alpha>(entity).Value;
			}

			if (Has<Depth>(entity))
			{
				depth = -Get<Depth>(entity).Value;
			}

			ArtSpriteBatch.Add(
				new Vector3(position.X + offset.X, position.Y + offset.Y, depth),
				orientation,
				new Vector2(sprite.SliceRect.W, sprite.SliceRect.H) * scale,
				color,
				sprite.UV.LeftTop,
				sprite.UV.Dimensions
			);
		}

#if DEBUG
		if (DrawDebugColliders)
		{
			foreach (var entity in ColliderFilter.Entities)
			{
				var position = Get<Position2D>(entity);
				var rect = Get<Rectangle>(entity);
				var orientation = 0.0f;

				// Get a high-contrasting color.
				var color = Has<ColorBlend>(entity) ? Get<ColorBlend>(entity).Color.Inverse() : Color.Red;

				var depth = -2f;
				if (Has<Depth>(entity))
				{
					// Render above the actual entity.
					depth = -Get<Depth>(entity).Value + 1;
				}

				var sprite = SpriteAnimations.Pixel.Frames[0];

				const int lineThickness = 1;
				var horizontalLineSize = new Vector2(rect.Width, lineThickness);
				var verticalLineSize = new Vector2(lineThickness, rect.Height);

				// Horizontal Top
				ArtSpriteBatch.Add(
					new Vector3(position.X + rect.X, position.Y + rect.Y, depth),
					orientation,
					horizontalLineSize,
					color,
					sprite.UV.LeftTop,
					sprite.UV.Dimensions
				);

				// Horizontal Bottom
				ArtSpriteBatch.Add(
					new Vector3(position.X + rect.X, position.Y + rect.Y + rect.Height - 1, depth),
					orientation,
					horizontalLineSize,
					color,
					sprite.UV.LeftTop,
					sprite.UV.Dimensions
				);

				// Vertical Left
				ArtSpriteBatch.Add(
					new Vector3(position.X + rect.X, position.Y + rect.Y, depth),
					orientation,
					verticalLineSize,
					color,
					sprite.UV.LeftTop,
					sprite.UV.Dimensions
				);

				// Vertical Right
				ArtSpriteBatch.Add(
					new Vector3(position.X + rect.X + rect.Width - 1, position.Y + rect.Y, depth),
					orientation,
					verticalLineSize,
					color,
					sprite.UV.LeftTop,
					sprite.UV.Dimensions
				);
			}
		}
#endif

		TextBatch.Start();
		foreach (var entity in TextFilter.Entities)
		{
			if (HasOutRelation<DontDraw>(entity))
				continue;

			var text = Get<Text>(entity);
			var position = Get<Position2D>(entity);

			var str = Data.TextStorage.GetString(text.TextID);
			var font = Fonts.FromID(text.FontID);
			var color = Has<Color>(entity) ? Get<Color>(entity) : Color.White;
			var depth = -1f;

			if (Has<ColorBlend>(entity))
			{
				color = Get<ColorBlend>(entity).Color;
			}

			if (Has<Depth>(entity))
			{
				depth = -Get<Depth>(entity).Value;
			}

			if (Has<TextDropShadow>(entity))
			{
				var dropShadow = Get<TextDropShadow>(entity);

				var dropShadowPosition = position + new Position2D(dropShadow.OffsetX, dropShadow.OffsetY);

				TextBatch.Add(
					font,
					str,
					text.Size,
					Matrix4x4.CreateTranslation(dropShadowPosition.X, dropShadowPosition.Y, depth - 1),
					new Color((byte)0, (byte)0, (byte)0, color.A),
					text.HorizontalAlignment,
					text.VerticalAlignment
				);
			}

			TextBatch.Add(
				font,
				str,
				text.Size,
				Matrix4x4.CreateTranslation(position.X, position.Y, depth),
				color,
				text.HorizontalAlignment,
				text.VerticalAlignment
			);

		}

		TriangleBatch.Start();
		foreach (var entity in DetectionConeFilter.Entities)
		{
			if (HasOutRelation<DontDraw>(entity))
				continue;

			var selfPosition = Get<Position2D>(entity);

			// FIXME: use detection color (alert state?)
			var color = (HasInRelation<Detected>(entity) || Has<ChargingUpAttack>(entity)) ? Color.Red : Color.Green;
			color.A = 100;

			// FIXME: ensure this draws below most entities, but above the ground
			var depth = -10; 

			var selfPosVec = new Vector3(selfPosition.X, selfPosition.Y, depth);
			var colorVec = color.ToVector4();

			var numPoints = OutRelationCount<DetectionVisualPoint>(entity);
			if (numPoints < 2)
			{
				continue;
			}

			var points = OutRelations<DetectionVisualPoint>(entity);
			points.MoveNext(); // points to OOB at the start
			var prevOther = points.Current;
			while (points.MoveNext())
			{
				var other = points.Current;
				var position = Get<Position2D>(other);
				var prevPos = Get<Position2D>(prevOther);

				// FIXME: Order of positions matters for winding order.
				TriangleBatch.AddTriangle(
					colorVec,
					new Vector3(position.X, position.Y, depth),
					new Vector3(prevPos.X, prevPos.Y, depth),
					selfPosVec
				);

				prevOther = other;
			}
		}

		ArtSpriteBatch.Upload(commandBuffer); // Copy and Compute passes happen here!
		TextBatch.UploadBufferData(commandBuffer);
		TriangleBatch.Upload(commandBuffer);

#region RENDER PASS START
		var renderPass = commandBuffer.BeginRenderPass(
			new DepthStencilTargetInfo(DepthTexture, 1, 0),
			new ColorTargetInfo(RenderTexture, Color.Black)
		);

		var viewProjectionMatrices = new ViewProjectionMatrices(GetCameraMatrix(), GetProjectionMatrix());

		if (ArtSpriteBatch.InstanceCount > 0)
		{
			ArtSpriteBatch.Render(renderPass, SpriteAtlasTexture, PointSampler, viewProjectionMatrices);
		}

		if (TriangleBatch.InstanceCount > 0)
		{
			TriangleBatch.Render(renderPass, viewProjectionMatrices);
		}

		renderPass.BindGraphicsPipeline(TextPipeline);
		TextBatch.Render(renderPass, GetCameraMatrix() * GetProjectionMatrix());

		commandBuffer.EndRenderPass(renderPass);
#endregion

		commandBuffer.Blit(RenderTexture, swapchainTexture, MoonWorks.Graphics.Filter.Nearest);
	}

	public Matrix4x4 GetCameraMatrix()
	{
		return Matrix4x4.Identity;
	}

	public Matrix4x4 GetProjectionMatrix()
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