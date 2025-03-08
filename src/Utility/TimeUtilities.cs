//using System.Numerics;

namespace RollAndCash.Utility;

public static class TimeUtilities
{
    // From ROLL AND CASH, just moved to a more convenient place.
    public static bool OnTime(float time, float triggerTime, float dt, float loopTime)
    {
        if (loopTime == 0)
        {
            return false;
        }

        var t = time % loopTime;
        return (
            (t <= triggerTime && t + dt >= triggerTime) ||
            (t <= triggerTime + loopTime && t + dt >= triggerTime + loopTime)
            );
    }
}