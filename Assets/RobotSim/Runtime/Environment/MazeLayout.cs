using System.Collections.Generic;
using UnityEngine;

namespace RobotSim
{
    public enum MazeDirection
    {
        North,
        East,
        South,
        West
    }

    /// <summary>
    /// A perfect maze on a grid (exactly one route between any two cells), generated with a seeded
    /// recursive backtracker. Cell (0,0) is the south-west corner; +y is north.
    /// </summary>
    public class MazeLayout
    {
        public readonly int Width;
        public readonly int Height;

        // eastWalls[x, y]: wall on the east side of cell (x, y). northWalls[x, y]: wall on the north side.
        readonly bool[,] eastWalls;
        readonly bool[,] northWalls;

        public MazeLayout(int width, int height)
        {
            Width = width;
            Height = height;
            eastWalls = new bool[width, height];
            northWalls = new bool[width, height];
            for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                eastWalls[x, y] = true;
                northWalls[x, y] = true;
            }
        }

        public static Vector2Int Step(MazeDirection d) => d switch
        {
            MazeDirection.North => Vector2Int.up,
            MazeDirection.East => Vector2Int.right,
            MazeDirection.South => Vector2Int.down,
            _ => Vector2Int.left
        };

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.y >= 0 && c.x < Width && c.y < Height;

        public bool HasWall(Vector2Int cell, MazeDirection d)
        {
            switch (d)
            {
                case MazeDirection.North: return cell.y >= Height - 1 || northWalls[cell.x, cell.y];
                case MazeDirection.East: return cell.x >= Width - 1 || eastWalls[cell.x, cell.y];
                case MazeDirection.South: return cell.y <= 0 || northWalls[cell.x, cell.y - 1];
                default: return cell.x <= 0 || eastWalls[cell.x - 1, cell.y];
            }
        }

        void RemoveWall(Vector2Int cell, MazeDirection d)
        {
            switch (d)
            {
                case MazeDirection.North: northWalls[cell.x, cell.y] = false; break;
                case MazeDirection.East: eastWalls[cell.x, cell.y] = false; break;
                case MazeDirection.South: northWalls[cell.x, cell.y - 1] = false; break;
                default: eastWalls[cell.x - 1, cell.y] = false; break;
            }
        }

        public static MazeLayout Generate(int width, int height, int seed)
        {
            var maze = new MazeLayout(width, height);
            var rng = new System.Random(seed);
            var visited = new bool[width, height];
            var stack = new Stack<Vector2Int>();
            var options = new List<MazeDirection>(4);

            var start = Vector2Int.zero;
            visited[0, 0] = true;
            stack.Push(start);
            while (stack.Count > 0)
            {
                Vector2Int current = stack.Peek();
                options.Clear();
                for (int i = 0; i < 4; i++)
                {
                    var d = (MazeDirection)i;
                    Vector2Int next = current + Step(d);
                    if (maze.InBounds(next) && !visited[next.x, next.y])
                        options.Add(d);
                }
                if (options.Count == 0)
                {
                    stack.Pop();
                    continue;
                }
                MazeDirection chosen = options[rng.Next(options.Count)];
                maze.RemoveWall(current, chosen);
                Vector2Int target = current + Step(chosen);
                visited[target.x, target.y] = true;
                stack.Push(target);
            }
            return maze;
        }

        /// <summary>Shortest path between two cells (inclusive), or null if unreachable.</summary>
        public List<Vector2Int> FindPath(Vector2Int from, Vector2Int to)
        {
            var previous = new Dictionary<Vector2Int, Vector2Int>();
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(from);
            previous[from] = from;
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                if (c == to) break;
                for (int i = 0; i < 4; i++)
                {
                    var d = (MazeDirection)i;
                    if (HasWall(c, d)) continue;
                    Vector2Int n = c + Step(d);
                    if (previous.ContainsKey(n)) continue;
                    previous[n] = c;
                    queue.Enqueue(n);
                }
            }
            if (!previous.ContainsKey(to)) return null;
            var path = new List<Vector2Int>();
            for (Vector2Int c = to; c != from; c = previous[c]) path.Add(c);
            path.Add(from);
            path.Reverse();
            return path;
        }

        /// <summary>Number of cells reachable from (0,0).</summary>
        public int CountReachable()
        {
            var seen = new HashSet<Vector2Int> { Vector2Int.zero };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(Vector2Int.zero);
            while (queue.Count > 0)
            {
                Vector2Int c = queue.Dequeue();
                for (int i = 0; i < 4; i++)
                {
                    var d = (MazeDirection)i;
                    if (HasWall(c, d)) continue;
                    Vector2Int n = c + Step(d);
                    if (seen.Add(n)) queue.Enqueue(n);
                }
            }
            return seen.Count;
        }
    }
}
