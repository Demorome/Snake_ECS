using System;
using System.Numerics;
using System.Xml;
using RollAndCash.Components;

namespace RollAndCash.Utility;

public static class RayCollision
{

    static float ray_box_min(float x, float y)
    {
        return (float.IsNaN(x) || float.IsNaN(y)) ? float.NegativeInfinity : float.Min(x, y);
    }

    static float ray_box_max(float x, float y)
    {
        return (float.IsNaN(x) || float.IsNaN(y)) ? float.PositiveInfinity : float.Max(x, y);
    }

    static Vector2 ray_box_min(Vector2 a, Vector2 b)
    {
        return new Vector2(ray_box_min(a.X, b.X), ray_box_min(a.Y, b.Y));
    }

    static Vector2 ray_box_max(Vector2 a, Vector2 b)
    {
        return new Vector2(ray_box_max(a.X, b.X), ray_box_max(a.Y, b.Y));
    }

    // Credits to Jan Schultke: https://gamedev.stackexchange.com/a/208346
    // Seems to be a version of the popular Slab method: https://tavianator.com/2011/ray_box.html
    // Seems to only work give results as though our ray was of infinite length, so it's useless for us.
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
    /*
    static public (bool hit, Vector2 hitPos) Intersect(
        Vector2 rayOrigin, Vector2 rayDirection,
        Rectangle AABB)
    {
        Vector2 t0 = (AABB.TopLeft() - rayOrigin) / rayDirection; // TODO: mult by dir_inv instead, for speed
        Vector2 t1 = (AABB.BottomRight() - rayOrigin) / rayDirection;

        Vector2 min = ray_box_min(t0, t1); // entry points per plane
        Vector2 max = ray_box_max(t0, t1); // exit points per plane

        var t_min = MathF.Max(min.X, min.Y);
        var t_max = MathF.Min(max.X, max.Y);

        bool hit = t_min <= t_max;
        if (hit)
        {
            float ix = rayOrigin.X + rayDirection.X * t_min;
            float iy = rayOrigin.Y + rayDirection.Y * t_min;
            return (true, new Vector2(ix, iy));
        }
        else
        {
            return (false, new Vector2(float.NaN, float.NaN));
        }
    }*/

    /*
    // From https://gdbooks.gitbooks.io/3dcollisions/content/Chapter3/raycast_aabb.html
    static public (float t_min, float t_max) Intersect(
        Vector2 origin, Vector2 direction, 
        Vector2 box_min, Vector2 box_max) 
    {
        float t1 = (box_min.X - origin.X) / direction.X;
        float t2 = (box_max.X - origin.X) / direction.X;
        float t3 = (box_min.Y - origin.Y) / direction.Y;
        float t4 = (box_max.Y - origin.Y) / direction.Y;
        
        float t_min = MathF.Max(MathF.Min(t1, t2), MathF.Min(t3, t4));
        float t_max = MathF.Min(MathF.Max(t1, t2), MathF.Max(t3, t4));

        // if tmax < 0, ray (line) is intersecting AABB, but whole AABB is behing us
        return ( t_min, t_max );
    }*/

    // TODO: Implement https://noonat.github.io/intersect/ 's "AABB vs Segment" and compare performance.

