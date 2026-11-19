using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class BuildingFeaturePersistenceTests
    {
        GameObject root;
        GameSession game, previous;
        GameTuning tuning;
        WorldGenerator world;
        float previousScale;
        Random.State previousRandom;

        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; previousScale = Time.timeScale; previousRandom = Random.state;
            root = new GameObject("Random building feature persistence fixture");
            game = root.AddComponent<GameSession>(); game.autoStart = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); tuning.maximumSharks = 0; game.tuning = tuning;
            var ship = new GameObject("Ship"); ship.transform.SetParent(root.transform);
            game.ship = ship.AddComponent<ShipMotor>(); game.captain = Actor(); game.diver = Actor();
            game.Initialize();
            var water = new GameObject("Water"); water.transform.SetParent(root.transform);
            world = water.AddComponent<WorldGenerator>(); world.tuning = tuning; world.session = game; game.world = world;
            world.extendRouteCoverage = false; world.generateCaves = false; world.filterSalvageChance = 0;
            world.drawLabels = false; world.geometryLayer = 20;
            game.worldSeed = 8417; world.Generate(game.worldSeed);
        }

        CrewActor Actor()
        {
            var obj = new GameObject("Crew"); obj.transform.SetParent(root.transform);
            var actor = obj.AddComponent<CrewActor>(); actor.enabled = false; return actor;
        }

        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes();
            foreach (var enemy in game.enemies.ToArray()) if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root); Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = previousScale; Random.state = previousRandom;
        }

        [Test] public void RepeatedLoadRestoresExactHidesHiddenCrewAndCargoAfterHideRangesAreEdited()
        {
            UnderwaterHideSpot hide = world.hideSpots.First(h => h.mazeIndex == 0);
            hide.range = 2.15f; hide.transform.position += Vector3.right * .05f;
            game.diver.RestorePose(false, hide.transform.position, Vector3.zero, Vector2.left);
            game.diver.SetHidden(true); game.diver.oxygen = 31;
            game.diver.backpack.Add(ResourceKind.Metal); game.diver.backpack.Add(ResourceKind.Fish);
            LootPickup cargo = world.pickups.First(p => p.isCrate && p.mazeIndex == 0 && !p.consumed);
            cargo.contents.Add(ResourceKind.Wood); cargo.contents.Add(ResourceKind.Fiber); cargo.attached = true;
            LootPickup consumed = world.pickups.First(p => p.isCrate && p != cargo && !p.consumed);
            consumed.consumed = true; consumed.gameObject.SetActive(false);
            string cargoId = cargo.saveId, consumedId = consumed.saveId;
            GameSaveData saved = game.CaptureSaveData();
            string[] hides = HideFingerprint(); Vector3 swimmerPosition = game.diver.Position;
            Assert.That(saved.world.hasHidingSpots, Is.True);
            Assert.That(saved.world.hidingSpots.Length, Is.EqualTo(world.hideSpots.Count));

            world.smallBuildingHideCount = world.largeBuildingHideCount = Vector2Int.zero;
            for (int load = 0; load < 2; load++)
            {
                Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
                Assert.That(HideFingerprint(), Is.EqualTo(hides), "Load preserves the saved snapshot instead of rerolling edited generation ranges.");
                Assert.That(game.IsHidden(game.diver), Is.True); Assert.That(game.diver.onboard, Is.False);
                Assert.That(game.diver.Position, Is.EqualTo(swimmerPosition)); Assert.That(game.diver.oxygen, Is.EqualTo(31));
                Assert.That(game.diver.backpack, Is.EqualTo(new[] { ResourceKind.Metal, ResourceKind.Fish }));
                var restoredCargo = world.pickups.Single(p => p.saveId == cargoId);
                Assert.That(restoredCargo.attached && !restoredCargo.consumed && restoredCargo.gameObject.activeSelf, Is.True);
                Assert.That(restoredCargo.contents, Is.EqualTo(new[] { ResourceKind.Wood, ResourceKind.Fiber }));
                var restoredConsumed = world.pickups.Single(p => p.saveId == consumedId);
                Assert.That(restoredConsumed.consumed, Is.True); Assert.That(restoredConsumed.gameObject.activeSelf, Is.False);
                Assert.That(world.pickups.Select(p => p.saveId).Distinct().Count(), Is.EqualTo(world.pickups.Count), "Repeated load must not duplicate cargo IDs.");
            }
        }

        [Test] public void LegacyHiddenCrewAndUnselectedLegacyCrateStillRestoreWithoutIncreasingFeatureCounts()
        {
            world.smallBuildingCrateCount = new Vector2Int(1, 1);
            world.Generate(game.worldSeed);
            // Select a genuine old hiding room that today's random placement does not cover,
            // ensuring this exercises the compatibility relocation rather than the normal path.
            int mazeIndex = Enumerable.Range(0, world.layouts.Count).First(i => world.hideSpots.All(h =>
                Vector2.Distance(world.layouts[i].Center(0), h.transform.position) > h.range));
            Vector3 oldHide = world.layouts[mazeIndex].Center(0);
            int[] hideCounts = Enumerable.Range(0, world.layouts.Count).Select(i => world.hideSpots.Count(h => h.mazeIndex == i)).ToArray();
            GameSaveData saved = game.CaptureSaveData();
            saved.world.hasHidingSpots = false; saved.world.hidingSpots = null;
            saved.diver.onboard = false; saved.diver.hidden = true; saved.diver.position = saved.diver.hidingPosition = oldHide;
            saved.diver.backpack.Add(ResourceKind.Fuel);
            // A one-crate small building retains its unused legacy ID, so an older save
            // can still bring back the second loaded crate at its original location.
            var unusedLegacy = saved.world.pickups.First(p => p.isCrate && p.consumed && p.mazeIndex >= 0
                && !world.IsLargeBuilding(world.layouts[p.mazeIndex]));
            unusedLegacy.consumed = false; unusedLegacy.attached = true;
            unusedLegacy.position = world.layouts[unusedLegacy.mazeIndex].Center(world.layouts[unusedLegacy.mazeIndex].rewardCell);
            unusedLegacy.contents.Add(ResourceKind.Metal); unusedLegacy.contents.Add(ResourceKind.Fiber);

            for (int load = 0; load < 2; load++)
            {
                Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
                Assert.That(game.IsHidden(game.diver), Is.True); Assert.That(game.diver.Position, Is.EqualTo(oldHide));
                Assert.That(game.diver.backpack, Is.EqualTo(new[] { ResourceKind.Fuel }));
                Assert.That(world.hideSpots.Any(h => h.transform.position == oldHide), Is.True,
                    "A spot moves back to the old room so saved hidden crew remain protected.");
                Assert.That(Enumerable.Range(0, world.layouts.Count).Select(i => world.hideSpots.Count(h => h.mazeIndex == i)), Is.EqualTo(hideCounts));
                var cargo = world.pickups.Single(p => p.saveId == unusedLegacy.id);
                Assert.That(cargo.attached && !cargo.consumed && cargo.gameObject.activeSelf, Is.True);
                Assert.That(cargo.transform.position, Is.EqualTo(unusedLegacy.position));
                Assert.That(cargo.contents, Is.EqualTo(new[] { ResourceKind.Metal, ResourceKind.Fiber }));
            }
        }

        string[] HideFingerprint() => world.hideSpots.Select(h => h.mazeIndex + ":" + h.mazeCell
            + "|" + h.transform.position.ToString("R") + "|" + h.range.ToString("R")).ToArray();
    }
}
