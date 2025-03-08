

namespace RollAndCash.Components;

public readonly record struct Timer(float Time, float Max, bool Repeats)
{
    public float RemainingPercentage => Time / Max;
    public Timer(float time, bool repeats = false) : this(time, time, repeats) { }
}
