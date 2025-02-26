using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
//using System.Runtime.Intrinsics.Arm;

namespace Snake.Utility;

// Source: https://gigi.nullneuron.net/gigilabs/a-pathfinding-example-in-c/
public static class AStarPathfinding
{
    public class Location
    {
        public int X;
        public int Y;
        public int F;
        public int G;
        public int H;
        public Location Parent = null;

        public Vector2 AsVector()
        {
            return new Vector2(X, Y);
        }
    }

    public static List<Location> GetWalkableAdjacentSquares(
        int x, 
        int y, 
        Func<int, int, bool> isSpaceWalkableFunc
        )
    {
        var proposedLocations = new List<Location>()
        {
            new Location { X = x, Y = y - 1 },
            new Location { X = x, Y = y + 1 },
            new Location { X = x - 1, Y = y },
            new Location { X = x + 1, Y = y },
        };

        return proposedLocations.Where(l => isSpaceWalkableFunc(l.X, l.Y)).ToList();
    }

    static int ComputeHScore(int x, int y, int targetX, int targetY)
    {
        // Manhattan distance
        return Math.Abs(targetX - x) + Math.Abs(targetY - y);
    }

    public static Location GetNextLocationToReachTarget(
        Vector2 startPos, 
        Vector2 targetPos, 
        Func<int, int, bool> isSpaceWalkableFunc)
    {
        Location current = null;
        var start = new Location { X = (int)startPos.X, Y = (int)startPos.Y };
        var target = new Location { X = (int)targetPos.X, Y = (int)targetPos.Y };
        var openList = new List<Location>();
        var closedList = new List<Location>();
        int g = 0;

        // start by adding the original position to the open list
        openList.Add(start);

        while (openList.Count > 0)
        {
            // get the square with the lowest F score
            var lowest = openList.Min(l => l.F);
            current = openList.First(l => l.F == lowest);

            // add the current square to the closed list
            closedList.Add(current);

            // remove it from the open list
            openList.Remove(current);

            // if we added the destination to the closed list, we've found a path
            if (closedList.FirstOrDefault(l => l.X == target.X && l.Y == target.Y) != null)
            {
                break;
            }

            var adjacentSquares = GetWalkableAdjacentSquares(current.X, current.Y, isSpaceWalkableFunc);
            g++;

            foreach(var adjacentSquare in adjacentSquares)
            {
                // if this adjacent square is already in the closed list, ignore it
                if (closedList.FirstOrDefault(l => l.X == adjacentSquare.X
                        && l.Y == adjacentSquare.Y) != null)
                {
                    continue;
                }

                // if it's not in the open list...
                if (openList.FirstOrDefault(l => l.X == adjacentSquare.X
                        && l.Y == adjacentSquare.Y) == null)
                {
                    // compute its score, set the parent
                    adjacentSquare.G = g;
                    adjacentSquare.H = ComputeHScore(adjacentSquare.X, adjacentSquare.Y, target.X, target.Y);
                    adjacentSquare.F = adjacentSquare.G + adjacentSquare.H;
                    adjacentSquare.Parent = current;

                    // and add it to the open list
                    openList.Insert(0, adjacentSquare);
                }
                else // is in open list
                {
                    // test if using the current G score makes the adjacent square's F score
                    // lower, if yes update the parent because it means it's a better path
                    if (g + adjacentSquare.H < adjacentSquare.F)
                    {
                        adjacentSquare.G = g;
                        adjacentSquare.F = adjacentSquare.G + adjacentSquare.H;
                        adjacentSquare.Parent = current;
                    }
                }
            }
        }

        if (current.Parent != null)
        {
            return closedList.Count > 1 ? closedList.ElementAt(1) : closedList.ElementAt(0);
        }
        return null;
    }
}