using UnityEngine;

namespace Lifeline
{
    public sealed partial class GameAudio
    {
        public GameHUDController hud;
        public GameSaveManager saveManager;
        [Min(.1f)] public float reelSoundSpeed = .08f;
        [Min(1f), Tooltip("Seconds between underwater sprint breaths at full oxygen. Low oxygen shortens the gap to the clip length.")]
        public float sprintBreathInterval = 4f;
        [Range(0, 1)] public float hullAlertThreshold = .24f, powerAlertThreshold = .10f, oxygenAlertThreshold = .20f;

        bool tracking, wasNight, hullAlert, powerAlert;
        readonly bool[] oxygenAlert = new bool[2];
        readonly float[] stepPitch = new float[2];
        readonly bool[] wasWalking = new bool[2];
        int lastDay, chapter, helmDirection;
        bool musicSelected;
        BgmContext? currentBgm;
        int? currentNightSeed;
        MainMenuController menu;
        float nextBirdCall, nextThunder;
        float lastSprintBreath = float.NegativeInfinity;
        SessionPhase phase;
        TutorialProgress tutorial;
        int tutorialStep;
        bool tutorialNotice;

        void Start()
        {
            if (session == null)
            {
                menu = FindAnyObjectByType<MainMenuController>();
                return;
            }
            session.SessionReady += VoyageReady;
            if (session.captain != null) session.captain.PresentationEvent += OnCrewSound;
            if (session.diver != null) session.diver.PresentationEvent += OnCrewSound;
            if (saveManager != null)
            {
                saveManager.SaveCompleted += OnSaved;
                saveManager.SaveFailed += OnSaveFailed;
            }
        }

        void UnsubscribeVoyage()
        {
            if (session != null)
            {
                session.SessionReady -= VoyageReady;
                if (session.captain != null) session.captain.PresentationEvent -= OnCrewSound;
                if (session.diver != null) session.diver.PresentationEvent -= OnCrewSound;
            }
            if (saveManager != null)
            {
                saveManager.SaveCompleted -= OnSaved;
                saveManager.SaveFailed -= OnSaveFailed;
            }
        }

        void OnSaved(SaveSummary save) => Local(save.reason == SaveReason.Manual ? AudioCue.SaveManual : AudioCue.SaveAuto);
        void OnSaveFailed(string _) => Local(AudioCue.SaveFail);
        void VoyageReady(bool _)
        {
            tracking = false;
            tutorial = null;
            lastSprintBreath = float.NegativeInfinity;
        }

        void LateUpdate()
        {
            if (menu != null)
                SelectMusic(menu.onlineRoomPanel != null && menu.onlineRoomPanel.IsOpen ? BgmContext.OnlineRoom : BgmContext.Menu);
            else if (session != null && session.IsInitialized) UpdateVoyage();
            UpdateLoops();
        }

        void UpdateVoyage()
        {
            var game = session;
            bool live = game.phase == SessionPhase.Playing;
            bool night = game.IsNight;
            bool storm = game.StormActive;
            bool localOnline = OnlineVoyage.InProgress;
            var focus = localOnline && OnlineVoyage.Current.LocalRole == CrewRole.Diver ? game.diver : game.captain;
            bool underwater = focus != null && !focus.onboard;
            if (game.phase == SessionPhase.Won || game.phase == SessionPhase.Lost) SelectMusic(null);
            else if (night) SelectMusic(BgmContext.Night, unchecked(game.worldSeed * 397 ^ game.day * 104729 ^ 0x56f23ac9));
            else if (game.IsDusk) SelectMusic(BgmContext.Dusk);
            else
            {
                float x = game.ship != null ? game.ship.Position.x : 0;
                SelectMusic(x < game.tuning.shallowBoundary ? BgmContext.ShallowsDay :
                    x < game.tuning.deepBoundary ? BgmContext.CityDay : BgmContext.DeepDay);
            }

            if (tutorial == null) tutorial = game.GetComponent<TutorialProgress>();
            UpdateAlerts(night, live);
            if (!live) return;
            if (localOnline)
                AmbientFor(focus, 1);
            else if (game.captain != null && game.diver != null && game.captain.onboard != game.diver.onboard)
            {
                AmbientFor(game.captain, .66f);
                AmbientFor(game.diver, .66f);
            }
            else AmbientFor(focus, 1);
            if (storm) Keep(AudioCue.AmbRainLoop, 0, ambienceLevel * (underwater ? .25f : .78f));
            if (night) Keep(AudioCue.AmbNightWindLoop, 0, ambienceLevel * (underwater ? .12f : .62f));
            float now = Time.unscaledTime;
            if (storm && now >= nextThunder)
            {
                Local(AudioCue.ThunderFar, underwater ? .22f : .48f);
                nextThunder = now + 10 + (float)variation.NextDouble() * 6;
            }
            if (!storm) nextThunder = now + 7;
            if (!night && !underwater && now >= nextBirdCall)
            {
                Local(AudioCue.AmbDistantBirds, ambienceLevel * .45f);
                nextBirdCall = now + 16 + (float)variation.NextDouble() * 9;
            }

            CrewMovement(game.captain);
            CrewMovement(game.diver);
            DeviceLoops();
            EnemyLoops();
        }

