using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>A connected cell graph. Walls and loot are built from this same graph.</summary>
    [Serializable]
    public class MazeLayout
    {
        public string name;
        public int columns, rows, entranceCell, rewardCell;
        public float cellSize;
        public Vector2 topLeft;
        public List<Vector2Int> passages = new List<Vector2Int>();
        public List<int> rewardRoute = new List<int>();
        public bool entranceOnLeft, openLeft, openRight;
        public int leftExitRow, rightExitRow;

        public void OpenSidePassages()
        {
            openLeft = openRight = true;
            leftExitRow = rightExitRow = rows / 2;
        }

        public int Count => columns * rows;
        public Vector3 Center(int cell)
        {
            return new Vector3(topLeft.x + (cell % columns + .5f) * cellSize,
                topLeft.y - (cell / columns + .5f) * cellSize, 0);
        }

        public bool IsOpen(int a, int b)
        {
            if (a < 0 || b < 0 || a >= Count || b >= Count) return false;
            foreach (var edge in passages)
                if ((edge.x == a && edge.y == b) || (edge.x == b && edge.y == a)) return true;
            return false;
        }

        public IEnumerable<int> Neighbours(int cell)
        {
            int x = cell % columns, y = cell / columns;
            if (x > 0) yield return cell - 1;
            if (x < columns - 1) yield return cell + 1;
            if (y > 0) yield return cell - columns;
            if (y < rows - 1) yield return cell + columns;
        }

        public static MazeLayout Create(string name, int columns, int rows, float cellSize,
            Vector2 topLeft, System.Random random, float extraPassageChance, bool entranceOnLeft = false)
        {
            var maze = new MazeLayout
            {
                name = name, columns = columns, rows = rows, cellSize = cellSize,
                topLeft = topLeft, entranceOnLeft = entranceOnLeft,
                entranceCell = entranceOnLeft ? columns : columns / 2
            };
            var visited = new bool[maze.Count];
            var stack = new Stack<int>();
            stack.Push(maze.entranceCell);
            visited[maze.entranceCell] = true;
            while (stack.Count > 0)
            {
                int current = stack.Peek();
                var options = new List<int>();
                foreach (int next in maze.Neighbours(current)) if (!visited[next]) options.Add(next);
                if (options.Count == 0) { stack.Pop(); continue; }
                int chosen = options[random.Next(options.Count)];
                maze.passages.Add(new Vector2Int(current, chosen));
                visited[chosen] = true;
                stack.Push(chosen);
            }
            // Occasional loops give alternate routes; the deep cave retains more dead ends.
            for (int a = 0; a < maze.Count; a++)
                foreach (int b in maze.Neighbours(a))
                    if (b > a && !maze.IsOpen(a, b) && random.NextDouble() < extraPassageChance)
                        maze.passages.Add(new Vector2Int(a, b));
            maze.FindRewardRoute();
            return maze;
        }

        public int ReachableCellCount()
        {
            return BreadthFirst(out _).Count;
        }

        List<int> BreadthFirst(out int[] parent)
        {
            parent = new int[Count];
            for (int i = 0; i < parent.Length; i++) parent[i] = -1;
            var queue = new Queue<int>();
            var ordered = new List<int>();
            queue.Enqueue(entranceCell); parent[entranceCell] = entranceCell;
            while (queue.Count > 0)
            {
                int a = queue.Dequeue(); ordered.Add(a);
                foreach (int b in Neighbours(a))
                    if (parent[b] < 0 && IsOpen(a, b)) { parent[b] = a; queue.Enqueue(b); }
            }
            return ordered;
        }

        void FindRewardRoute()
        {
            var ordered = BreadthFirst(out int[] parent);
            rewardCell = ordered[ordered.Count - 1];
            rewardRoute.Clear();
            for (int cell = rewardCell; ; cell = parent[cell])
            {
                rewardRoute.Add(cell);
                if (cell == entranceCell) break;
            }
            rewardRoute.Reverse();
        }
    }
}
