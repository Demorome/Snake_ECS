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
using MoonWorks.Input;
using RollAndCash.Systems;
using Hexa.NET.ImGui;
using RollAndCash.Editor;
using RollAndCash.Data;
using RollAndCash.Rendering;

namespace RollAndCash;

public class Renderer : MoonTools.ECS.Renderer
{
	GraphicsDevice GraphicsDevice;
	GraphicsPipeline TextPipeline;
	TextBatch TextBatch;
	TriangleBatch TriangleBatch;
	SpriteBatch ArtSpriteBatch;

	// Its size should only change if a TileSet or TileSetVariant is added/removed from the Editor.
	// Otherwise, creating new SpriteBatches could slow things down, unless we really need the GPU space.
	List<(Texture, SpriteBatch)> TileSpriteBatches;

#if DEBUG
	SpriteBatch EditorSpriteBatch;
	EditorSystem EditorSystem;
	public static bool DrawDebugColliders = false;
	TileManipulator TileManipulator;
#endif

	RenderingManipulator RenderingManipulator;

	Texture RenderTexture;
	Texture DepthTexture;

	Texture SpriteAtlasTexture;

	Sampler PointSampler;

	MoonTools.ECS.Filter DrawRectFilter;
	MoonTools.ECS.Filter TextFilter;
	MoonTools.ECS.Filter SpriteAnimationFilter;
	MoonTools.ECS.Filter DetectionConeFilter;
	MoonTools.ECS.Filter TileFilter; 
	// TODO: Support TileAnimations here too!
#if DEBUG
	MoonTools.ECS.Filter ColliderFilter;
#endif

	public Renderer(
		World world,
		GraphicsDevice graphicsDevice,
		TitleStorage titleStorage,
		TextureFormat swapchainFormat,
#if DEBUG
		EditorSystem editorSystem
#endif
		) : base(world)
	{
		GraphicsDevice = graphicsDevice;

		DrawRectFilter = FilterBuilder.Include<Rectangle>().Include<Position2D>().Include<DrawAsRectangle>().Build();
		TextFilter = FilterBuilder.Include<Text>().Include<Position2D>().Build();
		SpriteAnimationFilter = FilterBuilder.Include<SpriteAnimation>().Include<Position2D>().Build();
		DetectionConeFilter = FilterBuilder.Include<CanDetect>().Include<Position2D>().Include<DrawDetectionCone>().Build();
		TileFilter = FilterBuilder.Include<TileID>().Include<Position2D>().Build();

#if DEBUG
		ColliderFilter = FilterBuilder.Include<Rectangle>().Include<Position2D>().Build();
		EditorSystem = editorSystem;
		TileManipulator = new(world);
#endif
		RenderingManipulator = new(world);

		RenderTexture = Texture.Create2D(GraphicsDevice, "Render Texture", Dimensions.GAME_W, Dimensions.GAME_H,
			swapchainFormat,
			TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler
		);

		DepthTexture = Texture.Create2D(GraphicsDevice, "Depth Texture", Dimensions.GAME_W, Dimensions.GAME_H,
			TextureFormat.D16Unorm,
			TextureUsageFlags.DepthStencilTarget
		);

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
#if DEBUG
		EditorSpriteBatch = new SpriteBatch(GraphicsDevice, titleStorage, swapchainFormat, TextureFormat.D16Unorm);
#endif

		TriangleBatch = new TriangleBatch(GraphicsDevice, titleStorage, swapchainFormat, TextureFormat.D16Unorm);

		// TODO: If I have multiple tileset textures, is there a limit to how many batchers I can have at once?
		TileSpriteBatches = new();
		foreach (var (_, tileSet) in TileSets.NameToTileSet)
        {
			TileSpriteBatches.Add(
				new (tileSet.DefaultTexture, 
					new SpriteBatch(GraphicsDevice, titleStorage, swapchainFormat, TextureFormat.D16Unorm)
				)
			);
            
			foreach (TileSetVariant variantTileSet in tileSet.VariantSets)
            {
                if (variantTileSet.Texture != tileSet.DefaultTexture)
                {
                    TileSpriteBatches.Add(
						new (variantTileSet.Texture, 
							new SpriteBatch(GraphicsDevice, titleStorage, swapchainFormat, TextureFormat.D16Unorm)
						)
					);
                }
            }
        }
	}

