using MoonTools.ECS;
using MoonWorks.Graphics;
using Rendering;
using System.Numerics;

using RollAndCash.Components;
using RollAndCash.Messages;
using RollAndCash.Relations;
using RollAndCash.Utility;
using RollAndCash.Data;
using MoonWorks;
using RollAndCash.Content;



#if DEBUG
using RollAndCash.Editor;
using RollAndCash.Systems;
#endif

namespace RollAndCash.Rendering;

public class RenderingManipulator : MoonTools.ECS.Manipulator
{
    public RenderingManipulator(World world) : base(world)
    {
    }

    private SpriteInstanceData GetSpriteInstanceData(
        Entity entity,
        Vector2 spriteOrigin,
        Vector2 frameRect,
        Vector2 sliceSize,
        UV UV
#if DEBUG
        , EditorSystem EditorSystem = null
#endif
        )
    {
        var position = Get<Position2D>(entity);

        //== Orientation
        var orientation = Has<Angle>(entity) ? Get<Angle>(entity).ValueInRadians : 0.0f;
        if (Has<RotatesWithDirection>(entity))
        {
            // FIXME: Does Direction2D here need to be SafeNormalized?
            orientation = MathUtilities.AngleFromUnitVector(Get<Direction2D>(entity).Value);
        }
        foreach (var rotationEnforcingEntity in OutRelations<Rotated>(entity))
        {
            var rotationData = GetRelationData<Rotated>(entity, rotationEnforcingEntity);
            orientation += rotationData.Angle;
        }

        //== Scale & flipping
        Vector2 scale = Vector2.One;
        if (Has<VisualScale>(entity))
        {
            scale = Get<VisualScale>(entity).Scale;
        }
        if (((OutRelationCount<FlippedHorizontallyByTarget>(entity)
            + (Has<HorizontalFlip>(entity) ? 1 : 0)) % 2) == 1)
        {
            scale.X *= -1;
        }
        if (((OutRelationCount<FlippedVerticallyByTarget>(entity) 
            + (Has<VerticalFlip>(entity) ? 1 : 0)) % 2) == 1)
        {
            scale.Y *= -1;
        }
        spriteOrigin *= scale;

        // TODO: Allow rotation around an abitrary point in sprite?
        if (orientation != 0.0f)
        {
            //var rotationMatrix = Matrix3x2.CreateRotation(orientation);
            //origin = Vector2.Transform(origin, rotationMatrix);
            spriteOrigin = MathUtilities.Rotate(spriteOrigin, orientation);
        }
        var offset = -spriteOrigin - frameRect * scale;

        //== Color & alpha
        var color = GetColorBlend(
            entity
#if DEBUG
            , EditorSystem
#endif
        );

        if (Has<AlphaOverride>(entity))
        {
            color.A = Get<AlphaOverride>(entity).Value;
        }

        //== Depth
        var depth = -(float)DepthLayer.PlaceholderDepth;
        if (Has<Depth>(entity))
        {
            depth = -Get<Depth>(entity).Value;
        }

        return new SpriteInstanceData(
            new Vector3(position.X + offset.X, position.Y + offset.Y, depth),
            orientation,
            sliceSize * scale,
            color,
            UV.LeftTop,
            UV.Dimensions
        );
    }

    public SpriteInstanceData GetSpriteInstanceData(
        Entity entity,
        Sprite sprite,
        Vector2 spriteOrigin
#if DEBUG
        , EditorSystem EditorSystem = null
#endif
        )
    {
        return GetSpriteInstanceData(
            entity, 
            spriteOrigin, 
            new Vector2(sprite.FrameRect.X, sprite.FrameRect.Y),
            new Vector2(sprite.SliceRect.W, sprite.SliceRect.H),
            sprite.UV
#if DEBUG
            , EditorSystem
#endif
        );
    }

