using System;
using System.Numerics;

namespace RollAndCash.Utility;

public static class RayCollision
{

    static float ray_box_min(float x, float y) {
        return (float.IsNaN(x) || float.IsNaN(y)) ? float.NegativeInfinity : float.Min(x, y);
    }

    static float ray_box_max(float x, float y) {
        return (float.IsNaN(x) || float.IsNaN(y)) ? float.PositiveInfinity : float.Max(x, y);
    }

    static Vector2 ray_box_min(Vector2 a, Vector2 b) {
        return new Vector2(ray_box_min(a.X, b.X), ray_box_min(a.Y, b.Y));
    }

    static Vector2 ray_box_max(Vector2 a, Vector2 b) {
        return new Vector2(ray_box_max(a.X, b.X), ray_box_max(a.Y, b.Y));
    }

    // Assumes t_min <= t_max
    // To get the actual intersection, use t_min, and multiply it with ray dir, adding ray origin. (credits to Bram Stolk)
    // If the ray origin is inside the box (t_min < 0), you need to use t_max instead. (credits to Tavian Barnes)
    static public Vector2 GetIntersectPos(float t_min, float t_max, Vector2 rayOrigin, Vector2 rayDirection)
    {
        var val = (t_min < 0) ? t_max : t_min;
        return (new Vector2(val, val) * rayDirection) + rayOrigin;
    }

    // Credits to Jan Schultke: https://gamedev.stackexchange.com/a/208346
    // Seems to be a version of the popular Slab method: https://tavianator.com/2011/ray_box.html
    /*
    t_min is the furthest entry point
    t_max is the closest exit point

    If t_min <= t_max, the intersection is not a miss.
        If also t_min > 0, the intersection is completely in front.
        If also t_max < 1, the intersection is completely behind.
            If also t_min < 0 && t_max > 0, the ray origin is inside the box.
    If t_min is NaN or t_max is NaN, the ray direction is the zero-vector.
    If t_min == 0, the ray origin lies on the box surface.
    If t_min == t_max, the box has no volume, or an edge or corner was hit. 
        Either way, the intersection is a single point, not a line segment.
    */
    static public (float t_min, float t_max) Intersect(Vector2 origin, Vector2 direction, Vector2 box_min, Vector2 box_max) 
    {
        Vector2 t0 = (box_min - origin) / direction; // TODO: mult by dir_inv instead, for speed
        Vector2 t1 = (box_max - origin) / direction;
        Vector2 min = ray_box_min(t0, t1); // entry points per plane
        Vector2 max = ray_box_max(t0, t1); // exit points per plane
        var t_min = MathF.Max(min.X, min.Y);
        var t_max = MathF.Min(max.X, max.Y);
        return ( t_min, t_max );
    }
}