        void SelectMusic(BgmContext? context, int? nightSeed = null)
        {
            var manager = BgmManager.Current != null ? BgmManager.Current : bgm;
            if (manager == null || (musicSelected && currentBgm == context && currentNightSeed == nightSeed)) return;
            musicSelected = true;
            currentBgm = context;
            currentNightSeed = nightSeed;
            if (context.HasValue) manager.Play(context.Value, nightSeed);
            else manager.Stop();
        }

        void UpdateAlerts(bool night, bool live)
        {
            var game = session;
            bool lowHull = game.hull <= game.hullMax * hullAlertThreshold;
            bool lowPower = game.generatorInstalled && !game.infinitePower && game.power <= game.tuning.maxPower * powerAlertThreshold;
            if (tracking)
            {
                if (phase != game.phase && game.phase == SessionPhase.Won) Local(AudioCue.EndWin);
                if (phase != game.phase && game.phase == SessionPhase.Lost) Local(AudioCue.EndFail);
                if (live)
                {
                    if (!wasNight && night) Local(AudioCue.NightFall);
                    if (lastDay != game.day) Local(AudioCue.DayRefresh);
                    if (chapter != game.QuestChapter) Local(AudioCue.QuestStage, .65f);
                    if (!hullAlert && lowHull) Local(AudioCue.AlertHullLow);
                    if (!powerAlert && lowPower && game.power > 0) Local(AudioCue.AlertPowerLow);
                }
            }
            OxygenAlert(game.captain, live);
            OxygenAlert(game.diver, live);
            if (tutorial != null)
            {
                if (tracking && live && tutorial.Step > tutorialStep)
                    Local(tutorial.Complete ? AudioCue.TutorialComplete : AudioCue.TutorialStep, .68f);
                bool notice = !string.IsNullOrEmpty(tutorial.NoticeSource);
                if (tracking && live && !tutorialNotice && notice) Local(AudioCue.TutorialNotice, .7f);
                tutorialStep = tutorial.Step;
                tutorialNotice = notice;
            }
            tracking = true;
            wasNight = night;
            lastDay = game.day;
            chapter = game.QuestChapter;
            hullAlert = lowHull;
            powerAlert = lowPower;
            phase = game.phase;
        }

        void OxygenAlert(CrewActor actor, bool live)
        {
            if (actor == null || (OnlineVoyage.InProgress && OnlineVoyage.Current.LocalRole != actor.role)) return;
            int index = (int)actor.role;
            bool low = !actor.onboard && actor.oxygen <= actor.oxygenMax * oxygenAlertThreshold;
            if (tracking && live && low && !oxygenAlert[index] && !session.IsIncapacitated(actor))
                Local(AudioCue.AlertOxygenLow, 1, .25f);
            oxygenAlert[index] = low;
        }

        void AmbientFor(CrewActor actor, float gain)
        {
            if (actor == null) return;
            AudioCue cue = actor.onboard ? AudioCue.AmbOceanShipLoop : InCave(actor) ? AudioCue.AmbCaveLoop : AudioCue.AmbUnderwaterLoop;
            Keep(cue, 0, gain * ambienceLevel);
        }

        bool InCave(CrewActor actor)
        {
            if (session.world == null) return false;
            var point = actor.Position;
            foreach (var layout in session.world.layouts)
                if (layout.entranceOnLeft && point.x >= layout.topLeft.x && point.x < layout.topLeft.x + layout.columns * layout.cellSize &&
                    point.y <= layout.topLeft.y && point.y > layout.topLeft.y - layout.rows * layout.cellSize) return true;
            return false;
        }

