using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class AquaticEcosystemTests
    {
        GameObject root; GameSession game, previous; GameTuning tuning; float scale; Random.State random;
        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; scale = Time.timeScale; random = Random.state;
            root = new GameObject("Aquatic ecosystem fixture"); game = root.AddComponent<GameSession>(); game.autoStart = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); game.tuning = tuning; tuning.maximumSharks = 0;
            var ship = new GameObject("Ship"); ship.transform.SetParent(root.transform); game.ship = ship.AddComponent<ShipMotor>();
            game.captain = Actor(); game.diver = Actor(); game.Initialize(); game.DisableTutorialForTests();
        }
        CrewActor Actor()
        {
            var obj = new GameObject("Crew"); obj.transform.SetParent(root.transform);
            var actor = obj.AddComponent<CrewActor>(); actor.enabled = false; return actor;
        }
        WorldGenerator World()
        {
            var obj = new GameObject("Water"); obj.transform.SetParent(root.transform);
            var world = obj.AddComponent<WorldGenerator>(); world.session = game; world.tuning = tuning; world.drawLabels = false; world.geometryLayer = 20;
            game.world = world; return world;
        }
        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes();
            foreach (var enemy in game.enemies.ToArray()) if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root); Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = scale; Random.state = random;
        }
        [Test] public void PlacedCaveAlwaysHasReachableEelAndWarmWallsPlusNearbyWhiteGuards()
        {
            tuning.maximumSharks = 8; tuning.eelCaveChance = 0;
            var world = World(); world.forceCave = true; world.Generate(8417);
            Assert.That(world.generatedCave && world.generatedEel);
            var cave = world.layouts.Single(layout => layout.entranceOnLeft);
            var eels = world.pickups.Where(p => p.resource == ResourceKind.Eel).ToArray();
            Assert.That(eels.Length, Is.EqualTo(1)); Assert.That(eels[0].transform.position, Is.EqualTo(cave.Center(cave.rewardCell)));
            Assert.That(world.caveGuardSpawnPoints.Count, Is.InRange(1, 2));
            Assert.That(world.caveGuardSpawnPoints.All(p => cave.rewardRoute.Any(cell => cave.Center(cell) == p)));
            var caveRoot = world.solidRoot.Cast<Transform>().Single(t => t.name == cave.name);
            var wall = caveRoot.GetComponentsInChildren<Renderer>().First(r => r.transform.parent.name == "Wall");
            Assert.That(wall.sharedMaterial.color.r, Is.GreaterThan(.8f)); Assert.That(wall.sharedMaterial.color.b, Is.LessThan(.4f));
            Assert.That(world.ValidateGeneratedWorld(out string report), Is.True, report);
            game.RefreshWorldForDay(1);
            Assert.That(game.enemies.Count(e => e.IsShark), Is.LessThanOrEqualTo(8));
            Assert.That(game.enemies.Any(e => e.caveGuard && e.kind == EnemyKind.WhiteShark));
        }
        [Test] public void FishSpawnAtEverySeaAndSchoolStaysInCityMiddleDepthWithDeterministicSave()
        {
            var world = World(); world.Generate(8417);
            for (int area = 0; area < 3; area++)
                Assert.That(world.pickups.Any(p => p.resource == ResourceKind.Fish && p.mazeIndex >= area * 3 && p.mazeIndex < area * 3 + 3));
            Assert.That(world.piranhaSpawnPoints.Count, Is.InRange(1, 6));
            Assert.That(world.piranhaSpawnPoints.All(p => p.x >= tuning.shallowBoundary && p.x < tuning.deepBoundary && world.WaterSurface - p.y >= 20 && world.WaterSurface - p.y <= 35));
            var ids = world.pickups.Select(p => p.saveId).ToArray(); var points = world.piranhaSpawnPoints.ToArray();
            world.Generate(8417); Assert.That(world.pickups.Select(p => p.saveId), Is.EqualTo(ids)); Assert.That(world.piranhaSpawnPoints, Is.EqualTo(points));
            var fish = world.pickups.First(p => p.resource == ResourceKind.Fish); fish.consumed = true; string fishId = fish.saveId;
            var saved = game.CaptureSaveData(); Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(world.pickups.Single(p => p.saveId == fishId).consumed);
        }
        [Test] public void SchoolRollHasSixIndependentSixtyPercentSlotsAndOneFishFloor()
        {
            var rng = new System.Random(717); int total = 0, full = 0;
            for (int i = 0; i < 10000; i++) { int count = WorldGenerator.RollPiranhaCount(rng, 6, .6f); Assert.That(count, Is.InRange(1, 6)); total += count; if (count == 6) full++; }
            Assert.That(total / 10000f, Is.InRange(3.5f, 3.7f)); Assert.That(full, Is.InRange(350, 600));
            Assert.That(WorldGenerator.RollPiranhaCount(rng, 6, 0), Is.EqualTo(1)); Assert.That(WorldGenerator.RollPiranhaCount(rng, 0, .6f), Is.Zero);
        }
        [Test] public void SharksNeedTwoOrThreeHitsAndEverySurvivedHitInterruptsIntoThreeSecondFlight()
        {
            tuning.maximumSharks = 8; game.BeginDive(); game.diver.RestorePose(false, new Vector3(33, -32, 0), Vector3.zero, Vector2.right);
            foreach (var kind in new[] { EnemyKind.Shark, EnemyKind.WhiteShark })
            {
                var enemy = game.SpawnEnemy(kind, new Vector3(35, -32, 0)); enemy.geometryMask = 1 << 20; enemy.state = EnemyState.Lunge;
                int hits = kind == EnemyKind.Shark ? 2 : 3;
                for (int i = 1; i < hits; i++)
                {
                    enemy.ReceiveHit(99, game.diver); Assert.That(enemy.health, Is.EqualTo(hits - i)); Assert.That(enemy.state, Is.EqualTo(EnemyState.Retreat));
                    float before = Vector3.Distance(enemy.transform.position, game.diver.Position);
                    game.baitTime = 10; game.baitPosition = enemy.transform.position;
                    enemy.StepEnemy(2.99f); Assert.That(enemy.state, Is.EqualTo(EnemyState.Retreat));
                    Assert.That(Vector3.Distance(enemy.transform.position, game.diver.Position), Is.GreaterThan(before));
                    enemy.StepEnemy(.02f); Assert.That(enemy.state, Is.EqualTo(EnemyState.Patrol));
                }
                enemy.ReceiveHit(1, game.diver); Assert.That(enemy.IsAlive, Is.False);
            }
        }
        [Test] public void PiranhaUsesOneHitUnitAndOneShotDeathWithConcreteAttackTuning()
        {
            game.BeginDive(); game.diver.RestorePose(false, new Vector3(80, -29, 0), Vector3.zero, Vector2.right);
            var fish = game.SpawnEnemy(EnemyKind.Piranha, game.diver.Position);
            Assert.That(fish.AttackRange, Is.EqualTo(.84f)); Assert.That(fish.AttackWindup, Is.EqualTo(.544f)); Assert.That(fish.ChaseSpeed, Is.EqualTo(3.696f));
            Assert.That(fish.visualRoot.localScale.x, Is.EqualTo(1.2f).Within(.001f));
            game.diver.ReceiveDamage(1f); Assert.That(game.diverBites, Is.EqualTo(1f)); Assert.That(game.diverFainted, Is.False);
            var saved = game.CaptureSaveData(); Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(game.diverBites, Is.EqualTo(1f)); game.diverInvulnerable = 0; game.diver.ReceiveDamage(1f); Assert.That(game.diverFainted);
            var restoredFish = game.enemies.Single(); restoredFish.ReceiveHit(.01f, game.captain); Assert.That(restoredFish.IsAlive, Is.False);
        }
        [Test] public void AquaticHuntersSeeCircularMetreRangesAndPassThroughSolidWalls()
        {
            tuning.maximumSharks = 8; game.BeginDive(); game.diver.RestorePose(false, new Vector3(80, -30, 0), Vector3.zero, Vector2.right);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform); wall.layer = 20; wall.transform.position = new Vector3(78, -30, 0); wall.transform.localScale = new Vector3(.4f, 8, 2);
            Physics.SyncTransforms();
            foreach (var kind in new[] { EnemyKind.Shark, EnemyKind.WhiteShark, EnemyKind.Piranha })
            {
                var enemy = game.SpawnEnemy(kind, new Vector3(77, -30, 0)); enemy.geometryMask = 1 << 20;
                Assert.That(enemy.DetectionRadius, Is.EqualTo(3.6f)); enemy.StepEnemy(.01f); Assert.That(enemy.state, Is.EqualTo(EnemyState.Detecting));
                enemy.state = EnemyState.Pursuit; enemy.StepEnemy(.7f); Assert.That(enemy.transform.position.x, Is.GreaterThan(78.2f));
                game.elapsed = tuning.daySeconds * .9f; Assert.That(enemy.DetectionRadius, Is.EqualTo(1.8f));
                game.upgradeLevels[(int)UpgradeKind.Searchlight] = 1;
                game.searchlightOn = game.circuitRepaired = true; game.power = 10; Assert.That(enemy.DetectionRadius, Is.EqualTo(3.6f));
                game.diver.transform.localScale = Vector3.one * 2; Assert.That(enemy.DetectionRadius, Is.EqualTo(3.6f), "art scaling cannot change authored metre values");
                game.diver.transform.localScale = Vector3.one; game.elapsed = 0; game.searchlightOn = false;
            }
        }
        [Test] public void FaintedDiverDoesNotPreventCaptainDiveOrLetPoweredWinchInstantlyReboardHim()
        {
            game.BeginDive(); game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 10, Vector3.zero, Vector2.right); game.FaintDiver("test");
            var station = new GameObject("Dive prep"); station.transform.SetParent(game.ship.transform); station.transform.position = game.captain.Position;
            station.AddComponent<StationAnchor>().kind = StationKind.DivePreparation;
            game.upgradeLevels[(int)UpgradeKind.Rope] = 3; game.circuitRepaired = true; game.power = 50; game.winchElectric = game.winchSignalAuthorized = true;
            game.HandleCommand(game.captain, new CrewCommand { interact = true });
            Assert.That(game.captain.onboard, Is.False); Assert.That(game.winchElectric || game.winchActive || game.winchSignalAuthorized, Is.False);
            for (int i = 0; i < 10; i++) game.StepSimulation(.02f);
            Assert.That(game.captain.onboard, Is.False); Assert.That(game.captain.Position.y, Is.LessThan(0)); Assert.That(game.phase, Is.EqualTo(SessionPhase.Playing));
        }
    }
}
