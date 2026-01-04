using System;
using RollAndCash.Components;
using MoonTools.ECS;
using RollAndCash.Editor;
using System.Numerics;
using MoonWorks;
using RollAndCash.Utility;
using MoonWorks.Math;

namespace RollAndCash.Systems;

public class Camera : MoonTools.ECS.System
{
    /// <summary>
    /// Snaps to integer-mults, to maintain pixel-perfect rendering.
    /// The greater the value, the more zoomed out the camera is.
    /// When transitioning to the next integer target, 
    /// this will temporarily *not* be pixel-perfect. A necessary evil,
    /// to smoothen the zooming motion, and probably hardly noticeable.
    /// </summary>
    public static float CurrentZoomOutScale = 1f;
    private static float TargetZoomOutScale = 1f;
    public const float MIN_SCALE = 1f;

    public static Position2D CurrentPosition = default;
    private static Position2D TargetPosition = default;

    private static readonly int PixelsToMovePerSecondWhenAtMaxSpeed 
        = (int)float.Ceiling(PlayerController.MaxPlayerSpeedBase);
    private const int PixelsToMoveBeforeReachingFullSpeed = 8;
    private const int DistanceFromTargetBeforeSlowingDown = 8;
    private static float DistanceTravelled = 0f;

	MoonTools.ECS.Filter CameraFocusFilter;

	public Camera(World world) : base(world)
    {
        CameraFocusFilter = 
            FilterBuilder
            .Include<CameraFocus>()
            .Include<Rectangle>()
            .Build();
    }

	public override void Update(TimeSpan delta)
	{
		var dt = (float)delta.TotalSeconds;

        if (!LevelEditorManipulator.IsInLevelEditor)
        {
            // We're in game-mode.

            // FIXME: Add Some(CameraLock) check for small rooms with a locked camera!

            if (CameraFocusFilter.Count == 0)
            {
                Logger.LogError("No camera focus available!");
                return;
            }

            // Credits to @HappyCoder for the idea of using a bounding box:
            // http://www.gamedev.net/forums/topic/660245-2dmake-a-camera-follow-multiple-characters/5175884/?page=1
            // Camera pos is centered between all camera focuses.
            // Thus, form one large box that includes all focuses,
            // then position the camera to that center.
            var entities = CameraFocusFilter.Entities;
            var firstEntity = entities.Current;
            var cameraRect = Get<Rectangle>(firstEntity);

            if (entities.MoveNext())
            {
                foreach (var entity in entities)
                {
                    var rect = Get<Rectangle>(entity);
                    cameraRect = Rectangle.Union(cameraRect, rect);
                }
            }

            // TODO: Limit camera so that it can't go out of level bounds?
            // This is called edge-snapping: 
            // https://gamedesignskills.com/game-design/camera-design-2d-side-scroller-games/
            // Games like Samurai Gunn avoid it to make the game feel more hectic.
            // Thus, since we're going for a twitchy action-game feel,
            // we won't implement this for now.

            // TODO: Add some padding, so focuses aren't on the edges of the camera borders.

            // Set zoom level.
            if (cameraRect.Width > cameraRect.Height)
            {
                TargetZoomOutScale = cameraRect.Width / (float)Dimensions.GAME_W;
            }
			else
            {
                TargetZoomOutScale = cameraRect.Height / (float)Dimensions.GAME_H;
            }

            if (TargetZoomOutScale < MIN_SCALE)
            {
                TargetZoomOutScale = MIN_SCALE;
            }
            else
            {
                // Snap scale target to an integer, rounding up.
                TargetZoomOutScale = float.Ceiling(TargetZoomOutScale);
            }

            // Set camera target position.
            TargetPosition = new Position2D(cameraRect.Center);
        }
        else
        {
            // We're in editor-mode.
            // Handle smoothing for the editor camera.

            // FIXME: Ignore all CameraFocus game entities:
            // FIXME: focus on the editor's specific camera focus value.
        }

        // Smoothen translation camera motion.
        if (CurrentPosition != TargetPosition)
        {
            var distanceToTarget = CurrentPosition.PixelDistance(TargetPosition);

            // Credits to @Servant of the Lord for the idea of 
            // smoothing camera based on speed:
            // http://www.gamedev.net/forums/topic/673372-easing-my-camera-towards-a-target-that-is-also-moving/5263081/
            
            float maxSpeedMult = float.Max(
                DistanceTravelled / PixelsToMoveBeforeReachingFullSpeed, 
                1.0f
            );
            // TODO: Experiment with easing funcs!
            maxSpeedMult = Easing.InQuad(maxSpeedMult);

            float minSpeedMult = float.Max(
                (float)distanceToTarget / DistanceFromTargetBeforeSlowingDown, 
                1.0f
            );
            minSpeedMult = 1.0f - minSpeedMult;
            // TODO: Experiment with easing funcs!
            minSpeedMult = Easing.OutCubic(minSpeedMult);

            float currentSpeedMult = float.Min(maxSpeedMult, minSpeedMult);

            float speedInPixels = currentSpeedMult * PixelsToMovePerSecondWhenAtMaxSpeed;
            var directionToTarget = MathUtilities.GetHeadingUnitVector(
                CurrentPosition.AsVector(), 
                TargetPosition.AsVector()
            );

            // Update camera position.
            CurrentPosition += speedInPixels * directionToTarget;
            DistanceTravelled += speedInPixels;
        }
        else
        {
            // There's no camera motion to perform, 
            // so reset distance travelled.
            DistanceTravelled = 0f;
        }

        if (CurrentZoomOutScale != TargetZoomOutScale)
        {
            // FIXME: Use some smoothing algorithm.
            CurrentZoomOutScale = TargetZoomOutScale;
        }
	}
}
