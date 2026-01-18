using System.Numerics;

namespace RollAndCash;

public static class Dimensions
{
	/// <summary>
	/// The width of the game's virtual rendering resolution. <br/>
	/// NOT the width of the game's world-space!
	/// Check the currently loaded level/room for that. <br/>
	/// May be smaller than the window, especially for pixel-art games,
	/// which need upscaling to be visible on bigger monitors.
	/// </summary>
	public const int VIRTUAL_SCREEN_W = 640;

	/// <summary>
	/// The height of the game's virtual rendering resolution. <br/>
	/// NOT the height of the game's world-space!
	/// Check the currently loaded level/room for that. <br/>
	/// May be smaller than the window, especially for pixel-art games,
	/// which need upscaling to be visible on bigger monitors.
	/// </summary>
	public const int VIRTUAL_SCREEN_H = 360;

	public static Vector2 VIRTUAL_SCREEN_RESOLUTION 
		= new Vector2(VIRTUAL_SCREEN_W, VIRTUAL_SCREEN_H);

	public const int TILE_SIZE = 16;
	public static readonly Vector2 TILE_SIZE_VEC 
		= new(TILE_SIZE, TILE_SIZE);

	// FIXME: Make these based on currently loaded level/room instead!
    public const int TILEGRID_ROWS = VIRTUAL_SCREEN_H / TILE_SIZE;
    public const int TILEGRID_COLUMNS = VIRTUAL_SCREEN_W / TILE_SIZE;
	public static readonly Vector2 TILEGRID_SIZE 
		= new(TILEGRID_ROWS, TILEGRID_COLUMNS);

#if DEBUG
	public const int BATTLE_AREA_W = VIRTUAL_SCREEN_W / 2;
	public const int BATTLE_AREA_H = VIRTUAL_SCREEN_H / 2;
	public const int BATTLE_AREA_THICKNESS = 10;
#endif
}


public static class FontSizes
{
	public const int SCORE = 12;
	//public const int SCORE_STRING = 20;
}

public static class Time
{
	//public const float ROUND_TIME = 90.0f;
}
