using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Lifeline.Tests
{
    public sealed class SurvivalInteractionPlayModeTests
    {
        GameObject root;GameSession game,previous;GameTuning tuning;float scale;Random.State random;
        [SetUp] public void SetUp()
        {
            previous=GameSession.Current;scale=Time.timeScale;random=Random.state;
            root=new GameObject("Survival interaction regression");game=root.AddComponent<GameSession>();game.autoStart=false;game.enabled=false;
            tuning=ScriptableObject.CreateInstance<GameTuning>();tuning.maximumSharks=0;tuning.birdInterval=10000;game.tuning=tuning;
            game.ship=Child("Ship").AddComponent<ShipMotor>();game.ship.enabled=false;
            game.captain=Actor();game.diver=Actor();game.Initialize();
            var anchor=Child("Winch").AddComponent<StationAnchor>();anchor.kind=StationKind.Winch;anchor.transform.SetParent(game.ship.transform);anchor.transform.position=game.captain.Position;
            var world=Child("World").AddComponent<WorldGenerator>();world.geometryLayer=8;world.session=game;world.tuning=tuning;game.world=world;
        }
        GameObject Child(string name){var obj=new GameObject(name);obj.transform.SetParent(root.transform);return obj;}
        CrewActor Actor(){var obj=Child("Crew");obj.layer=9;var actor=obj.AddComponent<CrewActor>();actor.enabled=false;return actor;}
        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes();foreach(var enemy in game.enemies.ToArray())if(enemy!=null)Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root);Object.DestroyImmediate(tuning);GameSession.RestoreCurrentAfterValidation(previous);Time.timeScale=scale;Random.state=random;
        }
        [UnityTest] public IEnumerator MeleeCannotRetriggerBeforeItsAnimationOrCooldownFinishes()
        {
            var view=game.captain.gameObject.AddComponent<CrewPresentation>();view.actor=game.captain;view.swingSeconds=.8f;
            int events=0;game.captain.PresentationEvent+=(actor,kind,strength)=>{if(kind==CrewVisualEvent.Melee)events++;};
            Assert.That(game.MeleeAttack(game.captain));for(int i=0;i<50;i++)Assert.That(game.MeleeAttack(game.captain),Is.False);
            game.StepSimulation(.8f);Assert.That(game.MeleeAttack(game.captain),Is.False,"animation still running even after simulation cooldown expires");
            yield return new WaitForSeconds(.85f);Assert.That(game.MeleeAttack(game.captain));Assert.That(events,Is.EqualTo(2));
        }
        [UnityTest] public IEnumerator AnEmptyUnderwaterSwingDrawsTheFacingSectorAtGameplayReach()
        {
            var view = game.diver.gameObject.AddComponent<CrewPresentation>();
            view.swingSeconds = 1f;
            game.diver.RestorePose(false, game.BoatAnchor + Vector3.down * 5, Vector3.zero, Vector2.up);
            Assert.That(game.hasHarpoon, Is.False);
            Assert.That(game.MeleeAttack(game.diver), Is.True);
            yield return null;
            Assert.That(view.meleeStroke, Is.Not.Null);
            Assert.That(view.meleeStroke.enabled, Is.True);
            Vector3 origin = view.meleeStroke.GetPosition(0);
            Vector3 edge = view.meleeStroke.GetPosition(1) - origin;
            Assert.That(Vector3.Angle(Vector3.up, edge), Is.EqualTo(67.5f).Within(.1f));
            Assert.That(edge.magnitude, Is.EqualTo(game.MeleeReach).Within(.001f));
        }
        Text Label(string name,Transform parent)
        {var obj=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text));obj.transform.SetParent(parent,false);return obj.GetComponent<Text>();}
        [UnityTest] public IEnumerator SwitchingWorkbenchAndRepairStationNeverReusesTheirJobsOrQte()
        {
            var craft=ScriptableObject.CreateInstance<RecipeDefinition>();var repair=ScriptableObject.CreateInstance<RecipeDefinition>();
            try
            {
                craft.id="craft";craft.displayName="Craft";craft.cost=new ResourceAmount[0];repair.id="repair";repair.displayName="Repair";repair.repairAmount=10;repair.cost=new ResourceAmount[0];game.recipes=new[]{craft,repair};game.upgradeLevels[(int)UpgradeKind.Workbench]=3;game.hull=50;game.repairBenchInstalled=true;
                Assert.That(game.TryCraft("craft",game.captain));Assert.That(game.TryCraft("repair",game.captain));var crafting=game.jobs[0];var fixing=game.jobs[1];
                var panel=Child("Panel controller").AddComponent<CrewStationPanel>();panel.panel=new GameObject("Panel",typeof(RectTransform));panel.panel.transform.SetParent(root.transform);panel.titleText=Label("Title",panel.panel.transform);panel.detailText=Label("Detail",panel.panel.transform);panel.liveText=Label("Live",panel.panel.transform);
                panel.actionsRoot=new GameObject("Actions",typeof(RectTransform)).GetComponent<RectTransform>();panel.actionsRoot.SetParent(panel.panel.transform);
                var template=Label("Button template",root.transform);panel.actionButtonPrefab=template.gameObject.AddComponent<Button>();template.gameObject.SetActive(false);
                var rhythm=new GameObject("Rhythm container",typeof(RectTransform));rhythm.transform.SetParent(panel.panel.transform);var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(rhythm.transform);panel.rhythmFill=fill.GetComponent<Image>();
                panel.Initialize(null,game,game.captain);panel.Open(StationKind.Workbench);Assert.That(panel.selectedJobId,Is.EqualTo(crafting.id));
                panel.Close();Assert.That(panel.selectedJobId,Is.Zero);panel.Open(StationKind.HullRepair);Assert.That(panel.selectedJobId,Is.EqualTo(fixing.id));
                crafting.elapsed=crafting.nextCue;fixing.elapsed=fixing.nextCue;
                panel.selectedJobId=crafting.id;panel.HandleCommand(new CrewCommand{move=Vector2.up});Assert.That(crafting.attempted,Is.False,"even a stale selection cannot calibrate another station's job");
                yield return null;Assert.That(panel.liveText.text,Does.Not.Contain("Craft"));panel.Close();Assert.That(game.jobs.Count,Is.EqualTo(2));
            }
            finally{Object.DestroyImmediate(craft);Object.DestroyImmediate(repair);}
        }
        [UnityTest] public IEnumerator NightManualAndPoweredWinchesRecoverAnUnconsciousDiverAndCargoWithNoPower()
        {
            // T3 must fall back to manual calibration if nighttime devices empty the battery.
            game.SetDayClock(3,.9f);game.upgradeLevels[(int)UpgradeKind.Rope]=3;game.circuitRepaired=true;game.power=0;
            Assert.That(game.BeginDive());game.diver.RestorePose(false,game.BoatAnchor+Vector3.down*5,Vector3.zero,Vector2.up);game.diver.backpack.AddRange(new[]{ResourceKind.Fiber,ResourceKind.Metal});game.FaintDiver("fixture");
            var bag=game.world.pickups.Single(p=>p.isLostCargo);bag.attached=true;game.RegisterAttachedCargo(bag);
            game.SetWinchActive(true);Assert.That(game.winchActive);Assert.That(game.winchElectric,Is.False);
            for(int i=0;i<20&&(!game.diver.onboard||!bag.consumed);i++)
            {game.TryWinchQte(game.WinchQteCenter);game.StepSimulation(tuning.winchCycle+.01f);yield return null;}
            Assert.That(game.diver.onboard,Is.True,"pos="+game.diver.Position+" anchor="+game.BoatAnchor+" active="+game.winchActive+" targets="+game.WinchTargetCount+" blocked="+game.diverRope.IsBlocked+" path="+game.diverRope.PathLength+" cap="+game.captain.Position+" phase="+game.phase+" feedback="+game.winchFeedback);Assert.That(bag.consumed,Is.True,"cargo="+bag.transform.position+" active="+game.winchActive+" targets="+game.WinchTargetCount);Assert.That(game.phase,Is.EqualTo(SessionPhase.Playing));
            game.power=100;game.BeginDive();game.diver.RestorePose(false,game.BoatAnchor+Vector3.down*4,Vector3.zero,Vector2.up);game.SetWinchActive(true);game.SetWinchActive(false);
            for(int i=0;i<40&&!game.diver.onboard;i++){game.StepSimulation(.1f);yield return null;}Assert.That(game.diver.onboard,Is.True,"powered T3 continues after leaving panel at night");
        }
        [UnityTest] public IEnumerator AquaticBodiesCollideWithCrewButIgnoreWallsAndStillLandContactAttacks()
        {
            game.BeginDive();game.diver.RestorePose(false,new Vector3(0,-30,0),Vector3.zero,Vector2.right);tuning.maximumSharks=8;
            foreach(var kind in new[]{EnemyKind.Piranha,EnemyKind.Shark,EnemyKind.Shadow})
            {
                var enemy=game.SpawnEnemy(kind,game.diver.Position+Vector3.right*3,true);enemy.enabled=false;
                Assert.That(enemy.crewCollision.solid.isTrigger,Is.False);Assert.That((enemy.crewCollision.solid.excludeLayers.value&(1<<8))!=0);Assert.That((enemy.crewCollision.solid.excludeLayers.value&(1<<9))==0);
                if(kind!=EnemyKind.Shadow)
                {
                    enemy.state=EnemyState.Lunge;enemy.targetPoint=game.diver.Position;game.diverInvulnerable=0;float before=game.diverBites;
                    for(int i=0;i<100&&game.diverBites==before&&!game.diverFainted;i++){enemy.StepEnemy(.02f);yield return new WaitForFixedUpdate();}
                    Assert.That(game.diverBites,Is.GreaterThan(before),kind+" must land a contact attack; state="+enemy.state+" enemy="+enemy.transform.position+" diver="+game.diver.Position);
                    game.diverBites=0;game.diverFainted=false;game.diver.fainted=false;
                }
                Object.DestroyImmediate(enemy.gameObject);
            }
        }
        [Test] public void PauseCanvasRendersAboveSurfaceBearingAndSettingsHaveTheirOwnHigherLayer()
        {
            var canvas=Child("HUD").AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var hud=canvas.gameObject.AddComponent<GameHUDController>();hud.enabled=false;hud.session=game;hud.pausePanel=new GameObject("Pause",typeof(RectTransform));hud.pausePanel.transform.SetParent(canvas.transform);
            var bearing=Child("Bearing").AddComponent<Canvas>();bearing.renderMode=RenderMode.ScreenSpaceOverlay;bearing.sortingOrder=3;
            hud.Initialize();hud.SetPause(true);hud.SetPause(false);hud.pausePanel.GetComponent<Canvas>().overrideSorting=false;hud.pausePanel.GetComponent<Canvas>().sortingOrder=0;hud.SetPause(true);Assert.That(hud.pausePanel.GetComponent<Canvas>().overrideSorting);Assert.That(hud.pausePanel.GetComponent<Canvas>().sortingOrder,Is.GreaterThan(bearing.sortingOrder));hud.SetPause(false);
        }
    }
}
