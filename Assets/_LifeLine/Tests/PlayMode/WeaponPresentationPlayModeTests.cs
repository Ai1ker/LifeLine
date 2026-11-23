using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Lifeline.Tests
{
    public sealed class WeaponPresentationPlayModeTests
    {
        [UnityTest]
        public IEnumerator SpeargunHitsArmoredBirdForTwoRegardlessOfOtherDamage()
        {
            var previous = GameSession.Current;
            var random = Random.state;
            float previousScale = Time.timeScale;
            Time.timeScale = 1;
            var root = new GameObject("Speargun bird hit fixture");
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            LifelineSpear spear = null;
            try
            {
                var game = root.AddComponent<GameSession>();
                game.autoStart = false;
                game.enabled = false;
                game.tuning = tuning;
                game.phase = SessionPhase.Playing;
                game.hasSpeargun = true;
                game.spearDamage = 1f;
                game.captainBuffs.attack = tuning.fishBuffSeconds;
                var crew = new GameObject("Captain");
                crew.transform.SetParent(root.transform);
                crew.transform.position = new Vector3(300, 300, 0);
                var captain = crew.AddComponent<CrewActor>();
                captain.enabled = false;
                captain.session = game;
                game.captain = captain;
                var bird = game.SpawnEnemy(EnemyKind.ArmoredBird, captain.Position + Vector3.right * 3);
                bird.transform.SetParent(root.transform);
                bird.enabled = false;
                Physics.SyncTransforms();

                Assert.That(bird.health, Is.EqualTo(2f));
                Assert.That(game.FireSpear(captain), Is.True);
                spear = game.activeSpears[(int)CrewRole.Captain];
                for (float deadline = Time.time + 2; Time.time < deadline && !spear.hasHit;) yield return null;
                Assert.That(spear.hasHit, Is.True);
                Assert.That(bird.health, Is.Zero);
                Assert.That(bird.IsAlive, Is.False);
            }
            finally
            {
                if (spear != null) Object.DestroyImmediate(spear.gameObject);
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(tuning);
                GameSession.RestoreCurrentAfterValidation(previous);
                Random.state = random;
                Time.timeScale = previousScale;
            }
        }

        [UnityTest] public IEnumerator SpearStopsAtTenMetresShowsTetherAndCanBeRecalledEarly()
        {
            var previous=GameSession.Current;float scale=Time.timeScale;Time.timeScale=1;
            var root=new GameObject("Spear range fixture");var tuning=ScriptableObject.CreateInstance<GameTuning>();
            var session=root.AddComponent<GameSession>();session.autoStart=false;session.enabled=false;session.tuning=tuning;session.phase=SessionPhase.Playing;session.hasSpeargun=true;
            var actorObject=new GameObject("Captain");actorObject.transform.SetParent(root.transform);actorObject.transform.position=new Vector3(300,300,0);
            var actor=actorObject.AddComponent<CrewActor>();actor.enabled=false;actor.session=session;actor.role=CrewRole.Captain;actor.onboard=true;session.captain=actor;
            try
            {
                Assert.That(session.FireSpear(actor),Is.True);var spear=session.activeSpears[0];
                Assert.That(spear.GetComponent<SpearTetherView>(),Is.Not.Null);
                for(float deadline=Time.time+3;Time.time<deadline&&!spear.returning;){yield return null;Assert.That(Vector3.Distance(spear.transform.position,actor.Position),Is.LessThanOrEqualTo(10.01f));}
                Assert.That(spear.returning,Is.True,"maximum range must automatically recall");
                Object.DestroyImmediate(spear.gameObject);
                Assert.That(session.FireSpear(actor),Is.True);spear=session.activeSpears[0];yield return null;
                Assert.That(session.FireSpear(actor),Is.True);Assert.That(spear.returning,Is.True,"second press recalls before maximum range");
                Object.DestroyImmediate(spear.gameObject);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(tuning);GameSession.RestoreCurrentAfterValidation(previous);Time.timeScale=scale;}
        }
    }
}
