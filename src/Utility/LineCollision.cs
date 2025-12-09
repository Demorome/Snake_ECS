using System;
using System.Numerics;
using MoonTools.ECS;
using RollAndCash.Components;

namespace RollAndCash.Utility;

public static class LineCollision
{
    public readonly record struct Line(Position2D PointA, Position2D PointB);

    public static Line GetLine(Entity e, World world)
    {
        if (!world.Has<HasLineHitbox>(e))
        {
            throw new Exception("Non-line entity cannot be used here.");
        }

        var pointA = world.Get<Position2D>(e);

        // FIXME: Do I need to normalize here?
        var direction = world.Get<Direction2D>(e).Value;
        var lineLength = world.Get<VisualScale>(e).Scale.X;
        var pointB = pointA + new Position2D(direction * lineLength);

        return new Line(pointA, pointB);
    }


    // Credits to ericleong.me/research/circle-line/ for the "checklinescollide" function
    // This is just a vectorised form of that.
    /*
    public static Position2D? Line_vs_Line(Vector2 lineFirstPoint, Vector2 lineSecondPoint,
                        Vector2 otherLineFirstPoint, Vector2 otherLineSecondPoint)
    {
        float x1 = lineFirstPoint.X;
        float x2 = lineSecondPoint.X;
        float y1 = lineFirstPoint.Y;
        float y2 = lineSecondPoint.Y;

        float x3 = otherLineFirstPoint.X;
        float x4 = otherLineSecondPoint.X;
        float y3 = otherLineFirstPoint.Y;
        float y4 = otherLineSecondPoint.Y;

        float A1 = y2 - y1;
        float B1 = x1 - x2;
        //var AB1 = new Vector2(lineFirstPoint.X - lineSecondPoint.X, lineSecondPoint.Y - lineFirstPoint.Y);

        float C1 = A1 * x1 + B1 * y1;

        float A2 = y4 - y3;
        float B2 = x3 - x4;
        float C2 = A2 * x3 + B2 * y3;
        float det = A1 * B2 - A2 * B1;
        if (det != 0)
        {
            float x = (B2 * C1 - B1 * C2) / det;
            float y = (A1 * C2 - A2 * C1) / det;

            if (x >= Math.Min(x1, x2) && x <= Math.Max(x1, x2)
                && x >= Math.Min(x3, x4) && x <= Math.Max(x3, x4)
                && y >= Math.Min(y1, y2) && y <= Math.Max(y1, y2)
                && y >= Math.Min(y3, y4) && y <= Math.Max(y3, y4)
                )
            {
                return new Position2D(x, y);
            }
        }
        return null;
    }*/

    // Credits to Callum Rogers: https://stackoverflow.com/a/3746601
    // Returns collision point if there is any, null otherwise.
    public static Position2D? Line_vs_Line(Line line, Line otherLine)
    {
        Vector2 b = line.PointB - line.PointA;
        Vector2 d = otherLine.PointB - otherLine.PointA;
        float bDotDPerp = b.X * d.Y - b.Y * d.X;

        // if b dot d == 0, it means the lines are parallel so have infinite intersection points
        if (bDotDPerp == 0)
            return null;

        Vector2 c = otherLine.PointA - line.PointA;
        float t = (c.X * d.Y - c.Y * d.X) / bDotDPerp;
        if (t < 0 || t > 1)
        {
            return null;
        }

        float u = (c.X * b.Y - c.Y * b.X) / bDotDPerp;
        if (u < 0 || u > 1)
        {
            return null;
        }

        return line.PointA + new Position2D(t * b);
    }
}