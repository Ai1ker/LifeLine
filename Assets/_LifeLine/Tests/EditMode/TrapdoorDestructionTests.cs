using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class TrapdoorDestructionTests
    {
        GameObject root;
        GameSession game, previous;
        GameTuning tuning;
        float previousScale;
        UnityEngine.Random.State previousRandom;

        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; previousScale = Time.timeScale; previousRandom = UnityEngine.Random.state;
            root = new GameObject("Breakable hatch fixture");
        }

        [TearDown] public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (tuning != null) Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = previousScale; UnityEngine.Random.state = previousRandom;
        }

        [TestCase(false)] [TestCase(true)]
        public void BrokenGateCannotCloseOrBlockThePassageAfterReconfiguration(bool horizontal)
        {
            var door = new GameObject("Recoverable obstruction"); door.transform.SetParent(root.transform); door.layer = 8;
            door.transform.position = new Vector3(1000, -200, 0);
            TrapdoorGate gate = door.AddComponent<TrapdoorGate>(); gate.Configure(horizontal, 3, 2, null);
            Vector3 direction = horizontal ? Vector3.up : Vector3.right;
            Vector3 start = door.transform.position + direction * 3, end = door.transform.position - direction * 3;
            Physics.SyncTransforms(); Assert.That(Physics.Linecast(start, end, 1 << 8, QueryTriggerInteraction.Ignore), Is.True);

            Assert.That(gate.BreakByWinch(), Is.True);
            gate.SetOpen(false); gate.Configure(horizontal, 3, 2, null); Physics.SyncTransforms();
            Assert.That(gate.destroyed && gate.open); Assert.That(gate.barrier.enabled, Is.False);
            Assert.That(gate.visualRoot.gameObject.activeSelf, Is.False); Assert.That(gate.label.gameObject.activeSelf, Is.False);
            Assert.That(Physics.Linecast(start, end, 1 << 8, QueryTriggerInteraction.Ignore), Is.False, "a broken hatch must never resume obstructing recovery");
            Assert.That(gate.BreakByWinch(), Is.False, "breaking the same hatch twice is idempotent");
        }

        [Test] public void BrokenHatchRestoresAcrossRepeatedSameDayLoadsAndLegacyMissingFlagStaysIntact()
        {
            game = root.AddComponent<GameSession>(); game.autoStart = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); tuning.maximumSharks = 0;
            tuning.shallowLootCount = tuning.middleLootCount = tuning.deepLootCount = 1; game.tuning = tuning;
            var ship = new GameObject("Ship"); ship.transform.SetParent(root.transform); ship.transform.position = Vector3.right * 1000;
            ship.AddComponent<Rigidbody>(); game.ship = ship.AddComponent<ShipMotor>();
            game.captain = Crew("Captain"); game.diver = Crew("Diver"); game.Initialize();
            var water = new GameObject("Water map"); water.transform.SetParent(root.transform);
            WorldGenerator world = water.AddComponent<WorldGenerator>(); game.world = world; world.session = game; world.tuning = tuning;
            world.drawLabels = false; world.areaCenters = new[] { 0f }; world.generateCaves = false; world.cratesPerZone = 0; world.trapdoorChance = 1;
            world.Generate(game.worldSeed); Assert.That(world.trapdoors.Count, Is.GreaterThan(0));
            TrapdoorGate gate = world.trapdoors[0]; string id = gate.saveId;
            GameSaveData intact = game.CaptureSaveData();
            int resourcesBefore = game.bank.Sum(slot => slot.amount);
            Assert.That(gate.BreakByWinch(), Is.True); Assert.That(game.bank.Sum(slot => slot.amount), Is.EqualTo(resourcesBefore), "recovery damage provides no materials");
            GameSaveData broken = game.CaptureSaveData();
            Assert.That(broken.world.gates.Single(saved => saved.id == id).destroyed);
            for (int repeat = 0; repeat < 2; repeat++)
            {
                Assert.That(game.TryRestoreSaveData(broken, out string error), Is.True, error);
                TrapdoorGate restored = world.trapdoors.Single(current => current.saveId == id);
                restored.SetOpen(false);
                Assert.That(restored.destroyed && restored.open); Assert.That(restored.barrier.enabled, Is.False);
                Assert.That(game.day, Is.EqualTo(broken.state.day)); Assert.That(game.bank.Sum(slot => slot.amount), Is.EqualTo(resourcesBefore));
            }
            string oldJson = JsonUtility.ToJson(intact).Replace(",\"destroyed\":false", "");
            GameSaveData legacy = JsonUtility.FromJson<GameSaveData>(oldJson);
            Assert.That(game.TryRestoreSaveData(legacy, out string legacyError), Is.True, legacyError);
            TrapdoorGate oldGate = world.trapdoors.Single(current => current.saveId == id);
            Assert.That(oldGate.destroyed, Is.False); Assert.That(oldGate.open, Is.False); Assert.That(oldGate.barrier.enabled);
        }

        CrewActor Crew(string name)
        {
            var actor = new GameObject(name); actor.transform.SetParent(root.transform); actor.AddComponent<Rigidbody>(); actor.AddComponent<CapsuleCollider>();
            var crew = actor.AddComponent<CrewActor>(); crew.enabled = false; return crew;
        }
    }
}
