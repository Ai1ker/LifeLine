using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public partial class WorldGenerator
    {
        public bool IsLargeBuilding(MazeLayout maze)
            => maze != null && maze.Count >= Mathf.Max(1, largeBuildingMinimumRooms);

        void BuildRandomBuildingFeatures(Transform lootRoot)
        {
            for (int mazeIndex = 0; mazeIndex < layouts.Count; mazeIndex++)
            {
                MazeLayout maze = layouts[mazeIndex];
                if (maze.entranceOnLeft) continue;
                bool large = IsLargeBuilding(maze);
                var random = new System.Random(unchecked(generatedSeed ^ mazeIndex * 73856093
                    ^ Mathf.RoundToInt(maze.topLeft.x * 1000) * 19349663
                    ^ Mathf.RoundToInt(maze.topLeft.y * 1000) ^ 0x372bd15));
                List<int> candidates = BuildingFeatureCandidates(maze);
                ShuffleFeatureCells(candidates, random);
                int crateCount = RollFeatureCount(large ? largeBuildingCrateCount : smallBuildingCrateCount, random);
                int hideCount = RollFeatureCount(large ? largeBuildingHideCount : smallBuildingHideCount, random);
                var crateCells = TakeSpacedFeatureCells(maze, candidates, crateCount);
                var hideCells = TakeSpacedFeatureCells(maze, candidates, hideCount);

                var legacyCrates = new List<LootPickup>();
                foreach (LootPickup pickup in pickups)
                    if (pickup != null && pickup.isCrate && pickup.mazeIndex == mazeIndex)
                        legacyCrates.Add(pickup);
                for (int i = 0; i < Mathf.Max(legacyCrates.Count, crateCells.Count); i++)
                {
                    LootPickup crate = i < legacyCrates.Count ? legacyCrates[i] : null;
                    if (i >= crateCells.Count)
                    {
                        // Keep the old generated ID in the lookup so an existing save can restore
                        // its original crate, position and contents even when today's count is one.
                        crate.consumed = true;
                        crate.gameObject.SetActive(false);
                        continue;
                    }
                    int cell = crateCells[i];
                    Vector3 position = maze.Center(cell) + buildingCrateOffset;
                    if (crate == null)
                    {
                        crate = SpawnPickup(lootRoot, position, ResourceKind.Wood, true, false, mazeIndex, cell);
                        crate.saveId = generatedSeed + ":" + activeDay + ":building-feature:crate:" + mazeIndex + ":" + i;
                    }
                    crate.mazeCell = cell;
                    crate.transform.position = position;
                    crate.name = "Random cargo crate / building " + mazeIndex + " / room " + cell;
                }
                foreach (int cell in hideCells)
                {
                    var obj = new GameObject("Random hiding spot / building " + mazeIndex + " / room " + cell);
                    obj.transform.SetParent(generatedRoot, false);
                    obj.transform.position = maze.Center(cell) + buildingHideOffset;
                    var hide = obj.AddComponent<UnderwaterHideSpot>();
                    hide.world = this; hide.mazeIndex = mazeIndex; hide.mazeCell = cell;
                    hideSpots.Add(hide);
                }
            }
        }

        List<int> BuildingFeatureCandidates(MazeLayout maze)
        {
            var result = new List<int>();
            if (maze.Count == 0 || maze.entranceCell < 0 || maze.entranceCell >= maze.Count) return result;
            var visited = new bool[maze.Count];
            var queue = new Queue<int>();
            queue.Enqueue(maze.entranceCell); visited[maze.entranceCell] = true;
            while (queue.Count > 0)
            {
                int cell = queue.Dequeue();
                // Keep the entire crate footprint inside the configured collectible depth.
                if (waterSurface - maze.Center(cell).y + .525f <= maximumLootDepth) result.Add(cell);
                foreach (int next in maze.Neighbours(cell))
                    if (!visited[next] && maze.IsOpen(cell, next))
                    { visited[next] = true; queue.Enqueue(next); }
            }
            return result;
        }

        static int RollFeatureCount(Vector2Int range, System.Random random)
        {
            int minimum = Mathf.Clamp(Mathf.Min(range.x, range.y), 0, 1000);
            int maximum = Mathf.Clamp(Mathf.Max(range.x, range.y), minimum, 1000);
            return random.Next(minimum, maximum + 1);
        }

        static void ShuffleFeatureCells(List<int> cells, System.Random random)
        {
            for (int i = cells.Count - 1; i > 0; i--)
            {
                int other = random.Next(i + 1), cell = cells[i];
                cells[i] = cells[other]; cells[other] = cell;
            }
        }

        static List<int> TakeSpacedFeatureCells(MazeLayout maze, List<int> available, int count)
        {
            var selected = new List<int>();
            while (selected.Count < count && available.Count > 0)
            {
                int chosen = 0, bestSeparation = -1;
                for (int i = 0; i < available.Count; i++)
                {
                    int nearest = int.MaxValue;
                    foreach (int placed in selected)
                        nearest = Mathf.Min(nearest, Mathf.Abs(placed % maze.columns - available[i] % maze.columns)
                            + Mathf.Abs(placed / maze.columns - available[i] / maze.columns));
                    // The pre-shuffled order randomizes the first point and equal-distance ties.
                    if (nearest > bestSeparation) { chosen = i; bestSeparation = nearest; }
                }
                selected.Add(available[chosen]); available.RemoveAt(chosen);
            }
            return selected;
        }
    }
}
