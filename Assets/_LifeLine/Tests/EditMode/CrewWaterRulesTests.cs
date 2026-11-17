using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
namespace Lifeline.Tests
{
    public sealed class CrewWaterRulesTests
    {
        GameObject root;GameSession game,previous;GameTuning tuning;Random.State random;float scale;
        [SetUp] public void SetUp()
        {
            previous=GameSession.Current;random=Random.state;scale=Time.timeScale;
            root=new GameObject("Crew water rules fixture");game=root.AddComponent<GameSession>();game.autoStart=false;
            tuning=ScriptableObject.CreateInstance<GameTuning>();tuning.maximumSharks=0;game.tuning=tuning;
            var ship=new GameObject("Ship");ship.transform.SetParent(root.transform);ship.AddComponent<Rigidbody>();game.ship=ship.AddComponent<ShipMotor>();
            game.captain=Actor();game.diver=Actor();game.Initialize();
            var station=new GameObject("Winch");station.transform.SetParent(ship.transform);station.transform.position=game.captain.Position;station.AddComponent<StationAnchor>().kind=StationKind.Winch;
        }
        CrewActor Actor()
        {
            var obj=new GameObject("Crew");obj.transform.SetParent(root.transform);obj.AddComponent<Rigidbody>();obj.AddComponent<CapsuleCollider>();
            var actor=obj.AddComponent<CrewActor>();actor.enabled=false;actor.visualRoot=new GameObject("VisualRoot").transform;actor.visualRoot.SetParent(obj.transform);return actor;
        }
        WorldGenerator World(bool generate=false)
        {
            var obj=new GameObject("World");obj.transform.SetParent(root.transform);var world=obj.AddComponent<WorldGenerator>();world.session=game;world.tuning=tuning;
            world.areaCenters=new[]{0f};world.drawLabels=false;game.world=world;if(generate)world.Generate(game.worldSeed);return world;
        }
        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes();foreach(var enemy in game.enemies.ToArray())if(enemy!=null)Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root);Object.DestroyImmediate(tuning);GameSession.RestoreCurrentAfterValidation(previous);Random.state=random;Time.timeScale=scale;
        }
        [Test] public void InteractHidingLocksActionsHidesModelAndPreventsTargetingAndAllDamageUntilSecondPress()
        {
            var world=World();var spot=new GameObject("Hide");spot.transform.SetParent(root.transform);spot.transform.position=game.BoatAnchor+Vector3.down*8;
            var hide=spot.AddComponent<UnderwaterHideSpot>();hide.world=world;world.hideSpots.Add(hide);
            game.diver.RestorePose(false,spot.transform.position,Vector3.zero,Vector2.right);
            game.HandleCommand(game.diver,new CrewCommand{interact=true});
            Assert.That(game.IsHidden(game.diver));Assert.That(game.diver.visualRoot.gameObject.activeSelf,Is.False);Assert.That(game.IsActorInputBlocked(game.diver));
            Assert.That(game.FindWaterTarget(spot.transform.position),Is.Null);
            game.diver.ReceiveDamage(1);Assert.That(game.ReceiveDiverHit("test",false),Is.False);
            game.ResolveAreaStrike(new AreaStrike{point=game.diver.Position,radius=3,onShipMap=false,kind=AreaStrikeKind.ShadowBite});
            Assert.That(game.diverFainted,Is.False);Assert.That(game.diverBites,Is.Zero);
            game.HandleCommand(game.diver,new CrewCommand{move=Vector2.right,collect=true,fire=true,interactHeld=true});Assert.That(game.IsHidden(game.diver));
            game.HandleCommand(game.diver,new CrewCommand{interact=true});
            Assert.That(game.IsHidden(game.diver),Is.False);Assert.That(game.diver.visualRoot.gameObject.activeSelf);
        }
        [Test] public void TierThreeRetainsManualQteWithoutPowerAndFallsBackAfterPowerLoss()
        {
            game.BeginDive();game.diver.RestorePose(false,game.BoatAnchor+Vector3.down*10,Vector3.zero,Vector2.right);
            game.upgradeLevels[(int)UpgradeKind.Rope]=3;game.circuitRepaired=true;game.power=0;
            game.SetWinchActive(true);Assert.That(game.winchActive);Assert.That(game.winchElectric,Is.False);
            float before=game.DiverRopeLength;Assert.That(game.TryWinchQte(tuning.winchCenter));Assert.That(game.DiverRopeLength,Is.LessThan(before));
            game.power=1;game.SetWinchActive(true);Assert.That(game.winchElectric);game.power=0;game.StepSimulation(.01f);
            Assert.That(game.winchElectric,Is.False);Assert.That(game.winchActive);Assert.That(game.TryWinchQte(tuning.winchCenter));
        }
        [Test] public void CaptainUsesBaseOxygenAndOneHitDespiteDiverEquipmentAndFishBuffs()
        {
            game.upgradeLevels[(int)UpgradeKind.Oxygen]=3;game.upgradeLevels[(int)UpgradeKind.DivingSuit]=2;tuning.upgradedSuitImmunity=1;
            game.captainBuffs.oxygen=120;Assert.That(game.BeginDive(game.captain));Assert.That(game.captain.oxygenMax,Is.EqualTo(tuning.baseOxygen));
            Assert.That(game.GetOxygenMax(game.diver),Is.EqualTo(145));game.StepSimulation(1);
            Assert.That(game.captain.oxygen,Is.EqualTo(tuning.baseOxygen-1).Within(.001f));
            game.captain.ReceiveDamage(1);Assert.That(game.IsFainted(game.captain));Assert.That(game.IsActorInputBlocked(game.captain));
            Assert.That(game.GetMaxDiverBites(),Is.EqualTo(2));
        }
        [Test] public void CaptainSelfBoardRequiresContinuousThreeSecondsAndDiverCannotSelfBoard()
        {
            game.BeginDive(game.captain);game.captain.backpack.Add(ResourceKind.Fiber);
            game.HandleCommand(game.captain,new CrewCommand{interactHeld=true});game.StepSimulation(2.9f);Assert.That(game.captain.onboard,Is.False);
            game.HandleCommand(game.captain,new CrewCommand());game.StepSimulation(.1f);Assert.That(game.captainBoardProgress,Is.Zero);
            game.upgradeLevels[(int)UpgradeKind.Rope]=3;game.circuitRepaired=true;game.power=50;game.winchElectric=game.winchSignalAuthorized=true;
            tuning.winchPassiveSpeed=tuning.winchElectricSpeed=0;
            game.HandleCommand(game.captain,new CrewCommand{interactHeld=true});game.StepSimulation(2.9f);Assert.That(game.captain.onboard,Is.False);
            game.StepSimulation(.11f);Assert.That(game.captain.onboard);Assert.That(game.GetResource(ResourceKind.Fiber),Is.EqualTo(1));Assert.That(game.winchElectric||game.winchSignalAuthorized,Is.False);
            game.BeginDive();game.HandleCommand(game.diver,new CrewCommand{interactHeld=true});game.StepSimulation(4);Assert.That(game.diver.onboard,Is.False);
        }
        [Test] public void DiverCanManuallyWinchCaptainAndTwoSwimmersSharePullEfficiency()
        {
            game.BeginDive(game.captain);game.captain.RestorePose(false,game.BoatAnchor+Vector3.down*4,Vector3.zero,Vector2.right);
            game.diver.RestorePose(true,game.captainSpawnLocal,Vector3.zero,Vector2.right);
            game.SetWinchActive(true,game.diver);Assert.That(game.winchActive);Assert.That(game.winchOperatorRole,Is.EqualTo(CrewRole.Diver));
            for(int i=0;i<6&&!game.captain.onboard;i++){game.TryWinchQte(tuning.winchCenter);game.StepSimulation(tuning.winchCycle+.01f);}
            Assert.That(game.captain.onboard);game.BeginDive(game.captain);game.BeginDive(game.diver);
            Assert.That(game.WinchTargetCount,Is.EqualTo(2));Assert.That(game.WinchEfficiency,Is.EqualTo(.5f));
        }
        [Test] public void BothBirdTypesDamageOnlyHullEvenBesideBothCrew()
        {
            game.SetDayClock(2,.9f);foreach(var kind in new[]{EnemyKind.Bird,EnemyKind.ArmoredBird})
            {
                var enemy=game.SpawnEnemy(kind,game.captain.Position);enemy.state=EnemyState.Telegraph;enemy.stateTime=.6f;
                float hull=game.hull;typeof(LifelineEnemy).GetMethod("TickBird",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(enemy,new object[]{.01f});
                Assert.That(game.hull,Is.EqualTo(hull-tuning.birdHullDamage));Assert.That(game.captain.downed,Is.False);Assert.That(game.diver.downed,Is.False);
            }
        }
        [Test] public void NewWaterAndHiddenStateRoundTripsAndCaptainChannelDoesNotResume()
        {
            var world=World(true);game.BeginDive(game.captain);game.BeginDive(game.diver);
            var spot=world.hideSpots[0];game.captain.RestorePose(false,spot.transform.position,Vector3.zero,Vector2.left);
            Assert.That(world.TryHide(game.captain));game.diver.RestorePose(false,game.BoatAnchor+Vector3.down*5,Vector3.zero,Vector2.right);
            game.winchOperatorRole=CrewRole.Diver;var saved=game.CaptureSaveData();
            Assert.That(saved.captain.hidden);Assert.That(game.TryRestoreSaveData(saved,out string error),Is.True,error);
            Assert.That(game.captain.onboard,Is.False);Assert.That(game.IsHidden(game.captain));Assert.That(game.captain.visualRoot.gameObject.activeSelf,Is.False);
            Assert.That(game.captain.oxygenMax,Is.EqualTo(40));Assert.That(game.captainBoardProgress,Is.Zero);
            Assert.That(game.winchOperatorRole,Is.EqualTo(CrewRole.Diver));Assert.That(game.GetCrewLifeline(game.captain),Is.Not.SameAs(game.GetCrewLifeline(game.diver)));
        }
        [Test] public void ForcedNextDayCaveIsDeterministicAcrossSaveAndExpiresFollowingDay()
        {
            var world=World(true);world.forceCaveDay=2;game.SetDayClock(2,.1f);Assert.That(world.generatedCave);
            var saved=game.CaptureSaveData();Assert.That(game.TryRestoreSaveData(saved,out string error),Is.True,error);
            Assert.That(world.generatedCave);Assert.That(world.forceCaveDay,Is.EqualTo(2));
            game.SetDayClock(3,.1f);Assert.That(world.forceCaveDay,Is.EqualTo(-1));
        }
    }
}