        void CrewMovement(CrewActor actor)
        {
            if (actor == null || session.IsIncapacitated(actor) || session.IsHidden(actor)) return;
            int index = (int)actor.role;
            var velocity = OnlineVoyage.IsGuest && actor.body != null && actor.body.isKinematic ? ReplicaVelocity(actor) :
                actor.body != null ? (Vector2)actor.body.linearVelocity : Vector2.zero;
            float shipSpeed = session.ship != null ? session.ship.speed : 0;
            bool walking = actor.onboard && (actor.Grounded || OnlineVoyage.IsGuest && Mathf.Abs(velocity.y) < .3f) &&
                !actor.IsClimbing && Mathf.Abs(velocity.x - shipSpeed) > .35f;
            if (walking && !wasWalking[index]) stepPitch[index] = .96f + (float)variation.NextDouble() * .08f;
            if (walking) KeepAt(index == 0 ? AudioCue.CrewStepDeck : AudioCue.CrewStepDeck02, index, actor.Position, true, .86f, stepPitch[index]);
            wasWalking[index] = walking;
            if (actor.onboard && actor.IsClimbing) KeepAt(AudioCue.CrewLadderLoop, index, actor.Position, true, .9f);
            if (!actor.onboard && velocity.sqrMagnitude > .28f)
            {
                KeepAt(AudioCue.CrewSwimLoop, index, actor.Position, false, .7f);
                PlaySprintBreath(actor);
            }
            if (session.reviveProgress.Length > index && session.reviveProgress[index] > 0)
                KeepAt(AudioCue.CrewReviveLoop, index, actor.Position, actor.onboard, .72f);
        }

        void PlaySprintBreath(CrewActor actor)
        {
            if (OnlineVoyage.InProgress && OnlineVoyage.Current.LocalRole != actor.role) return;
            bool sprinting = OnlineVoyage.IsGuest
                ? !OnlineVoyage.Current.LocalMenuOpen && actor.input != null && actor.input.IsActionHeld("Sprint")
                : actor.role == CrewRole.Captain ? session.captainSprinting : session.diverSprinting;
            if (!sprinting) return;
            var clip = library != null ? library.Find(AudioCue.CrewSprintBreath)?.clip : null;
            if (clip == null) return;
            float now = Time.unscaledTime;
            if (now - lastSprintBreath < SprintBreathSpacing(actor, clip.length)) return;
            float gain = Audible(actor.Position, false, out float pan);
            PlayEffect(AudioCue.CrewSprintBreath, gain * .7f, pan);
            lastSprintBreath = now;
        }

        float SprintBreathSpacing(CrewActor actor, float clipLength)
        {
            float oxygen = actor.oxygenMax > 0 ? Mathf.Clamp01(actor.oxygen / actor.oxygenMax) : 1f;
            return Mathf.Lerp(clipLength, Mathf.Max(clipLength, sprintBreathInterval), oxygen);
        }

        readonly Vector2[] previousPosition = new Vector2[2];
        readonly bool[] positionKnown = new bool[2];
        Vector2 ReplicaVelocity(CrewActor actor)
        {
            int index = (int)actor.role;
            Vector2 point = actor.Position;
            Vector2 movement = point - previousPosition[index];
            previousPosition[index] = point;
            bool known = positionKnown[index];
            positionKnown[index] = true;
            return known && movement.sqrMagnitude < 1 ? movement / Mathf.Max(.005f, Time.unscaledDeltaTime) : Vector2.zero;
        }

