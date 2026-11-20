using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class MapPersistenceTests
    {
        GameObject root;
        GameSession game, previous;
        GameTuning tuning;
        float previousScale;
        UnityEngine.Random.State previousRandom;

        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; previousScale = Time.timeScale; previousRandom = UnityEngine.Random.state;
            root = new GameObject("Independent map persistence fixture");
            game = root.AddComponent<GameSession>(); game.autoStart = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); tuning.maximumSharks = 0; game.tuning = tuning;
            var boat = new GameObject("Ship"); boat.transform.SetParent(root.transform); boat.AddComponent<Rigidbody>(); game.ship = boat.AddComponent<ShipMotor>();
            game.captain = Crew(CrewRole.Captain); game.diver = Crew(CrewRole.Diver);
            game.maps = root.AddComponent<GameMapPartition>(); game.maps.shipMapOrigin = Vector3.up * 1000;
            game.Initialize();
        }

        CrewActor Crew(CrewRole role)
        {
            var go = new GameObject(role.ToString()); go.transform.SetParent(root.transform);
            go.AddComponent<Rigidbody>(); go.AddComponent<CapsuleCollider>();
            var actor = go.AddComponent<CrewActor>(); actor.enabled = false; return actor;
        }

        [TearDown] public void TearDown()
        {
            foreach (LifelineSpear spear in Object.FindObjectsByType<LifelineSpear>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (spear.session == game) Object.DestroyImmediate(spear.gameObject);
            foreach (LifelineEnemy enemy in game.enemies.ToArray()) if (enemy != null) Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root); Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = previousScale; UnityEngine.Random.state = previousRandom;
        }

        [Test] public void ShipBirdAndProjectileSaveInLogicalCoordinatesThenRestoreIntoCurrentMapOrigin()
        {
            Assert.That(game.ship.Position.y, Is.EqualTo(1000));
            game.ship.body.position += Vector3.right * 37; game.ship.transform.position = game.ship.body.position;
            game.captain.RestorePose(true, game.ShipPoint(new Vector3(2, 1, 0)), Vector3.zero, Vector2.right);
            game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 10, Vector3.zero, Vector2.left);
            LifelineEnemy bird = game.SpawnEnemy(EnemyKind.Bird, game.ShipPoint(new Vector3(8, 6, 0)));
            bird.targetPoint = game.ShipPoint(new Vector3(0, 1, 0));
            game.hasSpeargun = true; Assert.That(game.FireSpear(game.captain));
            GameSaveData saved = game.CaptureSaveData();
            Assert.That(saved.coordinateVersion, Is.EqualTo(1)); Assert.That(saved.shipPosition, Is.EqualTo(new Vector3(37, 0, 0)));
            Assert.That(saved.enemies.Single().position.y, Is.EqualTo(6)); Assert.That(saved.spears.Single().onShipMap);
            Vector3 waterDiverPosition = saved.diver.position; Vector3 logicalSpearPosition = saved.spears.Single().position;

            game.maps.shipMapOrigin = Vector3.up * 2000; game.maps.Initialize(game);
            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(game.ship.Position, Is.EqualTo(new Vector3(37, 2000, 0)));
            Assert.That(game.captain.Position, Is.EqualTo(game.ShipPoint(new Vector3(2, 1, 0))));
            Assert.That(game.diver.Position, Is.EqualTo(waterDiverPosition), "a swimmer must stay in the water map");
            Assert.That(game.enemies.Single().transform.position.y, Is.EqualTo(2006));
            Assert.That(game.enemies.Single().targetPoint.y, Is.EqualTo(2001));
            Assert.That(game.activeSpears[0].transform.position, Is.EqualTo(game.WaterToShipPoint(logicalSpearPosition)));
        }

        [Test] public void LegacySingleMapSaveMovesTheShipAndBirdButKeepsShadowLodgedSpearUnderwater()
        {
            LifelineEnemy bird = game.SpawnEnemy(EnemyKind.Bird, game.ShipPoint(new Vector3(5, 4, 0)));
            LifelineEnemy shadow = game.SpawnEnemy(EnemyKind.Shadow, game.BoatAnchor + Vector3.down * 8);
            var projectile = new GameObject("Legacy lodged spear"); projectile.transform.position = shadow.transform.position; projectile.transform.SetParent(shadow.transform);
            var spear = projectile.AddComponent<LifelineSpear>(); spear.session = game; spear.owner = game.captain; spear.hasHit = spear.lodged = true;
            game.activeSpears[0] = spear;
            GameSaveData saved = game.CaptureSaveData(); saved.coordinateVersion = 0;
            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(game.ship.Position.y, Is.EqualTo(1000), "old y=0 saves cannot move the cabin back into the water map");
            Assert.That(game.enemies.Single(e => e.IsAirborne).transform.position.y, Is.EqualTo(1004));
            LifelineSpear restored = game.activeSpears[0];
            Assert.That(restored.onShipMap, Is.False, "the lodged enemy overrides an aboard owner's map in an old save");
            Assert.That(restored.transform.parent.GetComponent<LifelineEnemy>().kind, Is.EqualTo(EnemyKind.Shadow));
            Assert.That(restored.transform.position.y, Is.LessThan(0)); Assert.That(restored.hasHit && restored.lodged);
        }

        [Test] public void SpearRecallUsesTheOwnersEquivalentPositionInTheProjectilesMap()
        {
            game.hasSpeargun = true; Assert.That(game.FireSpear(game.diver));
            LifelineSpear spear = game.activeSpears[1]; Assert.That(spear.onShipMap);
            game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 5, Vector3.zero, Vector2.down);
            Assert.That(spear.RecallPoint, Is.EqualTo(game.WaterToShipPoint(game.diver.Position)));
            spear.onShipMap = false;
            Assert.That(spear.RecallPoint, Is.EqualTo(game.diver.Position));
            game.diver.RestorePose(true, game.ShipPoint(new Vector3(3, 1, 0)), Vector3.zero, Vector2.right);
            Assert.That(spear.RecallPoint, Is.EqualTo(game.ShipToWaterPoint(game.diver.Position)));
        }

        [Test] public void WrappedRopeContactsSurviveLoadingWithoutResettingToAThroughWallLine()
        {
            var wall = new GameObject("Water ruin obstruction"); wall.transform.SetParent(root.transform); wall.layer = 8;
            wall.transform.position = game.BoatAnchor + new Vector3(4, -3.5f, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(1, 6, 2);
            game.diver.RestorePose(false, game.BoatAnchor + new Vector3(8, -7, 0), Vector3.zero, Vector2.down);
            Physics.SyncTransforms(); game.RefreshLifelines();
            Assert.That(game.diverRope.IsBlocked, Is.False); Assert.That(game.diverRope.ContactCount, Is.GreaterThan(0));
            GameSaveData saved = game.CaptureSaveData();
            Assert.That(saved.diverRopeContacts.Length, Is.GreaterThan(0)); float pathLength = game.diverRope.PathLength;
            game.diverRope.ResetRoute();
            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(game.diverRope.IsBlocked, Is.False); Assert.That(game.diverRope.ContactCount, Is.GreaterThan(0));
            Assert.That(game.diverRope.PathLength, Is.EqualTo(pathLength).Within(.03f));
        }

        [Test] public void OvernightSwimmerKeepsMapAndWrappedRopeThroughSaveAndReplica()
        {
            var worldObject = new GameObject("Overnight world");
            worldObject.transform.SetParent(root.transform);
            var world = worldObject.AddComponent<WorldGenerator>();
            world.session = game; world.tuning = tuning; world.areaCenters = new[] { 0f }; world.generateCaves = false;
            game.world = world;
            world.Generate(game.worldSeed);
            game.SetDayClock(1, .999f);
            var wall = new GameObject("Fixed test obstruction");
            wall.transform.SetParent(root.transform);
            wall.layer = world.geometryLayer;
            wall.transform.position = game.BoatAnchor + new Vector3(4, -3.5f, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(1, 6, 2);
            var position = game.BoatAnchor + new Vector3(8, -7, 0);
            game.diver.RestorePose(false, position, Vector3.zero, Vector2.up);
            Physics.SyncTransforms();
            game.RefreshLifelines();
            var contacts = game.diverRope.CaptureContacts();
            Assert.That(contacts, Is.Not.Empty);
            var geometry = world.generatedRoot;
            int firstSeed = world.generatedSeed;
            int autosaveDay = 0;
            game.DayReady += readyDay => autosaveDay = readyDay;

            game.StepSimulation(tuning.daySeconds * .002f);
            Assert.That(game.day, Is.EqualTo(2));
            Assert.That(autosaveDay, Is.EqualTo(2));
            Assert.That(world.generatedRoot, Is.SameAs(geometry));
            Assert.That(game.diver.Position, Is.EqualTo(position));
            Assert.That(game.diverRope.CaptureContacts(), Is.EqualTo(contacts));

            var saved = game.CaptureSaveData();
            Assert.That(saved.world.activeDay, Is.EqualTo(1));
            Assert.That(game.TryRestoreSaveData(saved, out string error), Is.True, error);
            Assert.That(world.generatedSeed, Is.EqualTo(firstSeed));
            Assert.That(game.diverRope.CaptureContacts(), Is.EqualTo(contacts));
            var frame = game.CaptureOnlineFrame(null, false, true);
            world.RefreshDay(2);
            game.ApplyOnlineFrame(frame, null);
            Assert.That(world.activeDay, Is.EqualTo(1));
            Assert.That(world.generatedSeed, Is.EqualTo(firstSeed));
            Assert.That(game.diver.Position, Is.EqualTo(position));

            game.diver.RestorePose(true, game.ShipPoint(game.diverSpawnLocal), Vector3.zero, Vector2.right);
            Assert.That(world.activeDay, Is.EqualTo(1));
            game.StepSimulation(tuning.daySeconds);
            Assert.That(world.activeDay, Is.EqualTo(3));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MeleeChoosesATargetInTheFacingSector(bool onboard)
        {
            var position = onboard ? game.ShipPoint(new Vector3(0, 1, 0)) : game.BoatAnchor + Vector3.down * 8;
            var facing = onboard ? Vector2.right : Vector2.up;
            var side = new Vector2(-facing.y, facing.x);
            game.diver.RestorePose(onboard, position, Vector3.zero, facing);
            game.hasHarpoon = true;
            var kind = onboard ? EnemyKind.Bird : EnemyKind.Shadow;
            var outside = game.SpawnEnemy(kind, position + (Vector3)side, true);
            var front = game.SpawnEnemy(kind, position + (Vector3)facing * 2.75f, true);
            front.health = outside.health = 100f;
            front.hitRadius = outside.hitRadius = .1f;
            Assert.That(game.MeleeAttack(game.diver));
            Assert.That(front.health, Is.LessThan(100f));
            Assert.That(outside.health, Is.EqualTo(100f));
        }

        [Test] public void EveryAttachedCargoRestoresItsOwnWrappedRoute()
        {
            var water = new GameObject("World"); water.transform.SetParent(root.transform);
            game.world = water.AddComponent<WorldGenerator>(); game.world.session = game; game.world.tuning = tuning;
            game.world.geometryLayer = 8; game.world.drawLabels = false; game.world.areaCenters = new[] { 0f }; game.world.generateCaves = false;
            tuning.shallowLootCount = tuning.middleLootCount = tuning.deepLootCount = 1;
            game.world.Generate(game.worldSeed);
            var wall = new GameObject("Shared obstacle"); wall.transform.SetParent(root.transform); wall.layer = 8;
            wall.transform.position = game.BoatAnchor + new Vector3(4, -3.5f, 0);
            wall.AddComponent<BoxCollider>().size = new Vector3(1, 6, 2);
            for (int i = 0; i < 2; i++)
            {
                var obj = new GameObject("Cargo " + i); obj.transform.SetParent(water.transform);
                obj.transform.position = game.BoatAnchor + new Vector3(8 + i, -7, 0);
                var cargo = obj.AddComponent<LootPickup>(); cargo.world = game.world;
                cargo.isCrate = cargo.attached = true; cargo.contents.Add(ResourceKind.Wood);
                game.world.pickups.Add(cargo); game.RegisterAttachedCargo(cargo);
            }
            Physics.SyncTransforms(); game.RefreshLifelines();
            GameSaveData data = game.CaptureSaveData();
            var attached = data.world.pickups.Where(p => p.attached).ToArray();
            Assert.That(attached.Length, Is.EqualTo(2));
            Assert.That(attached.All(p => p.ropeContacts.Length > 0));
            Assert.That(game.TryRestoreSaveData(data, out string error), Is.True, error);
            Assert.That(game.WinchTargetCount, Is.EqualTo(2), "the aboard diver does not count");
            foreach (var saved in attached)
            {
                var cargo = game.world.pickups.Single(p => p.saveId == saved.id);
                Assert.That(cargo.attached); Assert.That(cargo.transform.position, Is.EqualTo(saved.position));
                var route = game.GetCargoLifeline(cargo);
                Assert.That(route.IsBlocked, Is.False); Assert.That(route.ContactCount, Is.GreaterThan(0));
                Assert.That(route.CaptureContacts(), Is.EqualTo(saved.ropeContacts));
            }
        }

        [Test] public void TurretFiresInTheTargetsMapAndCaptainHarpoonCanHitTheNearbyShadow()
        {
            game.circuitRepaired = game.turretInstalled = game.turretOn = true; game.power = 100;
            LifelineEnemy bird = game.SpawnEnemy(EnemyKind.Bird, game.ShipPoint(new Vector3(12, 2, 0)));
            TickTurret();
            LifelineSpear shot = Object.FindObjectsByType<LifelineSpear>(FindObjectsSortMode.None).Single(s => s.session == game);
            Assert.That(shot.onShipMap); Assert.That(shot.target, Is.SameAs(bird)); Assert.That(shot.transform.position.y, Is.GreaterThan(1000));
            Object.DestroyImmediate(shot.gameObject); bird.health = 0;
            Vector3 equivalentCaptain = game.ShipToWaterPoint(game.captain.Position);
            LifelineEnemy shadow = game.SpawnEnemy(EnemyKind.Shadow, equivalentCaptain + Vector3.right * 2);
            TickTurret();
            shot = Object.FindObjectsByType<LifelineSpear>(FindObjectsSortMode.None).Single(s => s.session == game);
            Assert.That(shot.onShipMap, Is.False); Assert.That(shot.target, Is.SameAs(shadow)); Assert.That(shot.transform.position.y, Is.LessThan(10));
            game.hasHarpoon = true; Assert.That(game.MeleeAttack(game.captain)); Assert.That(shadow.shadowHitCount, Is.EqualTo(1));
        }

        void TickTurret() => typeof(GameSession).GetMethod("UpdateTurret", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(game, new object[] { tuning.turretShotInterval + .01f });
    }
}
