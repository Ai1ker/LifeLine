using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class BuildingFeatureSpawnTests
    {
        GameObject root;
        GameTuning tuning;
        WorldGenerator world;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Random building features fixture");
            tuning = ScriptableObject.CreateInstance<GameTuning>();
            tuning.maximumSharks = 0;
            world = root.AddComponent<WorldGenerator>();
            world.tuning = tuning; world.drawLabels = false; world.geometryLayer = 20;
            world.generateCaves = false; world.filterSalvageChance = 0;
        }

        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(tuning);
        }

        [TestCase(8417)]
        [TestCase(2718)]
        [TestCase(104731)]
        public void EveryBuildingHasItsRequiredRandomFeatureCountsInDistinctReachableRooms(int seed)
        {
            world.generateCaves = world.forceCave = seed == 8417;
            world.Generate(seed);
            Physics.SyncTransforms();
            var crates = ActiveCrates();
            int small = 0, large = 0;
            for (int index = 0; index < world.layouts.Count; index++)
            {
                var maze = world.layouts[index];
                var localCrates = crates.Where(p => p.mazeIndex == index).ToArray();
                var hides = world.hideSpots.Where(h => h.mazeIndex == index).ToArray();
                if (maze.entranceOnLeft)
                {
                    Assert.That(localCrates, Is.Empty, "Caves retain their dedicated reward layout.");
                    Assert.That(hides, Is.Empty, "Caves do not receive ordinary building hiding spots.");
                    continue;
                }
                bool isLarge = maze.Count >= 24;
                if (isLarge) large++; else small++;
                Assert.That(localCrates.Length, Is.InRange(isLarge ? 2 : 1, isLarge ? 4 : 2), maze.name);
                Assert.That(hides.Length, Is.InRange(isLarge ? 3 : 2, isLarge ? 5 : 3), maze.name);
                Assert.That(maze.ReachableCellCount(), Is.EqualTo(maze.Count), maze.name);
                var featureCells = new List<int>();
                foreach (var crate in localCrates)
                {
                    Assert.That(crate.contents, Is.Empty, "Random generation must not pre-fill player cargo.");
                    CheckPlacement(maze, crate.mazeCell, crate.transform.position);
                    featureCells.Add(crate.mazeCell);
                }
                foreach (var hide in hides)
                {
                    Assert.That(hide.world, Is.SameAs(world));
                    CheckPlacement(maze, hide.mazeCell, hide.transform.position);
                    featureCells.Add(hide.mazeCell);
                }
                Assert.That(featureCells.Distinct().Count(), Is.EqualTo(featureCells.Count),
                    maze.name + ": crates and hiding spots must use separate rooms.");
            }
            Assert.That(small, Is.EqualTo(11), "All route-coverage buildings participate in the small-building rule.");
            Assert.That(large, Is.EqualTo(15));
            Assert.That(crates.Any(p => p.mazeCell != world.layouts[p.mazeIndex].entranceCell
                && p.mazeCell != world.layouts[p.mazeIndex].rewardCell), Is.True,
                "Cargo is sampled from additional rooms, rather than only the two legacy locations.");
            Assert.That(world.hideSpots.Any(h => h.mazeCell != 0), Is.True,
                "Hiding spots are no longer fixed to the first room.");
            Assert.That(world.pickups.Select(p => p.saveId).Distinct().Count(), Is.EqualTo(world.pickups.Count));
            if (world.forceCave)
            {
                Assert.That(world.generatedCave, Is.True);
                Assert.That(world.ValidateGeneratedWorld(out string report), Is.True, report);
            }
        }

        [Test] public void SameSeedAndDayReproducePlacementsWhileTheNextDaySamplesNewRooms()
        {
            world.extendRouteCoverage = false;
            world.Generate(8417);
            string dayOne = FeatureFingerprint();
            world.Generate(8417);
            Assert.That(FeatureFingerprint(), Is.EqualTo(dayOne));
            world.RefreshForDay(2);
            string dayTwo = FeatureFingerprint();
            Assert.That(dayTwo, Is.Not.EqualTo(dayOne), "Compare room selections, not merely day-dependent save IDs.");
            world.RefreshForDay(2);
            Assert.That(FeatureFingerprint(), Is.EqualTo(dayTwo));
        }

        [Test] public void FeatureCountChangesDoNotShiftOrdinaryLootOrMazeAndEnemyRandomStreams()
        {
            world.generateCaves = world.forceCave = true;
            world.smallBuildingHideCount = world.largeBuildingHideCount = Vector2Int.zero;
            world.smallBuildingCrateCount = world.largeBuildingCrateCount = Vector2Int.zero;
            world.Generate(8417);
            var ordinary = OrdinaryLootFingerprint();
            var graphs = world.layouts.Select(m => string.Join(";", m.passages.Select(p => p.ToString()))).ToArray();
            var gateIds = world.trapdoors.Select(g => g.saveId).ToArray();
            var sharks = world.sharkSpawnPoints.ToArray();
            var piranhas = world.piranhaSpawnPoints.ToArray();
            bool cave = world.generatedCave, eel = world.generatedEel;

            world.smallBuildingHideCount = new Vector2Int(2, 3);
            world.largeBuildingHideCount = new Vector2Int(3, 5);
            world.smallBuildingCrateCount = new Vector2Int(1, 2);
            world.largeBuildingCrateCount = new Vector2Int(2, 4);
            world.Generate(8417);
            Assert.That(OrdinaryLootFingerprint(), Is.EqualTo(ordinary), "Resource IDs, positions, types and cabinet contents stay compatible.");
            Assert.That(world.layouts.Select(m => string.Join(";", m.passages.Select(p => p.ToString()))), Is.EqualTo(graphs));
            Assert.That(world.trapdoors.Select(g => g.saveId), Is.EqualTo(gateIds));
            Assert.That(world.sharkSpawnPoints, Is.EqualTo(sharks));
            Assert.That(world.piranhaSpawnPoints, Is.EqualTo(piranhas));
            Assert.That(world.generatedCave, Is.EqualTo(cave)); Assert.That(world.generatedEel, Is.EqualTo(eel));
            Assert.That(world.hideSpots.Count, Is.GreaterThan(0)); Assert.That(ActiveCrates().Length, Is.GreaterThan(0));
        }

        [Test] public void DevelopersCanChangeBuildingSizeThresholdAndCountRanges()
        {
            world.extendRouteCoverage = false;
            world.largeBuildingMinimumRooms = 15;
            world.largeBuildingHideCount = new Vector2Int(4, 4);
            world.largeBuildingCrateCount = new Vector2Int(3, 3);
            world.Generate(2718);
            for (int i = 0; i < world.layouts.Count; i++)
            {
                Assert.That(world.IsLargeBuilding(world.layouts[i]), Is.True);
                Assert.That(world.hideSpots.Count(h => h.mazeIndex == i), Is.EqualTo(4));
                Assert.That(ActiveCrates().Count(p => p.mazeIndex == i), Is.EqualTo(3));
            }
        }

        LootPickup[] ActiveCrates() => world.pickups.Where(p => p.isCrate && !p.consumed && p.gameObject.activeSelf).ToArray();

        void CheckPlacement(MazeLayout maze, int cell, Vector3 position)
        {
            Assert.That(cell, Is.InRange(0, maze.Count - 1));
            Vector3 local = position - maze.Center(cell);
            Assert.That(Mathf.Abs(local.x), Is.LessThan(maze.cellSize * .5f));
            Assert.That(Mathf.Abs(local.y), Is.LessThan(maze.cellSize * .5f));
            Assert.That(world.waterSurface - position.y, Is.LessThanOrEqualTo(world.maximumLootDepth));
            Vector3 stem = Vector3.up * (.9f - .32f);
            var overlaps = Physics.OverlapCapsule(position + stem, position - stem, .32f,
                1 << world.geometryLayer, QueryTriggerInteraction.Ignore);
            Assert.That(overlaps.Any(c => c.transform.IsChildOf(world.solidRoot)
                && c.GetComponentInParent<TrapdoorGate>() == null), Is.False,
                maze.name + ": room " + cell + " must fit a swimmer clear of the actual wall colliders.");
        }

        string FeatureFingerprint()
        {
            return string.Join(";", ActiveCrates().Select(p => "C" + p.mazeIndex + ":" + p.mazeCell)
                .Concat(world.hideSpots.Select(h => "H" + h.mazeIndex + ":" + h.mazeCell)).OrderBy(s => s));
        }

        string[] OrdinaryLootFingerprint()
        {
            return world.pickups.Where(p => !p.isCrate).Select(p => p.saveId + "|" + p.mazeIndex + ":" + p.mazeCell
                + "|" + p.transform.position.ToString("R") + "|" + p.resource + "|" + string.Join(",", p.contents)).ToArray();
        }
    }
}
