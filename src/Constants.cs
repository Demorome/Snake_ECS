using System.Numerics;

namespace RollAndCash;

public static class Dimensions
{
	/// <summary>
	/// The width of the game-space. <br/>
	/// May be smaller than the window, especially for pixel-art games,
	/// which need upscaling to be visible on bigger monitors.
	/// </summary>
	public const int GAME_W = 640;

	/// <summary>
	/// The height of the game-space. <br/>
	/// May be smaller than the window, especially for pixel-art games,
	/// which need upscaling to be visible on bigger monitors.
	/// </summary>
	public const int GAME_H = 360;

	public static Vector2 GAME_DIMENSIONS = new Vector2(GAME_W, GAME_H);

	public const int TILE_SIZE = 16;
	public static readonly Vector2 TILE_DIMENSIONS 
		= new(TILE_SIZE, TILE_SIZE);

    public const int TILEGRID_ROWS = GAME_H / TILE_SIZE;
    public const int TILEGRID_COLUMNS = GAME_W / TILE_SIZE;
	public static readonly Vector2 TILEGRID_SIZE 
		= new(TILEGRID_ROWS, TILEGRID_COLUMNS);

	public const int BATTLE_AREA_W = GAME_W / 2;
	public const int BATTLE_AREA_H = GAME_H / 2;
	public const int BATTLE_AREA_THICKNESS = 10;
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