	public void Render(CommandBuffer commandBuffer, Texture swapchainTexture, Window window, double alpha)
	{
		ArtSpriteBatch.Start();

		foreach (var entity in DrawRectFilter.Entities)
		{
			var rectangle = Get<Rectangle>(entity);

			ArtSpriteBatch.Add(
				RenderingManipulator.GetSpriteInstanceData(
					entity,
					rectangle,
					// TODO: Optimize away the creation of a SpriteAnimation, if needed
					new SpriteAnimation(SpriteAnimations.Pixel)
#if DEBUG
					, EditorSystem
#endif
				)
			);
		}

		#region SPRITE RENDERING
		foreach (var entity in SpriteAnimationFilter.Entities)
		{
			if (HasOutRelation<DontDraw>(entity))
				continue;

			var spriteAnim = Get<SpriteAnimation>(entity);

			ArtSpriteBatch.Add(
				RenderingManipulator.GetSpriteInstanceData(
					entity,
					spriteAnim.CurrentSprite,
					spriteAnim.Origin
#if DEBUG
					, EditorSystem
#endif
				)
			);
		}
		#endregion SPRITE RENDERING

		#region TILE RENDERING
		foreach (var entity in TileFilter.Entities)
        {
            if (HasOutRelation<DontDraw>(entity))
				continue;

			var tileID = Get<TileID>(entity);
        	var tileSprite = TileSprite.FromID(tileID);

			bool found = false;
			foreach (var (texture, batch) in TileSpriteBatches)
            {
				// We shouldn't have many textures to check, so O(n) should be fine.
                if (tileSprite.Texture.Handle == texture.Handle)
                {
                    batch.Add(
						RenderingManipulator.GetSpriteInstanceData(
							entity,
							tileSprite
#if DEBUG
							, EditorSystem
#endif
						)
					);
					found = true;
					break;
                }
            }
			if (!found)
            {
                Logger.LogError($"Couldn't find texture for a tile sprite: {tileID}");
            }
        }
		#endregion TILE RENDERING

		#region TEXT RENDERING
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
			var depth = -(float)DepthLayer.DefaultDepth;

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
		#endregion TEXT RENDERING

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

		#region EDITOR RENDERING
#if DEBUG
		EditorSpriteBatch.Start();

		if (LevelEditorManipulator.IsInLevelEditor && LevelEditorManipulator.ShowGrid)
		{
			var color = new Color(LevelEditorManipulator.GridLineColor);
			var depth = -(float)DepthLayer.Editor_TileOutline; // draw above backgrounds, but nothing else.
			var verticalLength = Dimensions.TILE_ROW_COUNT * Dimensions.TILE_SIZE;
			var horizontalLength = Dimensions.TILE_COLUMN_COUNT * Dimensions.TILE_SIZE;

			// Top line
			var worldPos = TileManipulator.TilePosToWorldPos_TopLeft(0, 0);
			DrawDebugLine(worldPos.AsVector(), horizontalLength, DebugLineThickness, true, color, depth);

			// Left vertical line
			DrawDebugLine(worldPos.AsVector(), verticalLength, DebugLineThickness, false, color, depth);

			// Use twice the thickness since we're technically drawing the line twice.
			for (int col = 1; col < Dimensions.TILE_COLUMN_COUNT; ++col)
			{
				worldPos = TileManipulator.TilePosToWorldPos_TopLeft(col, 0);
				DrawDebugLine(new Vector2(worldPos.X - DebugLineThickness, worldPos.Y),
					verticalLength, DebugLineThickness * 2, false, color, depth
				);
			}

			for (int row = 1; row < Dimensions.TILE_ROW_COUNT; ++row)
			{
				worldPos = TileManipulator.TilePosToWorldPos_TopLeft(0, row);
				DrawDebugLine(new Vector2(worldPos.X, worldPos.Y - DebugLineThickness),
					horizontalLength, DebugLineThickness * 2, true, color, depth
				);
			}

			// Bottom line
			worldPos = TileManipulator.TilePosToWorldPos_TopLeft(0, Dimensions.TILE_ROW_COUNT);
			DrawDebugLine(worldPos.AsVector(), horizontalLength, DebugLineThickness, true, color, depth);

			// Right vertical line
			worldPos = TileManipulator.TilePosToWorldPos_TopLeft(Dimensions.TILE_COLUMN_COUNT, 0);
			DrawDebugLine(worldPos.AsVector(), verticalLength, DebugLineThickness, false, color, depth);

			if (EditorSystem.LevelEditor.HoveredOverTilePosition.HasValue)
			{
				color = Color.White with { A = 200 };
				var tilePos = EditorSystem.LevelEditor.HoveredOverTilePosition.Value;
				worldPos = TileManipulator.TilePosToWorldPos_TopLeft(tilePos);
				var tileRect = new Rectangle(0, 0, Dimensions.TILE_SIZE, Dimensions.TILE_SIZE);
				DrawDebugRectangle(worldPos, tileRect, color, depth, DebugLineThickness);
			}
		}

		if (DrawDebugColliders)
		{
			foreach (var entity in ColliderFilter.Entities)
			{
				var color = Color.Red;
				var depth = 2f;
				if (Has<Depth>(entity))
				{
					// Render above the actual entity.
					depth = -Get<Depth>(entity).Value + 1;
				}
				var rect = Get<Rectangle>(entity);
				DrawDebugRectangle(entity, rect, color, depth, DebugLineThickness);
			}
		}

		// Draw selection mode-related stuff
		{
			var outlineDepth = -(float)DepthLayer.Editor_SelectionOutline;

			var selectedEntity = EditorSystem.GetSelectedEntity();
			if (selectedEntity.HasValue)
			{
				var entity = selectedEntity.Value;
				var rectangle = EditorSystem.GetEntityVisualRect(entity).Value;
				DrawDebugRectangle(entity, rectangle, Color.LimeGreen, outlineDepth, DebugLineThickness * 4);
			}

			// FIXME: Scale color intensity by depth?
			var selectionColor = Color.LimeGreen with { A = 210 };

			if (EditorSystem.IsInEntitySelectionMode)
			{
				foreach (var entity in SpriteAnimationFilter.Entities)
				{
					if (selectedEntity.HasValue && entity == selectedEntity.Value)
					{
						continue;
					}
					if (!EditorSystem.CanEntityBeSelected(entity))
                    {
                        continue;
                    }
					var spriteAnim = Get<SpriteAnimation>(entity);
					var rectangle = EditorSystem.GetEntityVisualRect(entity).Value;
					DrawDebugRectangle(entity, rectangle, selectionColor, outlineDepth, DebugLineThickness);
				}

				foreach (var entity in DrawRectFilter.Entities)
				{
					if (selectedEntity.HasValue && entity == selectedEntity.Value)
					{
						continue;
					}
					if (!EditorSystem.CanEntityBeSelected(entity))
                    {
                        continue;
                    }

					var rect = Get<Rectangle>(entity);
					DrawDebugRectangle(entity, rect, selectionColor, outlineDepth, DebugLineThickness);
				}
			}
		}

		EditorSpriteBatch.Upload(commandBuffer);
#endif
		#endregion EDITOR RENDERING

		ArtSpriteBatch.Upload(commandBuffer); // Copy and Compute passes happen here!
		TextBatch.UploadBufferData(commandBuffer);
		TriangleBatch.Upload(commandBuffer);
		
		foreach (var (texture, batch) in TileSpriteBatches)
        {        
			batch.Upload(commandBuffer);
        }

		#region RENDER PASS
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
#if DEBUG
		if (EditorSpriteBatch.InstanceCount > 0)
		{
			EditorSpriteBatch.Render(renderPass, SpriteAtlasTexture, PointSampler, viewProjectionMatrices);
		}
#endif

		foreach (var (texture, batch) in TileSpriteBatches)
        {        
			if (batch.InstanceCount > 0)
			{
				batch.Render(renderPass, texture, PointSampler, viewProjectionMatrices);
			}
        }

		renderPass.BindGraphicsPipeline(TextPipeline);
		TextBatch.Render(renderPass, GetCameraMatrix() * GetProjectionMatrix());

		commandBuffer.EndRenderPass(renderPass);
		#endregion RENDER PASS

		commandBuffer.Blit(RenderTexture, swapchainTexture, MoonWorks.Graphics.Filter.Nearest);
	}

