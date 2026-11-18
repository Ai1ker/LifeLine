using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class WorldCoverageTests
    {
        GameObject root;
        GameTuning tuning;
        WorldGenerator world;

        [SetUp] public void SetUp()
        {
            root = new GameObject("Route coverage fixture");
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

        [Test] public void AddedBuildingsCoverTheWholeRouteAtEveryOriginalDepthWithoutChangingBuildingSizes()
        {
            world.Generate(8417);
            Assert.That(world.layouts.Count, Is.EqualTo(26));
            int[] expectedBuildings = { 11, 9, 6 };
            for (int band = 0; band < world.bandTopDepths.Length; band++)
            {
                float top = world.waterSurface - world.bandTopDepths[band];
                var buildings = world.layouts.Where(m => !m.entranceOnLeft && Mathf.Abs(m.topLeft.y - top) < .01f).ToArray();
                Assert.That(buildings.Length, Is.EqualTo(expectedBuildings[band]));
                foreach (var building in buildings)
                {
                    Assert.That(building.columns, Is.EqualTo(world.bandRoomCounts[band].x));
                    Assert.That(building.rows, Is.EqualTo(world.bandRoomCounts[band].y));
                    Assert.That(building.cellSize, Is.EqualTo(world.bandCellSizes[band]));
                }
                // A swimmer descending anywhere along the route sees architecture within 10m
                // horizontally, rather than the former 50-65m empty gaps between area centres.
                for (float x = 0; x <= tuning.routeDistance; x += 5)
                {
                    float nearest = buildings.Min(m => Mathf.Abs(x - Mathf.Clamp(x, m.topLeft.x, m.topLeft.x + m.columns * m.cellSize)));
                    Assert.That(nearest, Is.LessThanOrEqualTo(10), "Uncovered route x=" + x + ", depth band=" + band);
                }
            }
            Assert.That(world.ValidateGeneratedWorld(out string report), Is.True, report);
        }

        [Test] public void AppendOnlyGenerationPreservesOriginalMazeGraphsLootIdsPositionsAndDeepEventRolls()
        {
            world.generateCaves = true; world.forceCave = true; world.extendRouteCoverage = false;
            world.Generate(8417);
            var graphs = world.layouts.Select(m => string.Join(";", m.passages.Select(p => p.ToString()))).ToArray();
            var ids = world.pickups.Select(p => p.saveId).ToArray();
            var positions = world.pickups.Select(p => p.transform.position).ToArray();
            var resources = world.pickups.Select(p => p.resource).ToArray();
            var gates = world.trapdoors.Select(g => g.saveId).ToArray();
            bool cave = world.generatedCave, eel = world.generatedEel;
            world.extendRouteCoverage = true; world.Generate(8417);
            Assert.That(world.layouts.Take(graphs.Length).Select(m => string.Join(";", m.passages.Select(p => p.ToString()))), Is.EqualTo(graphs));
            Assert.That(world.pickups.Take(ids.Length).Select(p => p.saveId), Is.EqualTo(ids));
            Assert.That(world.pickups.Take(ids.Length).Select(p => p.transform.position), Is.EqualTo(positions));
            Assert.That(world.pickups.Take(ids.Length).Select(p => p.resource), Is.EqualTo(resources));
            Assert.That(world.trapdoors.Take(gates.Length).Select(g => g.saveId), Is.EqualTo(gates));
            Assert.That(world.generatedCave, Is.EqualTo(cave)); Assert.That(world.generatedEel, Is.EqualTo(eel));
            Assert.That(world.pickups.Select(p => p.saveId).Distinct().Count(), Is.EqualTo(world.pickups.Count));
            Assert.That(world.ValidateGeneratedWorld(out string report), Is.True, report);
            Assert.That(world.regionalConnections.Any(c => c.name == "Open-water route above cave"), Is.True);
        }

        [Test] public void AddedBuildingLootUsesItsActualSeaAreaRatherThanTheDepthBand()
        {
            world.shallowResourceWeights = new Vector4(1, 0, 0, 0);
            world.middleResourceWeights = new Vector4(0, 1, 0, 0);
            world.deepResourceWeights = new Vector4(0, 0, 1, 0);
            world.Generate(8417);
            int checkedPickups = 0;
            foreach (var pickup in world.pickups.Where(p => p.mazeIndex >= 9 && !p.isCrate && p.resource != ResourceKind.Fish))
            {
                var maze = world.layouts[pickup.mazeIndex];
                float x = maze.topLeft.x + maze.columns * maze.cellSize * .5f;
                int area = x < tuning.shallowBoundary ? 0 : x < tuning.deepBoundary ? 1 : 2;
                ResourceKind expected = world.weightedResourceKinds[area];
                if (pickup.isCabinet) Assert.That(pickup.contents.All(r => r == expected), Is.True);
                else Assert.That(pickup.resource, Is.EqualTo(expected));
                checkedPickups++;
            }
            Assert.That(checkedPickups, Is.GreaterThan(200));
        }

        [Test] public void ExpensiveCollisionValidationIsOptInAndAnExplicitRunDetectsNewObstructions()
        {
            Assert.That(world.validateOnGeneration, Is.False);
            world.Generate(8417);
            Assert.That(world.validationReport, Does.StartWith("Not run:"));
            Assert.That(world.ValidateGeneratedWorld(out string report), Is.True, report);
            var obstacle = new GameObject("Injected blocked doorway"); obstacle.transform.SetParent(world.solidRoot, false);
            obstacle.transform.position = world.layouts[0].Center(world.layouts[0].entranceCell);
            obstacle.AddComponent<BoxCollider>().size = new Vector3(2, 2, 2);
            Assert.That(world.ValidateGeneratedWorld(out report), Is.False);
            Assert.That(report, Does.Contain("obstructed"));
            Object.DestroyImmediate(obstacle);
            Assert.That(world.ValidateGeneratedWorld(out report), Is.True, report);
        }

        [Test] public void PlaceholderMeshesShareOneCubeAndOnlyPhysicalWallsHaveColliders()
        {
            world.Generate(8417);
            var meshes = world.generatedRoot.GetComponentsInChildren<MeshFilter>();
            Assert.That(meshes.Length, Is.GreaterThan(500));
            Assert.That(meshes.Select(m => m.sharedMesh).Distinct().Count(), Is.EqualTo(1));
            foreach (var mesh in meshes) Assert.That(mesh.GetComponent<Collider>(), Is.Null);
            Assert.That(world.solidRoot.GetComponentsInChildren<BoxCollider>().Length, Is.GreaterThan(100));
        }
    }
}
