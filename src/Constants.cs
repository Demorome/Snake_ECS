using System.Numerics;

namespace RollAndCash;

public static class Dimensions
{
	public const int GAME_W = 640;
	public const int GAME_H = 360;

	public const int TILE_SIZE = 16;
	public static Vector2 TILE_DIMENSIONS = new Vector2(TILE_SIZE, TILE_SIZE);
    public const int TILE_ROW_COUNT = GAME_H / TILE_SIZE;
    public const int TILE_COLUMN_COUNT = GAME_W / TILE_SIZE;

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
