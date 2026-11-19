using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class GameplayBatchTests
    {
        GameObject root;
        GameSession game,previous;
        GameTuning tuning;
        RecipeDefinition repair,otherRepair;
        float previousScale;
        Random.State previousRandom;
        [SetUp] public void SetUp()
        {
            previous=GameSession.Current;previousScale=Time.timeScale;previousRandom=Random.state;
            root=new GameObject("Gameplay batch fixture");game=root.AddComponent<GameSession>();game.autoStart=false;
            tuning=ScriptableObject.CreateInstance<GameTuning>();tuning.maximumSharks=0;game.tuning=tuning;
            var boat=new GameObject("Ship");boat.transform.SetParent(root.transform);boat.AddComponent<Rigidbody>();game.ship=boat.AddComponent<ShipMotor>();
            game.captain=Crew();game.diver=Crew();game.Initialize();
            repair=ScriptableObject.CreateInstance<RecipeDefinition>();repair.id="test.repair";repair.displayName="Repair";repair.repairAmount=20;
            otherRepair=ScriptableObject.CreateInstance<RecipeDefinition>();otherRepair.id="test.quick";otherRepair.repairAmount=10;
            game.recipes=new[]{repair,otherRepair};
        }
        CrewActor Crew()
        {
            var go=new GameObject("Crew");go.transform.SetParent(root.transform);go.AddComponent<Rigidbody>();go.AddComponent<CapsuleCollider>();
            var actor=go.AddComponent<CrewActor>();actor.enabled=false;return actor;
        }
        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes();
            foreach(var enemy in game.enemies.ToArray())if(enemy!=null)Object.DestroyImmediate(enemy.gameObject);
            foreach(var spear in Object.FindObjectsByType<LifelineSpear>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(spear.session==game)Object.DestroyImmediate(spear.gameObject);
            Object.DestroyImmediate(root);Object.DestroyImmediate(tuning);Object.DestroyImmediate(repair);Object.DestroyImmediate(otherRepair);
            GameSession.RestoreCurrentAfterValidation(previous);Time.timeScale=previousScale;Random.state=previousRandom;
        }
        [Test] public void RepairHealsContinuouslyRejectsSecondRepairAndDoesNotHealTwiceAfterLoad()
        {
            game.repairBenchInstalled=true;
            game.hull=40;game.upgradeLevels[(int)UpgradeKind.Workbench]=3;
            Assert.That(game.TryCraft(repair.id,game.captain),Is.True);
            Assert.That(game.TryCraft(otherRepair.id,game.diver),Is.False,"two slots must not allow two simultaneous hull repairs");
            float duration=game.jobs[0].duration;
            game.craftingManager.Tick(duration*.25f);Assert.That(game.hull,Is.EqualTo(45).Within(.01f));
            GameSaveData data=game.CaptureSaveData();Assert.That(data.jobs[0].repairApplied,Is.EqualTo(5).Within(.01f));
            Assert.That(game.TryRestoreSaveData(data,out string error),Is.True,error);game.StartGame();
            game.craftingManager.Tick(duration*.25f);Assert.That(game.hull,Is.EqualTo(50).Within(.01f));
            game.craftingManager.Tick(duration);Assert.That(game.hull,Is.EqualTo(60).Within(.01f));Assert.That(game.jobs,Is.Empty);
        }
        [Test] public void TurretPriorityIsShadowThenBirdThenShallowSharkAndTierTwoUsesExactCost()
        {
            game.circuitRepaired=true;game.turretInstalled=true;game.turretTier=1;
            tuning.maximumSharks=6;
            var shark=game.SpawnEnemy(EnemyKind.Shark,game.BoatAnchor+new Vector3(1,-30,0));
            shark.transform.position=game.BoatAnchor+new Vector3(1,-8,0);
            var deep=game.SpawnEnemy(EnemyKind.Shark,game.BoatAnchor+new Vector3(1,-25,0));
            var bird=game.SpawnEnemy(EnemyKind.Bird,game.ShipPoint(new Vector3(5,4,0)));
            var shadow=game.SpawnEnemy(EnemyKind.Shadow,game.BoatAnchor+new Vector3(10,-5,0));
            Assert.That(game.SelectTurretTargets(),Is.EqualTo(new[]{shadow}));
            game.AddResource(ResourceKind.Plate,1);game.AddResource(ResourceKind.FiberBundle,3);game.AddResource(ResourceKind.Plank,1);
            Assert.That(game.UpgradeTurret(),Is.True);Assert.That(game.SelectTurretTargets(),Is.EqualTo(new[]{shadow,bird}));
            Assert.That(game.GetResource(ResourceKind.Plate),Is.Zero);Assert.That(game.GetResource(ResourceKind.FiberBundle),Is.Zero);Assert.That(game.GetResource(ResourceKind.Plank),Is.Zero);
            shadow.state=EnemyState.Defeated;Assert.That(game.SelectTurretTargets(),Is.EqualTo(new[]{bird,shark}));
            bird.state=EnemyState.Defeated;shark.state=EnemyState.Defeated;Assert.That(game.SelectTurretTargets(),Is.Empty,"sharks deeper than20m are excluded");
        }
        [Test] public void ChasingSharkInShallowWaterCanBeSavedWithoutRelaxingSpawnDepth()
        {
            tuning.maximumSharks=6;
            Assert.That(game.SpawnEnemy(EnemyKind.Shark,game.BoatAnchor+Vector3.down*8),Is.Null);
            var shark=game.SpawnEnemy(EnemyKind.Shark,game.BoatAnchor+Vector3.down*30);
            shark.transform.position=game.BoatAnchor+Vector3.down*8;shark.state=EnemyState.Pursuit;
            GameSaveData data=game.CaptureSaveData();
            Assert.That(game.TryRestoreSaveData(data,out string error),Is.True,error);
            Assert.That(game.enemies.Single().transform.position,Is.EqualTo(game.BoatAnchor+Vector3.down*8));
            Assert.That(game.enemies.Single().state,Is.EqualTo(EnemyState.Pursuit));
        }
        [Test] public void ClockToolAppliesExactDayAndStormRollIsRepeatableWithCorrectNightGate()
        {
            game.SetDayClock(7,.12f);game.StepSimulation(.01f);Assert.That(game.day,Is.EqualTo(7));Assert.That(game.DayProgress,Is.EqualTo(.12f).Within(.001f));
            game.SetDayClock(2,.9f);Assert.That(game.StormActive,Is.True);
            game.SetDayClock(3,.9f);Assert.That(game.StormActive,Is.False);
            int storms=0;for(int d=4;d<1004;d++){game.SetDayClock(d,.9f);if(game.StormActive)storms++;Assert.That(game.StormActive,Is.EqualTo(GameSession.WeatherRoll(d,game.worldSeed)<.1f));}
            Assert.That(storms,Is.InRange(60,140));
            game.SetDayClock(2,.1f);Assert.That(game.StormActive,Is.False);
        }
        [Test] public void FixedWarningDoesNotFollowCrewAndImpactUsesItsStoredCircle()
        {
            Vector3 point=game.ShipToWaterPoint(game.captain.Position);
            var strike=game.QueueAreaStrike(AreaStrikeKind.Lightning,point,true,1.4f,2.2f,10);
            game.captain.RestorePose(true,game.captain.Position+Vector3.right*5,Vector3.zero,Vector2.right);
            Assert.That(strike.point,Is.EqualTo(point));float hull=game.hull;game.ResolveAreaStrike(strike);
            Assert.That(game.captain.downed,Is.False);Assert.That(game.hull,Is.EqualTo(hull-5));
            game.captain.RestorePose(true,game.WaterToShipPoint(point),Vector3.zero,Vector2.right);game.ResolveAreaStrike(strike);
            Assert.That(game.captain.downed,Is.True);
        }
        [Test] public void CaptainTwoUnitsSuitThreeUnitsAndBothDownedFailWithoutLosingBank()
        {
            game.upgradeLevels[(int)UpgradeKind.DivingSuit]=1;game.AddResource(ResourceKind.Metal,6);
            Assert.That(Physics.GetIgnoreCollision(game.captain.GetComponent<Collider>(),game.diver.GetComponent<Collider>()),Is.True);
            game.diver.ReceiveDamage(1);Assert.That(game.diver.downed,Is.False);game.diverInvulnerable=0;
            game.diver.ReceiveDamage(2);Assert.That(game.diver.downed,Is.True);
            game.captain.ReceiveDamage(2);Assert.That(game.captain.downed,Is.True);
            game.StepSimulation(.2f);Assert.That(game.phase,Is.EqualTo(SessionPhase.Lost));Assert.That(game.GetResource(ResourceKind.Metal),Is.EqualTo(6));
        }
        [Test] public void FlowHighlightOnlyFollowsReciprocalConnectionsAndGeneratedPuzzlesRemainSolvable()
        {
            var pipe=new PipeRepairPuzzle{rows=1,columns=3,sourceRow=0,exitRow=0,masks=new[]{10,5,10}};
            Assert.That(pipe.FlowCells(),Is.EqualTo(new[]{true,false,false}));Assert.That(pipe.IsSolved(),Is.False);
            pipe.RotateTile(1);Assert.That(pipe.FlowCells(),Is.EqualTo(new[]{true,true,true}));Assert.That(pipe.IsSolved(),Is.True);
            for(int seed=0;seed<100;seed++){pipe=new PipeRepairPuzzle();pipe.Generate(seed);pipe.masks=(int[])pipe.solution.Clone();Assert.That(pipe.IsSolved(),Is.True);Assert.That(pipe.FlowCells()[pipe.exitRow*pipe.columns+pipe.columns-1],Is.True);}
        }
        [Test] public void ShadowCyclesBiteDashThenTwoFixedDeckShotsWithBodyScaledRadii()
        {
            game.SetDayClock(4,.9f);game.diver.RestorePose(false,new Vector3(0,-8,0),Vector3.zero,Vector2.right);
            var enemy=game.SpawnEnemy(EnemyKind.Shadow,new Vector3(1,-8,0));enemy.geometryMask=0;
            var tick=typeof(LifelineEnemy).GetMethod("TickShadow",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            float length=game.diver.GetComponent<CapsuleCollider>().height;
            tick.Invoke(enemy,new object[]{.001f});
            Assert.That(game.areaStrikes.Single().kind,Is.EqualTo(AreaStrikeKind.ShadowBite));
            Assert.That(game.areaStrikes.Single().radius,Is.EqualTo(length*1.5f).Within(.001f));
            Assert.That(game.areaStrikes.Single().hullDamage,Is.InRange(5f,10f));
            tick.Invoke(enemy,new object[]{tuning.shadowWindup+.01f});game.ClearAreaStrikes();enemy.attackCooldown=0;
            enemy.transform.position=new Vector3(5,-8,0);tick.Invoke(enemy,new object[]{.001f});
            Assert.That(game.areaStrikes.Single().kind,Is.EqualTo(AreaStrikeKind.ShadowDash));
            Assert.That(game.areaStrikes.Single().radius,Is.EqualTo(length*.7f).Within(.001f));
            Vector3 aim=game.areaStrikes.Single().point;game.diver.RestorePose(false,new Vector3(-5,-8,0),Vector3.zero,Vector2.left);
            Assert.That(game.areaStrikes.Single().point,Is.EqualTo(aim),"dash warning cannot chase a dodging diver");
            tick.Invoke(enemy,new object[]{tuning.shadowWindup+tuning.shadowDashSeconds+.01f});game.ClearAreaStrikes();enemy.attackCooldown=0;
            enemy.transform.position=game.diver.Position+Vector3.right;tick.Invoke(enemy,new object[]{.001f});
            Assert.That(game.areaStrikes.Count,Is.EqualTo(2));
            Assert.That(game.areaStrikes.All(a=>a.kind==AreaStrikeKind.WaterShot&&a.onShipMap&&a.hullDamage==5&&a.radius==tuning.lightningRadius),Is.True);
            Assert.That(game.areaStrikes[0].point,Is.Not.EqualTo(game.areaStrikes[1].point));
            tick.Invoke(enemy,new object[]{tuning.shadowWaterShotWarning+.01f});Assert.That(enemy.shadowAttackIndex,Is.Zero);
        }
        [Test] public void RepairGraphicComponentsHaveCanvasRenderersBeforeGraphicInitialization()
        {
            var go=new GameObject("Pipe rendering fixture",typeof(RectTransform));go.transform.SetParent(root.transform);
            var pipe=go.AddComponent<PipeShapeGraphic>();Assert.That(pipe.GetComponent<CanvasRenderer>(),Is.Not.Null);
            var wireObject=new GameObject("Wire rendering fixture",typeof(RectTransform));wireObject.transform.SetParent(root.transform);
            var wire=wireObject.AddComponent<RepairWireGraphic>();Assert.That(wire.GetComponent<CanvasRenderer>(),Is.Not.Null);
        }
        [Test] public void PendingShadowImpactsAndPhaseRoundTripWithTurretTier()
        {
            var enemy=game.SpawnEnemy(EnemyKind.Shadow,game.BoatAnchor+Vector3.down*5);enemy.shadowAttackIndex=1;enemy.shadowPhase=2;enemy.shadowAttackClock=1.1f;enemy.shadowAim=new Vector3(2,-8,0);
            game.turretInstalled=true;game.turretTier=2;
            game.QueueAreaStrike(AreaStrikeKind.WaterShot,new Vector3(4,.9f,0),true,1.4f,2.2f,5);
            GameSaveData data=game.CaptureSaveData();
            Assert.That(game.TryRestoreSaveData(data,out string error),Is.True,error);Assert.That(game.turretTier,Is.EqualTo(2));
            Assert.That(game.enemies.Single().shadowPhase,Is.EqualTo(2));Assert.That(game.enemies.Single().shadowAim,Is.EqualTo(enemy.shadowAim));
            Assert.That(game.areaStrikes.Single().remaining,Is.EqualTo(2.2f));Assert.That(game.areaStrikes.Single().point,Is.EqualTo(new Vector3(4,.9f,0)));
        }
    }
}