    // From the book 'Real-Time Collision Detection' by Christer Ericson, slightly tweaked.
    static public (bool hit, Vector2 hitPos) Intersect(
        Vector2 rayOrigin, Vector2 rayDirection, Vector2 invRayDir,
        Rectangle AABB)
    {
        float min = 0;
        float max = 1;

        float t0;
        float t1;

        // Left and right sides.
        // - If the line is parallel to the y axis.
        if (rayDirection.X == 0)
        {
            if (rayOrigin.X < AABB.Left || rayOrigin.X > AABB.Right)
            {
                return (false, new Vector2(float.NaN, float.NaN));
            }
        }
        // - Make sure t0 holds the smaller value by checking the direction of the line.
        else
        {
            if (rayDirection.X > 0)
            {
                t0 = (AABB.Left - rayOrigin.X) * invRayDir.X;
                t1 = (AABB.Right - rayOrigin.X) * invRayDir.X;
            }
            else
            {
                t1 = (AABB.Left - rayOrigin.X) * invRayDir.X;
                t0 = (AABB.Right - rayOrigin.X) * invRayDir.X;
            }

            min = MathF.Max(min, t0); //if (t0 > min) min = t0;
            max = MathF.Min(max, t1); // if (t1 < max) max = t1;
            if (min > max || max < 0)
            {
                return (false, new Vector2(float.NaN, float.NaN));
            }
        }

        // The top and bottom side.
        // - If the line is parallel to the x axis.
        if (rayDirection.Y == 0)
        {
            if (rayOrigin.Y < AABB.Top || rayOrigin.Y > AABB.Bottom)
            {
                return (false, new Vector2(float.NaN, float.NaN));
            }
        }
        // - Make sure t0 holds the smaller value by checking the direction of the line.
        else
        {
            if (rayDirection.Y > 0)
            {
                t0 = (AABB.Top - rayOrigin.Y) * invRayDir.Y;
                t1 = (AABB.Bottom - rayOrigin.Y) * invRayDir.Y;
            }
            else
            {
                t1 = (AABB.Top - rayOrigin.Y) * invRayDir.Y;
                t0 = (AABB.Bottom - rayOrigin.Y) * invRayDir.Y;
            }

            min = MathF.Max(min, t0); //if (t0 > min) min = t0;
            max = MathF.Min(max, t1); // if (t1 < max) max = t1;
            if (min > max || max < 0)
            {
                return (false, new Vector2(float.NaN, float.NaN));
            }
        }

        // The point of intersection
        float ix = rayOrigin.X + rayDirection.X * min;
        float iy = rayOrigin.Y + rayDirection.Y * min;
        return (true, new Vector2(ix, iy));
    }

    // Credits to Jeroen Baert: https://gamedev.stackexchange.com/a/24464
    /*
    static public (bool hit, float t_min, float t_max) Intersect(
        Vector2 rayOrigin, Vector2 rayDirection, 
        Vector2 box_min, Vector2 box_max) 
    {
        Vector2 T_1 = new Vector2(); // vectors to hold the T-values for every direction
        Vector2 T_2 = new Vector2();
        
        float t_near = -float.MaxValue;
        float t_far = float.MaxValue;

        for (int i = 0; i < 2; i++){ //we test slabs in every direction
            if (rayDirection[i] == 0){ // ray parallel to planes in this direction
                if ((rayOrigin[i] < box_min[i]) || (rayOrigin[i] > box_max[i])) {
                    return ( false, t_near, t_far ); // parallel AND outside box : no intersection possible
                }
            } else { // ray not parallel to planes in this direction
                T_1[i] = (box_min[i] - rayOrigin[i]) / rayDirection[i];
                T_2[i] = (box_max[i] - rayOrigin[i]) / rayDirection[i];

                if(T_1[i] > T_2[i]){ // we want T_1 to hold values for intersection with near plane
                    (T_1, T_2) = (T_2, T_1); // swap
                }
                if (T_1[i] > t_near){
                    t_near = T_1[i];
                }
                if (T_2[i] < t_far){
                    t_far = T_2[i];
                }
                if( (t_near > t_far) || (t_far < 0) ){
                    return ( false, t_near, t_far );
                }
            }
        }
        return ( true, t_near, t_far ); // if we made it here, there was an intersection - YAY
    }*/

    // Assumes t_min <= t_max
    // To get the actual intersection, use t_min, and multiply it with ray dir, adding ray origin. (credits to Bram Stolk)
    // If the ray origin is inside the box (t_min < 0), you need to use t_max instead. (credits to Tavian Barnes)
    // See discussion at https://tavianator.com/2011/ray_box.html
    /*
    static public Vector2 GetIntersectPos(float t_min, float t_max, Vector2 rayOrigin, Vector2 normalizedRayDir)
    {
        var val = (t_min < 0) ? t_max : t_min;
        return new Vector2(val, val) * normalizedRayDir;

        //var rotationMatrix = Matrix3x2.CreateRotation(orientation);
        //origin = Vector2.Transform(origin, rotationMatrix);
    }*/
}