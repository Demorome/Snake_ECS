

namespace Snake.Components;

public readonly record struct Timer(float Time, float Max, bool Repeats)
{
    public float Remaining => Time / Max;
    public Timer(float time, bool repeats = false) : this(time, time, repeats) { }
}
