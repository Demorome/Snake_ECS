using MoonTools.ECS;

namespace Snake.Relations;

//public readonly record struct Colliding();

public readonly record struct TailPart();

public readonly record struct DontTime();
public readonly record struct DontDraw();
public readonly record struct MovementTimer();
public readonly record struct SpawnEnemyFromFood();
public readonly record struct ChangeStage(int CurrentStage, int NumStages);