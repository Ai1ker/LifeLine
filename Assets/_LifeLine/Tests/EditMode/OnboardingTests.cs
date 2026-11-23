using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline.Tests
{
    public sealed class OnboardingTests
    {
        GameObject root; GameSession game, previous; GameTuning tuning; TutorialProgress tutorial; float scale; Random.State random;
        [SetUp] public void SetUp()
        {
            previous=GameSession.Current;scale=Time.timeScale;random=Random.state;
            root=new GameObject("Action-driven onboarding regression");
            game=root.AddComponent<GameSession>();game.enabled=false;game.autoStart=false;
            tuning=ScriptableObject.CreateInstance<GameTuning>();tuning.maximumSharks=0;game.tuning=tuning;
            game.ship=Child("Ship").AddComponent<ShipMotor>();game.ship.enabled=false;
            game.captain=Child("Captain").AddComponent<CrewActor>();game.captain.enabled=false;
            game.diver=Child("Diver").AddComponent<CrewActor>();game.diver.enabled=false;
            game.Initialize();tutorial=root.AddComponent<TutorialProgress>();tutorial.Initialize(game);
        }
        GameObject Child(string name){var obj=new GameObject(name);obj.transform.SetParent(root.transform);return obj;}
        [TearDown]public void TearDown()
        {
            Object.DestroyImmediate(root);Object.DestroyImmediate(tuning);GameSession.RestoreCurrentAfterValidation(previous);
            Time.timeScale=scale;Random.state=random;
        }
        [Test]public void BasicsRequireActualActionsRememberEarlierActionsAndNeverAdvanceOnTime()
        {
            tutorial.Tick(1000);Assert.That(tutorial.Step,Is.Zero);
            tutorial.Observe(TutorialAction.DiverDive|TutorialAction.Collect);Assert.That(tutorial.Step,Is.Zero);
            var recipe=ScriptableObject.CreateInstance<RecipeDefinition>();recipe.output=new[]{new ResourceAmount(ResourceKind.Plank,1)};
            game.jobs.Add(new CraftJob{recipe=recipe,owner=CrewRole.Captain,duration=40});
            tutorial.Tick(0);Assert.That(tutorial.Step,Is.EqualTo(1));
            tutorial.Observe(TutorialAction.ViewControl);Assert.That(tutorial.Step,Is.EqualTo(4));
            tutorial.Observe(TutorialAction.LoadCargo|TutorialAction.AttachCargo|TutorialAction.Hide|TutorialAction.CaptainWinchRecovery);
            Assert.That(tutorial.Complete);Object.DestroyImmediate(recipe);
        }
        [Test]public void RealBackpackCargoAndWinchStatesAdvanceTheCorrespondingObservations()
        {
            var world=Child("World").AddComponent<WorldGenerator>();world.enabled=false;world.session=game;game.world=world;
            var crate=Child("Empty cargo").AddComponent<LootPickup>();crate.world=world;crate.isCrate=true;world.pickups.Add(crate);
            game.diver.SetOnboard(false,game.BoatAnchor+Vector3.down*5);crate.transform.position=game.diver.Position;
            game.diver.backpack.Add(ResourceKind.Wood);tutorial.Tick(0);
            Assert.That(tutorial.observedActions.HasFlag(TutorialAction.DiverDive|TutorialAction.Collect));
            Assert.That(crate.TryLoad(game.diver));tutorial.Tick(0);Assert.That(tutorial.observedActions.HasFlag(TutorialAction.LoadCargo));
            crate.attached=true;game.diverHidden=true;game.winchActive=true;game.winchOperatorRole=CrewRole.Captain;tutorial.Tick(0);
            Assert.That(tutorial.observedActions.HasFlag(TutorialAction.AttachCargo|TutorialAction.Hide));
            game.diver.SetOnboard(true,game.ShipPoint(game.diverSpawnLocal));tutorial.Tick(0);
            Assert.That(tutorial.observedActions.HasFlag(TutorialAction.CaptainWinchRecovery));
        }
        [Test]public void NoticesQueueOnceStayIndependentOfBasicsAndSurviveSaveRestore()
        {
            tutorial.QueueNotice(0);tutorial.QueueNotice(1);tutorial.QueueNotice(2);Assert.That(tutorial.activeNotice,Is.Zero);
            tutorial.remindersVisible=false;tutorial.Tick(100);Assert.That(tutorial.activeNotice,Is.Zero);
            var save=tutorial.Capture();tutorial.Restore(save);Assert.That(tutorial.activeNotice,Is.Zero);Assert.That(tutorial.Step,Is.Zero);
            tutorial.DismissNotice();Assert.That(tutorial.activeNotice,Is.EqualTo(1));
            tutorial.DismissNotice();Assert.That(tutorial.activeNotice,Is.EqualTo(2));
            tutorial.DismissNotice();tutorial.QueueNotice(0);Assert.That(tutorial.activeNotice,Is.EqualTo(-1));
            Assert.That(tutorial.Step,Is.Zero);
        }
        [Test]public void FirstEventReminderLastsFiveVisibleSecondsAndStormCopyUsesFourSecondRescue()
        {
            tutorial.QueueNotice(2);Assert.That(tutorial.remainingNoticeSeconds,Is.EqualTo(5));
            Assert.That(tutorial.NoticeSource,Does.Contain("4 秒"));
            tutorial.Tick(4.99f);Assert.That(tutorial.activeNotice,Is.EqualTo(2));
            tutorial.Tick(.02f);Assert.That(tutorial.activeNotice,Is.EqualTo(-1));Assert.That(tutorial.Step,Is.Zero);
        }
        [Test]public void WorkbenchHintOmitsRepairInstructionsButRepairBenchRetainsThem()
        {
            var panel=Child("Panel controller").AddComponent<CrewStationPanel>();panel.enabled=false;
            panel.panel=Child("Panel");
            panel.titleText=new GameObject("Title",typeof(RectTransform),typeof(Text)).GetComponent<Text>();panel.titleText.transform.SetParent(root.transform);
            panel.detailText=new GameObject("Detail",typeof(RectTransform),typeof(Text)).GetComponent<Text>();panel.detailText.transform.SetParent(root.transform);
            panel.liveText=new GameObject("Feedback",typeof(RectTransform),typeof(Text)).GetComponent<Text>();panel.liveText.transform.SetParent(root.transform);
            panel.Initialize(null,game,game.captain);panel.Open(StationKind.Workbench);
            Assert.That(panel.detailText.text,Does.Not.Contain("快速修补").And.Not.Contain("Quick"));
            game.repairBenchInstalled=true;panel.Close();panel.Open(StationKind.HullRepair);
            Assert.That(panel.detailText.text,Does.Contain("快速修补"));panel.Close();
        }
        [Test]public void LegacySaveSkipsBasicsWhileNewGameResetsAllLessonsAndWarnings()
        {
            var oldSave=game.CaptureSaveData();oldSave.tutorial=null;
            Assert.That(game.TryRestoreSaveData(oldSave,out string error),Is.True,error);
            Assert.That(tutorial.Complete);tutorial.QueueNotice(1);Assert.That(tutorial.activeNotice,Is.EqualTo(1));
            tutorial.Observe(TutorialAction.Collect);tutorial.ResetForNewGame();Assert.That(tutorial.Step,Is.Zero);
            Assert.That(tutorial.seenNotices,Is.Zero);Assert.That(tutorial.activeNotice,Is.EqualTo(-1));
        }
        [Test]public void FreshBootCreatesTutorialBeforeTheFirstAutomaticSaveCanCapture()
        {
            Object.DestroyImmediate(tutorial);
            var hud=Child("HUD before boot").AddComponent<GameHUDController>();hud.enabled=false;hud.session=game;
            var save=root.AddComponent<GameSaveManager>();save.enabled=false;save.bootOnStart=false;save.session=game;save.hud=hud;
            GameSaveManager.RequestNewGame();Assert.That(save.BootSession());
            tutorial=game.GetComponent<TutorialProgress>();Assert.That(tutorial,Is.Not.Null);
            var firstSave=game.CaptureSaveData(hud);Assert.That(firstSave.tutorial,Is.Not.Null);
            Assert.That(firstSave.tutorial.skipBasics,Is.False);Assert.That(firstSave.tutorial.observedActions,Is.Zero);
        }
        [Test]public void ExplicitUiTimingMigrationUpdatesOnlyRecognizedDefaultsAndBilingualReviveCopy()
        {
            var menu=Child("Menu timing").AddComponent<MainMenuController>();menu.enabled=false;menu.newGameFadeSeconds=.65f;tutorial.noticeSeconds=16;
            Assert.That(Lifeline.Editor.OnboardingUiInstaller.UpdateKnownTiming(menu,tutorial));
            Assert.That(menu.newGameFadeSeconds,Is.EqualTo(.2f));Assert.That(menu.newGameFadeOutSeconds,Is.EqualTo(.1f));Assert.That(tutorial.noticeSeconds,Is.EqualTo(5));
            menu.newGameFadeSeconds=.4f;tutorial.noticeSeconds=8;
            Assert.That(Lifeline.Editor.OnboardingUiInstaller.UpdateKnownTiming(menu,tutorial),Is.False);
            Assert.That(menu.newGameFadeSeconds,Is.EqualTo(.4f));Assert.That(tutorial.noticeSeconds,Is.EqualTo(8));
            var table=ScriptableObject.CreateInstance<LocalizationTable>();
            try
            {
                table.entries.Add(new LocalizationTable.Entry("revive.captain","按住交互5秒，扶起船长；松开或离开会中断。","Hold Interact for 5 seconds to revive the captain. Releasing or moving away interrupts the rescue."));
                table.entries.Add(new LocalizationTable.Entry("revive.diver","按住交互5秒，扶起潜水员；松开或离开会中断。","Custom developer instruction"));
                Assert.That(Lifeline.Editor.DefaultLocalizationBuilder.UpdateKnownUiTimingCopy(table));
                Assert.That(table.entries[0].zh,Does.Contain("4秒"));Assert.That(table.entries[0].en,Does.Contain("4 seconds"));
                Assert.That(table.entries[1].en,Is.EqualTo("Custom developer instruction"));
                Assert.That(Lifeline.Editor.DefaultLocalizationBuilder.UpdateKnownUiTimingCopy(table),Is.False);
            }
            finally{Object.DestroyImmediate(table);}
        }
        [Test]public void CaptainOxygenIsContextualLeftSideAndInvalidContinueIsHidden()
        {
            var hudObject=Child("HUD");var hud=hudObject.AddComponent<GameHUDController>();hud.enabled=false;hud.session=game;
            var view=hudObject.AddComponent<OnboardingView>();view.BuildMissingViews();view.Initialize(hud);
            view.RefreshView();Assert.That(view.captainOxygenRoot.activeSelf,Is.False);
            game.captain.SetOnboard(false,game.BoatAnchor+Vector3.down*3);game.captain.oxygen=25;game.captain.oxygenMax=50;
            view.RefreshView();Assert.That(view.captainOxygenRoot.activeSelf);Assert.That(view.captainOxygenText.text,Does.Contain("25 / 50s"));
            Assert.That(((RectTransform)view.captainOxygenRoot.transform).anchorMax.x,Is.LessThan(.5f));
            Assert.That(((RectTransform)view.captainOxygenRoot.transform).anchorMax.y,Is.LessThan(.799f),"Captain oxygen must stay below the existing hull label strip.");
            Assert.That(((RectTransform)view.instructionPanel.transform).anchorMax.y,Is.LessThan(((RectTransform)view.captainOxygenRoot.transform).anchorMin.y),"Lesson and captain oxygen must not overlap.");
            Assert.That(view.captainOxygenFill.rectTransform.anchorMax.x,Is.EqualTo(.5f).Within(.001f));
            game.captain.SetOnboard(true,game.ShipPoint(game.captainSpawnLocal));view.RefreshView();Assert.That(view.captainOxygenRoot.activeSelf,Is.False);
            var menu=Child("Menu").AddComponent<MainMenuController>();menu.enabled=false;
            menu.continueButton=Child("Continue").AddComponent<Button>();menu.SetContinueAvailable(false);Assert.That(menu.continueButton.gameObject.activeSelf,Is.False);
            menu.SetContinueAvailable(true);Assert.That(menu.continueButton.gameObject.activeSelf);Assert.That(menu.continueButton.interactable);
            Assert.That(SceneFadeTransition.FadeStep(0,1,.325f,.65f),Is.EqualTo(.5f).Within(.001f));
            Assert.That(SceneFadeTransition.FadeStep(.5f,0,.65f,.65f),Is.Zero);
        }
    }
}
