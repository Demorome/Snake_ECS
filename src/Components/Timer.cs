

namespace RollAndCash.Components;

public readonly record struct Timed(float Time, float Max, bool Repeats)
{
    public float RemainingPercentage => Time / Max;
    public Timed(float time, bool repeats = false) : this(time, time, repeats) { }
}
