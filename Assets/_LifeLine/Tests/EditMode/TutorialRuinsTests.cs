using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    /// <summary>Covers the opening-tutorial ruin state, free cleaning, hint tracking and save round-trip.</summary>
    public sealed class TutorialRuinsTests
    {
        GameObject root; GameSession game, previous; GameTuning tuning; Random.State random; float timeScale;

        [SetUp] public void SetUp()
        {
            previous = GameSession.Current; timeScale = Time.timeScale; random = Random.state;
            root = new GameObject("Tutorial ruins fixture"); game = root.AddComponent<GameSession>(); game.autoStart = false;
            tuning = ScriptableObject.CreateInstance<GameTuning>(); game.tuning = tuning;
            var ship = new GameObject("Ship"); ship.transform.SetParent(root.transform); game.ship = ship.AddComponent<ShipMotor>();
            game.captain = Actor(); game.diver = Actor(); game.Initialize();
        }
        CrewActor Actor()
        {
            var obj = new GameObject("Crew"); obj.transform.SetParent(root.transform);
            var actor = obj.AddComponent<CrewActor>(); actor.enabled = false; return actor;
        }
        [TearDown] public void TearDown()
        {
            Object.DestroyImmediate(root); Object.DestroyImmediate(tuning);
            GameSession.RestoreCurrentAfterValidation(previous); Time.timeScale = timeScale; Random.state = random;
        }

        [Test] public void NewGame_AllStationsAreRuins_AndTutorialActive()
        {
            Assert.That(game.TutorialActive, Is.True);
            for (int i = 0; i < GameSession.StationCount; i++)
                Assert.That(game.IsStationRuin((StationKind)i), Is.True, "Station " + i + " should start as a ruin");
        }

        [Test] public void CompletingRestoration_UnlocksOnlyThatStation()
        {
            var kind = StationKind.Turret;
            Assert.That(game.IsStationRuin(kind), Is.True);
            game.CompleteRestoration(kind);
            Assert.That(game.IsStationRestored(kind), Is.True);
            Assert.That(game.IsStationRuin(StationKind.Generator), Is.True, "Other stations stay ruined");
        }

        [Test] public void UnlockingGatedDevice_AutoRestoresStation_ButKeepsOthersRuined()
        {
            // Filter starts as a ruin. Unlocking the device must skip the 3-second clean.
            Assert.That(game.IsStationRuin(StationKind.Filter), Is.True);
            Assert.That(game.IsStationUnlocked(StationKind.Filter), Is.False);
            game.UnlockEquipment("filter");
            Assert.That(game.IsStationUnlocked(StationKind.Filter), Is.True, "device is built");
            Assert.That(game.IsStationRestored(StationKind.Filter), Is.True, "ruin auto-cleaned on unlock");
            Assert.That(game.IsStationRuin(StationKind.Generator), Is.True, "other devices stay ruined");
            Assert.That(game.IsStationRuin(StationKind.Workbench), Is.True, "normal stations stay ruined");
        }

        [Test] public void UnlockAutoRestore_IsIdempotent_AndFiresEventOnce()
        {
            var kind = StationKind.Generator;
            int fired = 0;
            game.StationRestored += k => { if (k == kind) fired++; };
            game.AutoRestoreStationOnUnlock(kind);            // still locked -> no-op
            Assert.That(fired, Is.EqualTo(0));
            Assert.That(game.IsStationRuin(kind), Is.True);

            game.UnlockEquipment("generator");
            Assert.That(fired, Is.EqualTo(1), "unlock fires one restore");
            game.AutoRestoreStationOnUnlock(kind);            // already restored -> no-op
            Assert.That(fired, Is.EqualTo(1), "repeat call is harmless");
        }

        [Test] public void AutoRestore_IgnoresNonGatedStations_AndLegacySaves()
        {
            // Non-gated stations never auto-clean from equipment unlock.
            game.AutoRestoreStationOnUnlock(StationKind.Workbench);
            Assert.That(game.IsStationRuin(StationKind.Workbench), Is.True);

            // Legacy saves have the tutorial off; everything stays usable, auto-restore is a no-op.
            game.DisableTutorialForTests();
            game.AutoRestoreStationOnUnlock(StationKind.Generator);
            Assert.That(game.IsStationRestored(StationKind.Generator), Is.True);
        }

        [Test] public void CompletingRestoration_FiresStationRestoredByRole_WithTargetRole()
        {
            var kind = StationKind.Workbench;
            StationKind firedKind = (StationKind)(-1);
            CrewRole firedRole = (CrewRole)(-1);
            game.StationRestoredByRole += (k, r) => { firedKind = k; firedRole = r; };
            game.CompleteRestoration(kind, CrewRole.Diver);
            Assert.That(firedKind, Is.EqualTo(kind));
            Assert.That(firedRole, Is.EqualTo(CrewRole.Diver));
        }

        [Test] public void CompletingRestoration_DefaultRole_IsCaptain()
        {
            var kind = StationKind.Circuit;
            CrewRole firedRole = (CrewRole)(-1);
            game.StationRestoredByRole += (k, r) => { firedRole = r; };
            game.CompleteRestoration(kind);
            Assert.That(firedRole, Is.EqualTo(CrewRole.Captain));
        }

        [Test] public void StationHint_IsShownOnlyOnce()
        {
            var kind = StationKind.Fishing;
            Assert.That(game.MarkStationHintSeen(kind), Is.True);
            Assert.That(game.MarkStationHintSeen(kind), Is.False);
        }

        [Test] public void RuinState_SurvivesSaveRoundTrip()
        {
            game.CompleteRestoration(StationKind.DivePreparation);
            game.MarkStationHintSeen(StationKind.DivePreparation);
            var data = game.CaptureSaveData();
            Assert.That(data.state.tutorialVersion, Is.EqualTo(1));
            Assert.That(data.state.stationUnlocked.Length, Is.EqualTo(GameSession.StationCount));
            Assert.That(data.state.stationUnlocked[(int)StationKind.DivePreparation], Is.True);
            Assert.That(data.state.stationUnlocked[(int)StationKind.Winch], Is.False);
            Assert.That(data.state.stationHintSeen[(int)StationKind.DivePreparation], Is.True);

            // Fresh session restores the saved ruin state.
            var root2 = new GameObject("Restore fixture"); var game2 = root2.AddComponent<GameSession>(); game2.autoStart = false;
            game2.tuning = tuning;
            var ship2 = new GameObject("Ship2"); ship2.transform.SetParent(root2.transform); game2.ship = ship2.AddComponent<ShipMotor>();
            game2.captain = Actor(); game2.diver = Actor(); game2.Initialize();
            string error;
            Assert.That(game2.TryRestoreSaveData(data, out error), Is.True, error);
            Assert.That(game2.IsStationRestored(StationKind.DivePreparation), Is.True);
            Assert.That(game2.IsStationRuin(StationKind.Winch), Is.True);
            Object.DestroyImmediate(root2);
        }

        [Test] public void LegacySave_TreatsAllStationsAsUnlocked()
        {
            game.DisableTutorialForTests(); // tutorialVersion = 0
            var data = game.CaptureSaveData();
            Assert.That(data.state.tutorialVersion, Is.EqualTo(0));
            Assert.That(game.IsStationRestored(StationKind.Turret), Is.True);
            Assert.That(game.IsStationRestored(StationKind.Winch), Is.True);
        }

        [Test] public void Milestone_FiresOnceAndSurvivesSaveRoundTrip()
        {
            int fired = 0;
            game.MilestoneReached += m => { if (m == TutorialMilestone.FirstDive) fired++; };
            Assert.That(game.ReachMilestone(TutorialMilestone.FirstDive), Is.True);
            Assert.That(game.ReachMilestone(TutorialMilestone.FirstDive), Is.False, "Same milestone must not fire twice");
            Assert.That(fired, Is.EqualTo(1));

            var data = game.CaptureSaveData();
            Assert.That(data.state.tutorialMilestoneSeen[(int)TutorialMilestone.FirstDive], Is.True);

            var root2 = new GameObject("Restore fixture"); var game2 = root2.AddComponent<GameSession>(); game2.autoStart = false;
            game2.tuning = tuning;
            var ship2 = new GameObject("Ship2"); ship2.transform.SetParent(root2.transform); game2.ship = ship2.AddComponent<ShipMotor>();
            game2.captain = Actor(); game2.diver = Actor(); game2.Initialize();
            string error;
            Assert.That(game2.TryRestoreSaveData(data, out error), Is.True, error);
            Assert.That(game2.ReachMilestone(TutorialMilestone.FirstDive), Is.False, "Restored save keeps milestone seen");
            Assert.That(game2.ReachMilestone(TutorialMilestone.FirstCraft), Is.True, "Other milestones still available");
            Object.DestroyImmediate(root2);
        }

        [Test] public void LegacySave_MilestonesNeverFire()
        {
            game.DisableTutorialForTests();
            Assert.That(game.ReachMilestone(TutorialMilestone.FirstDive), Is.False);
            Assert.That(game.ReachMilestone(TutorialMilestone.CircuitRepaired), Is.False);
        }
    }
}