    public SpriteInstanceData GetSpriteInstanceData(
        Entity entity,
        Rectangle rectangle,
        SpriteAnimation pixelAnim
#if DEBUG
        , EditorSystem EditorSystem = null
#endif
        )
    {
        var sprite = pixelAnim.CurrentSprite;
        return GetSpriteInstanceData(
            entity, 
            pixelAnim.Origin, 
            new Vector2(sprite.FrameRect.X, sprite.FrameRect.Y),
            new Vector2(sprite.SliceRect.W, sprite.SliceRect.H) * rectangle.Size,
            sprite.UV
#if DEBUG
            , EditorSystem
#endif
        );
    }

    public SpriteInstanceData GetSpriteInstanceData(
        Entity entity,
        TileSprite tileSprite
#if DEBUG
        , EditorSystem EditorSystem = null
#endif
        )
    {
        var origin = tileSprite.Origin;

        return GetSpriteInstanceData(
            entity, 
            tileSprite.Origin,
            tileSprite.FramePos,
            new Vector2(tileSprite.TileSize, tileSprite.TileSize),
            tileSprite.UV
#if DEBUG
            , EditorSystem
#endif
        );
    }

#if DEBUG
    // Returns a null texture in case of an error.
    public (SpriteInstanceData, Texture) Editor_GetSpriteInstanceDataAndTexture(
        Entity entity, 
        EditorSystem editorSystem = null
        )
    {
        SpriteAnimation? maybeSpriteAnim = null;
        if (Has<DrawAsRectangle>(entity))
        {
            maybeSpriteAnim = new SpriteAnimation(SpriteAnimations.Pixel);
        } 
        else if (Has<SpriteAnimation>(entity))
        {
            maybeSpriteAnim = Get<SpriteAnimation>(entity);
        }

        if (maybeSpriteAnim.HasValue)
        {
            var spriteAnim = maybeSpriteAnim.Value;
            var currentSprite = spriteAnim.CurrentSprite;
            return (
                GetSpriteInstanceData(entity, currentSprite, spriteAnim.Origin, editorSystem), 
                maybeSpriteAnim.Value.CurrentSprite.Texture
            );
        }
        else if (Has<TileID>(entity))
        {
            var tileID = Get<TileID>(entity);
        	var tileSprite = TileSprite.FromID(tileID);
            return (
                GetSpriteInstanceData(entity, tileSprite, editorSystem), 
                tileSprite.Texture
            );
        }

        Logger.LogError("Unable to get sprite instance data / texture!");
        return (default, null);
    }
#endif

    public Color GetColorBlend(
        Entity e
#if DEBUG
        , EditorSystem EditorSystem = null
#endif
        )
	{
		var color = Color.White;
		if (HasOutRelation<ColorBlendOverride>(e))
		{
			// Assumes there would be at most 1 ColorBlendOverride at a time.
			var overridingEntity = OutRelationSingleton<ColorBlendOverride>(e);
			color = GetRelationData<ColorBlendOverride>(e, overridingEntity).Color;
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

#if DEBUG
		// Editor: Make sprite partially transparent if it's not a member of the hovered-over layer.
		if (EditorSystem != null
            && LevelEditorManipulator.IsInLevelEditor 
            && EditorSystem.LevelEditor.ActiveLevel != null
			&& EditorSystem.LevelEditor.HoveredOverLayer != null
            )
		{
			if (Has<LevelRoomID>(e)
				&& Has<Editor_LevelLayerID>(e))
			{
				var entityRoomID = Get<LevelRoomID>(e);
				var entityLayerID = Get<Editor_LevelLayerID>(e);
				if (entityRoomID == EditorSystem.LevelEditor.ActiveRoom.ID
					&& entityLayerID == EditorSystem.LevelEditor.SelectedLayerInList.LayerID)
				{
					return color;
				}
			}
			else
            {
				Logger.LogError($"WTF! Entity {EditorSystem.EntityToString(e)} doesn't have a level layer or RoomID! Components: {EditorSystem.EntityComponentsToString(e)}");
            }

			color = Color.Lerp(color, Color.Transparent, 0.75f);
		}
#endif

		return color;
	}
}