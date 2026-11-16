using System;
using System.Collections.Generic;

namespace Lifeline
{
    /// <summary>Rotation-only puzzle. The stored route is guaranteed to connect both ports.</summary>
    [Serializable]
    public class PipeRepairPuzzle
    {
        public int columns = 5, rows = 4;
        public int sourceRow, exitRow;
        public int[] masks, solution;
        public int turns;
        public const int North = 1, East = 2, South = 4, West = 8;
        public void Generate(int seed)
        {
            var rng = new Random(seed);
            sourceRow = rng.Next(rows);
            var path = new List<int>();
            int row = sourceRow;
            for (int column = 0; column < columns; column++)
            {
                path.Add(row * columns + column);
                int nextRow = rng.Next(rows);
                if (column == 0 && nextRow == row) nextRow = (row + 2) % rows;
                while (row != nextRow)
                {
                    row += nextRow > row ? 1 : -1;
                    path.Add(row * columns + column);
                }
            }
            exitRow = row;
            solution = new int[columns * rows];
            for (int i = 0; i < solution.Length; i++) solution[i] = rng.Next(2) == 0 ? North | South : North | East;
            for (int i = 0; i < path.Count; i++)
            {
                int current = path[i];
                int incoming = i == 0 ? West : Direction(current, path[i - 1]);
                int outgoing = i == path.Count - 1 ? East : Direction(current, path[i + 1]);
                solution[current] = incoming | outgoing;
            }
            masks = (int[])solution.Clone();
            for (int i = 0; i < masks.Length; i++)
                for (int rotation = rng.Next(4); rotation > 0; rotation--) masks[i] = Rotate(masks[i]);
            turns = 0;
        }
        private int Direction(int from, int to)
        {
            int dx = to % columns - from % columns, dy = to / columns - from / columns;
            return dx > 0 ? East : dx < 0 ? West : dy > 0 ? South : North;
        }
        public static int Rotate(int mask) => ((mask << 1) & 15) | ((mask >> 3) & 1);
        public void RotateTile(int index) { if (masks != null && index >= 0 && index < masks.Length) { masks[index] = Rotate(masks[index]); turns++; } }
        public bool IsSolved()
        {
            if (masks == null || masks.Length != rows * columns) return false;
            int source = sourceRow * columns, target = exitRow * columns + columns - 1;
            if ((masks[source] & West) == 0 || (masks[target] & East) == 0) return false;
            var visited = new bool[masks.Length];
            var queue = new Queue<int>(); queue.Enqueue(source); visited[source] = true;
            int[] dx = { 0, 1, 0, -1 }, dy = { -1, 0, 1, 0 }, bits = { North, East, South, West }, opposite = { South, West, North, East };
            while (queue.Count > 0)
            {
                int index = queue.Dequeue(); if (index == target) return true;
                for (int d = 0; d < 4; d++)
                {
                    int x = index % columns + dx[d], y = index / columns + dy[d];
                    if (x < 0 || x >= columns || y < 0 || y >= rows || (masks[index] & bits[d]) == 0) continue;
                    int next = y * columns + x;
                    if (visited[next] || (masks[next] & opposite[d]) == 0) continue;
                    visited[next] = true; queue.Enqueue(next);
                }
            }
            return false;
        }
        public string TileLabel(int index)
        {
            switch (masks[index])
            {
                case North | South: return "│";
                case East | West: return "─";
                case North | East: return "└";
                case East | South: return "┌";
                case South | West: return "┐";
                case West | North: return "┘";
                default: return "+";
            }
        }
    }
}