        void DeviceLoops()
        {
            var game = session;
            if (game.ship == null) return;
            var ship = game.ship.Position;
            bool winching = (game.winchActive || game.winchElectric) && (game.WinchTargetCount > 0 || game.rescueVoyage);
            if (winching)
            {
                KeepAt(AudioCue.WinchLoop, 0, game.ShipPoint(new Vector3(1.8f, 1, 0)), true, .9f);
                if (game.captain != null && !game.captain.onboard) KeepAt(AudioCue.CrewWinchedLoop, 0, game.captain.Position, false, .6f);
                if (game.diver != null && !game.diver.onboard) KeepAt(AudioCue.CrewWinchedLoop, 1, game.diver.Position, false, .6f);
                if (game.world != null)
                    foreach (var cargo in game.world.pickups)
                        if (cargo != null && cargo.attached) { KeepAt(AudioCue.CargoStrainLoop, 0, cargo.transform.position, false, .63f); break; }
            }
            if (game.LightEffective) KeepAt(AudioCue.LightHumLoop, 0, game.ShipPoint(new Vector3(7.8f, 1, 0)), true, .55f);
            int direction = Mathf.Abs(game.ship.speed) > reelSoundSpeed ? (game.ship.speed > 0 ? 1 : -1) : 0;
            if (game.ship.helmOperator != null && direction != 0)
            {
                KeepAt(AudioCue.WheelLoop, 0, game.ship.helmOperator.Position, true, .7f);
                if (helmDirection != 0 && helmDirection != direction) PlayNotice(new OnlineAudioCue {
                    cue = AudioCue.WheelReverse, point = game.ShipToWaterPoint(game.ship.helmOperator.Position), space = Surface });
                helmDirection = direction;
            }
            else if (game.ship.helmOperator == null) helmDirection = 0;
            if (game.hull < game.hullMax * .4f) KeepAt(AudioCue.HullBreachLoop, 0, ship, true, .65f);
            if (game.hull < game.hullMax * .2f) KeepAt(AudioCue.FloodingLoop, 0, ship, true, .58f);
            if (game.rescueVoyage) KeepAt(AudioCue.BeaconLoop, 0, game.WaterToShipPoint(new Vector3(game.rescueArrivalX, game.BoatAnchor.y, 0)), true, .8f);
            if (hud != null)
            {
                PanelLoops(hud.captainPanel, game.captain);
                PanelLoops(hud.diverPanel, game.diver);
            }
        }

        void PanelLoops(CrewStationPanel panel, CrewActor actor)
        {
            if (panel == null || !panel.IsOpen || actor == null ||
                (OnlineVoyage.InProgress && OnlineVoyage.Current.LocalRole != actor.role)) return;
            int index = (int)actor.role;
            if (panel.FishingAudioReeling) Keep(AudioCue.FishingReelLoop, index, .78f);
            if (panel.ScrubbingAudioActive) Keep(AudioCue.OilScrubLoop, index, .75f);
            if (panel.station == StationKind.TotalControl && session.radioStage > 0)
                Keep(AudioCue.RadioStaticLoop, index, .34f);
        }

        void EnemyLoops()
        {
            LifelineEnemy closestPiranha = null;
            float piranhaGain = 0;
            foreach (var enemy in session.enemies)
            {
                if (enemy == null || !enemy.IsAlive) continue;
                int id = enemy.GetEntityId().GetHashCode();
                if (enemy.IsAirborne)
                {
                    AudioCue flap = Mathf.Abs(id % 3) == 0 ? AudioCue.EnemyBirdFlapLoop :
                        Mathf.Abs(id % 3) == 1 ? AudioCue.EnemyBirdFlapLoop02 : AudioCue.EnemyBirdFlapLoop03;
                    KeepAt(flap, id, enemy.transform.position, true, .62f);
                }
                else if (enemy.kind == EnemyKind.Shadow) KeepAt(AudioCue.EnemyShadowPressureLoop, id, enemy.transform.position, false, .92f);
                else if (enemy.kind == EnemyKind.Piranha)
                {
                    float gain = Audible(enemy.transform.position, false, out _);
                    if (gain > piranhaGain) { piranhaGain = gain; closestPiranha = enemy; }
                }
            }
            if (closestPiranha != null) KeepAt(AudioCue.EnemyPiranhaSwarmLoop, 0, closestPiranha.transform.position, false, .76f);
        }

        void OnCrewSound(CrewActor actor, CrewVisualEvent kind, float strength)
        {
            if (session.phase != SessionPhase.Playing) return;
            AudioCue cue = kind switch
            {
                CrewVisualEvent.Dive => AudioCue.CrewDiveSplash,
                CrewVisualEvent.Board => AudioCue.CrewBoard,
                CrewVisualEvent.Melee => AudioCue.CrewMeleeSwing,
                CrewVisualEvent.Fire => AudioCue.SpearFire,
                CrewVisualEvent.Hit => AudioCue.CrewHurt,
                CrewVisualEvent.Downed => AudioCue.CrewDowned,
                CrewVisualEvent.Revived => actor.onboard ? AudioCue.CrewRevived : AudioCue.CrewReviveWater,
                _ => AudioCue.None
            };
            if (cue != AudioCue.None) At(actor, cue);
        }
    }
}
