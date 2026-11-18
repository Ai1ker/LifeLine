using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class FilterSalvageTests
    {
        GameObject root; GameSession game, previous; GameTuning tuning; WorldGenerator world;
        float scale; Random.State random;
        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; scale = Time.timeScale; random = Random.state;
            root = new GameObject("Filter salvage fixture"); game = root.AddComponent<GameSession>(); game.autoStart = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); game.tuning = tuning; tuning.maximumSharks = 0;
            var ship = new GameObject("Ship"); ship.transform.SetParent(root.transform); game.ship = ship.AddComponent<ShipMotor>();
            game.captain = Actor(); game.diver = Actor(); game.Initialize();
            var water = new GameObject("Water"); water.transform.SetParent(root.transform);
            world = water.AddComponent<WorldGenerator>(); world.session = game; world.tuning = tuning;
            world.drawLabels = false; world.geometryLayer = 20; world.filterSalvageChance = 1; game.world = world;
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
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = scale; Random.state = random;
        }
        [Test] public void ThirtyPercentIndependentDailyRollIsDeterministicAndLeavesUnityRandomUntouched()
        {
            var before = Random.state; int spawned = 0;
            for (int day = 1; day <= 10000; day++)
            {
                int seed = WorldGenerator.ComputeDaySeed(8417, day);
                bool roll = WorldGenerator.RollFilterSalvage(seed, .3f);
                Assert.That(WorldGenerator.RollFilterSalvage(seed, .3f), Is.EqualTo(roll));
                if (roll) spawned++;
            }
            Assert.That(spawned, Is.InRange(2850, 3150)); Assert.That(Random.state, Is.EqualTo(before));
            Assert.That(WorldGenerator.RollFilterSalvage(1, 0), Is.False);
            Assert.That(WorldGenerator.RollFilterSalvage(1, 1), Is.True);
        }
        [Test] public void DeviceLivesInCityMiddleRoomsAndDoesNotChangeLegacyLootOrGeometry()
        {
            world.filterSalvageChance = 0; world.Generate(8417);
            var ids = world.pickups.Select(p => p.saveId).ToArray();
            var positions = world.pickups.Select(p => p.transform.position).ToArray();
            var graphs = world.layouts.Select(m => string.Join(";", m.passages.Select(p => p.ToString()))).ToArray();
            world.filterSalvageChance = 1; world.Generate(8417);
            Assert.That(world.pickups.Where(p => p.GetComponent<FilterSalvage>() == null).Select(p => p.saveId), Is.EqualTo(ids));
            Assert.That(world.pickups.Where(p => p.GetComponent<FilterSalvage>() == null).Select(p => p.transform.position), Is.EqualTo(positions));
            Assert.That(world.layouts.Select(m => string.Join(";", m.passages.Select(p => p.ToString()))), Is.EqualTo(graphs));
            Assert.That(world.pickups.Count(p => p.GetComponent<FilterSalvage>() != null), Is.EqualTo(1));
            var pickup = world.filterSalvage.pickup; float depth = world.WaterSurface - pickup.transform.position.y;
            Assert.That(pickup.mazeIndex, Is.EqualTo(4)); Assert.That(depth, Is.InRange(20, 35));
            Assert.That(pickup.transform.position.x, Is.InRange(tuning.shallowBoundary, tuning.deepBoundary));
            Assert.That(world.piranhaSpawnPoints.Count, Is.InRange(2, 6));
            Assert.That(world.piranhaSpawnPoints.Count(p => Vector3.Distance(p, pickup.transform.position) <= world.filterGuardRadius), Is.GreaterThanOrEqualTo(2));
            Assert.That(world.ValidateGeneratedWorld(out string report), Is.True, report);
        }
        [Test] public void FullBackpackCanSalvageDeviceAndAcquiredEquipmentDoesNotRespawnAcrossDaysOrSaveLoad()
        {
            world.Generate(8417); var pickup = world.filterSalvage.pickup;
            game.BeginDive(); game.diver.RestorePose(false, pickup.transform.position, Vector3.zero, Vector2.right);
            for (int n = 0; n < game.diver.BagCapacity; n++) game.diver.backpack.Add(ResourceKind.Wood);
            int backpackCount = game.diver.backpack.Count;
            Assert.That(pickup.TryCollect(game.diver), Is.True); Assert.That(game.filterInstalled, Is.False);
            typeof(LootPickup).GetField("startedAt", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(pickup, Time.time - 3.1f);
            Assert.That(pickup.TryCollect(game.diver), Is.True); Assert.That(game.filterInstalled, Is.True);
            Assert.That(pickup.consumed, Is.True); Assert.That(game.diver.backpack.Count, Is.EqualTo(backpackCount));
            var saved = game.CaptureSaveData(); Assert.That(saved.world.pickups.Single(p => p.id == pickup.saveId).dynamic, Is.True);
            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(game.filterInstalled, Is.True); Assert.That(world.pickups.All(p => p.consumed || p.GetComponent<FilterSalvage>() == null), Is.True);
            world.RefreshDay(2); Assert.That(world.filterSalvage, Is.Null);
        }
        [Test] public void UnacquiredDeviceRestoresWithItsSpecialInteractionEvenIfCurrentSpawnChanceWasChanged()
        {
            world.Generate(8417); string id = world.filterSalvage.pickup.saveId;
            Vector3 position = world.filterSalvage.transform.position;
            var saved = game.CaptureSaveData(); world.filterSalvageChance = 0;
            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            var restored = world.pickups.Single(p => p.saveId == id);
            Assert.That(restored.GetComponent<FilterSalvage>(), Is.Not.Null);
            Assert.That(restored.consumed, Is.False); Assert.That(restored.transform.position, Is.EqualTo(position));
            Assert.That(game.filterInstalled, Is.False);
        }
        [TestCase(0)] [TestCase(1)] [TestCase(6)]
        public void FilterGuardsRespectSharedPiranhaMaximum(int maximum)
        {
            tuning.maximumPiranhas = maximum; world.Generate(8417);
            Assert.That(world.filterSalvage, Is.Not.Null);
            Assert.That(world.piranhaSpawnPoints.Count, Is.LessThanOrEqualTo(maximum));
            if (maximum > 0) Assert.That(world.piranhaSpawnPoints.Count, Is.GreaterThanOrEqualTo(1));
            Assert.That(world.piranhaSpawnPoints.Distinct().Count(), Is.EqualTo(world.piranhaSpawnPoints.Count));
        }
    }
}