	// World-to-View matrix
	public Matrix4x4 GetCameraMatrix()
	{
		return Matrix4x4.Identity;
	}

	// View-to-Clip-space matrix
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
	
	
#if DEBUG
	const float DebugLineThickness = 0.3f;

	public void DrawDebugRectangle(Entity entity, Rectangle rect, Color color, float depth, float lineThickness)
	{
		var position = Get<Position2D>(entity);
		DrawDebugRectangle(position, rect, color, depth, lineThickness);
	}

	public void DrawDebugRectangle(Position2D position, Rectangle rect, Color color, float depth, float lineThickness)
	{
		var orientation = 0.0f;

		// Draw a square outline sprite if we can, to reduce sprite count in-editor (may hit limit!)
		// Can't use this for non-square dimensions, since stretching on the sides will be apparent.
		// FIXME: Due to point sampler, the top and left corners get cut off when scaling down too much.
		/*
		if (rect.Height == rect.Width)
		{
			var sprite = SpriteAnimations.EditorTile_Outline.Frames[0];

			EditorSpriteBatch.Add(
				new Vector3(position.X + rect.X, position.Y + rect.Y, depth),
				orientation,
				new Vector2(rect.Width, rect.Height),
				color,
				sprite.UV.LeftTop,
				sprite.UV.Dimensions
			);
		}
		else*/
		{
			var sprite = SpriteAnimations.Pixel.Frames[0];

			var horizontalLineSize = new Vector2(rect.Width, lineThickness);
			var verticalLineSize = new Vector2(lineThickness, rect.Height);

			// Horizontal Top
			EditorSpriteBatch.Add(
				new Vector3(position.X + rect.X, position.Y + rect.Y, depth),
				orientation,
				horizontalLineSize,
				color,
				sprite.UV.LeftTop,
				sprite.UV.Dimensions
			);

			// Horizontal Bottom
			EditorSpriteBatch.Add(
				new Vector3(position.X + rect.X, position.Y + rect.Y + rect.Height - lineThickness, depth),
				orientation,
				horizontalLineSize,
				color,
				sprite.UV.LeftTop,
				sprite.UV.Dimensions
			);

			// Vertical Left
			EditorSpriteBatch.Add(
				new Vector3(position.X + rect.X, position.Y + rect.Y, depth),
				orientation,
				verticalLineSize,
				color,
				sprite.UV.LeftTop,
				sprite.UV.Dimensions
			);

			// Vertical Right
			EditorSpriteBatch.Add(
				new Vector3(position.X + rect.X + rect.Width - lineThickness, position.Y + rect.Y, depth),
				orientation,
				verticalLineSize,
				color,
				sprite.UV.LeftTop,
				sprite.UV.Dimensions
			);
		}
	}
	
	public void DrawDebugLine(
		Vector2 position,
		float length,
		float thickness,
		bool verticalOrHorizontal,
		Color color,
		float depth
		)
	{
		var orientation = 0.0f;
		var sprite = SpriteAnimations.Pixel.Frames[0];

		Vector2 scale;
		if (verticalOrHorizontal == false) // if Vertical
		{
			scale = new Vector2(thickness, length);
		}
		else
		{
			scale = new Vector2(length, thickness);
		}
			
		EditorSpriteBatch.Add(
			new Vector3(position.X, position.Y, depth),
			orientation,
			scale,
			color,
			sprite.UV.LeftTop,
			sprite.UV.Dimensions
		);
	}
#endif
}