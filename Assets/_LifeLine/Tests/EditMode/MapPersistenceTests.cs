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
