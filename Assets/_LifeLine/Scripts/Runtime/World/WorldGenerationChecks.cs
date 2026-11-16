using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Deterministic checks callable from EditMode tests or the Editor console.</summary>
    public static class WorldGenerationChecks
    {
        public static string ValidateMazeSeeds(int firstSeed = -512, int count = 1024)
        {
            int rooms = 0;
            for (int seed = firstSeed; seed < firstSeed + count; seed++)
            {
                var random = new System.Random(seed);
                var mazes = new List<MazeLayout>();
                float[] centers = { 0, 36, 72 }, depths = { 10, 21.5f, 36 }, sizes = { 3, 3, 2.8f };
                int[] columns = { 5, 7, 9 }, rows = { 3, 4, 5 };
                foreach (float center in centers)
                    for (int band = 0; band < 3; band++)
                        mazes.Add(MazeLayout.Create("area " + center + "/band " + band, columns[band], rows[band], sizes[band],
                            new Vector2(center - columns[band] * sizes[band] * .5f, -1.5f - depths[band]), random, .2f));
                var deep = mazes[8];
                float exitY = deep.Center(2 * deep.columns + deep.columns - 1).y;
                mazes.Add(MazeLayout.Create("optional cave", 5, 4, 2.4f,
                    new Vector2(deep.topLeft.x + deep.columns * deep.cellSize + 1, exitY + 3.6f), random, .04f, true));
                foreach (var maze in mazes)
                {
                    if (maze.ReachableCellCount() != maze.Count) Fail(seed, maze, "disconnected graph");
                    if (maze.rewardRoute.Count < 2 || maze.rewardRoute[0] != maze.entranceCell
                        || maze.rewardRoute[maze.rewardRoute.Count - 1] != maze.rewardCell) Fail(seed, maze, "invalid reward route");
                    var unique = new HashSet<int>();
                    for (int i = 0; i < maze.rewardRoute.Count; i++)
                    {
                        if (!unique.Add(maze.rewardRoute[i])) Fail(seed, maze, "cyclic reward route");
                        if (i > 0 && !maze.IsOpen(maze.rewardRoute[i - 1], maze.rewardRoute[i])) Fail(seed, maze, "reward route crosses a wall");
                    }
                    for (int cell = 0; cell < maze.Count; cell++)
                    {
                        Vector3 position = maze.Center(cell);
                        Vector3 anchor = new Vector3(Mathf.Clamp(position.x, 0, 90), -2.2f, 0);
                        if (Vector3.Distance(position, anchor) + maze.cellSize * .5f > 54) Fail(seed, maze, "room exceeds final 54m rope");
                    }
                    if (maze.topLeft.x < -22 || maze.topLeft.x + maze.columns * maze.cellSize > 100) Fail(seed, maze, "architecture outside horizontal world bounds");
                    if (-1.5f - (maze.topLeft.y - maze.rows * maze.cellSize) > 50.01f) Fail(seed, maze, "architecture deeper than50m");
                    rooms += maze.Count;
                }
                if (WorldGenerator.ComputeDaySeed(seed, 1) == WorldGenerator.ComputeDaySeed(seed, 2))
                    throw new InvalidOperationException("Daily seed does not change");
            }
            return "PASS (pure graph): " + count + " seeds / " + count * 10 + " connected mazes / " + rooms
                + " rooms; all three sea areas span10–50m, reward routes follow open portals, bounds<=100m, final rope<=54m. No Unity physics claim.";
        }

        public static string RunGeneratedWorldChecks() => WorldRulesValidation.Run();

        static void Fail(int seed, MazeLayout maze, string reason)
        {
            throw new InvalidOperationException("Seed " + seed + " / " + maze.name + ": " + reason);
        }
    }
}
