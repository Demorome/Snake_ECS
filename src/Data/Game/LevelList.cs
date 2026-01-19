namespace RollAndCash.Data;

public readonly record struct LevelID(LevelList ID);

public enum LevelList
{
    INVALID = default,

    TestLevel = 1,
}