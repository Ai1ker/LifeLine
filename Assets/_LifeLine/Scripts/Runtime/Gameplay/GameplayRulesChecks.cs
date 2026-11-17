using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Editor-invocable integration checks, with real GameSession/CrewActor/ShipMotor components.</summary>
    public static class GameplayRulesChecks
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run rules checks outside Play mode; use Play mode for input/physics validation.");
            GameSession previousCurrent = GameSession.Current;
            UnityEngine.Random.State previousRandomState = UnityEngine.Random.state;
            GameObject fixture = new GameObject("LifeLine rules checks (temporary)");
            GameTuning tuning = ScriptableObject.CreateInstance<GameTuning>();
            RecipeDefinition recipe = ScriptableObject.CreateInstance<RecipeDefinition>();
            var log = new List<string>();
            GameSession game = null;
            try
            {
                fixture.transform.position = new Vector3(1000, 0, 0);
                game = fixture.AddComponent<GameSession>(); game.autoStart = false; game.tuning = tuning; game.phase = SessionPhase.Playing;
                GameObject boat = new GameObject("Ship"); boat.transform.SetParent(fixture.transform, false);
                boat.AddComponent<Rigidbody>(); game.ship = boat.AddComponent<ShipMotor>(); game.ship.Initialize(game);
                game.captain = Actor(fixture.transform, game, CrewRole.Captain, new Vector3(1000, 1, 0));
                game.diver = Actor(fixture.transform, game, CrewRole.Diver, new Vector3(1004, 1, 0));
                StationAnchor winch = boat.AddComponent<StationAnchor>(); winch.kind = StationKind.Winch; winch.interactionRadius = 3;
                game.hullMax = tuning.baseHull; game.hull = game.hullMax; game.power = 100;
                game.ropeLength = tuning.ropeLengths[0]; game.stormCountdown = 100;
                foreach (ResourceKind kind in Enum.GetValues(typeof(ResourceKind))) game.bank.Add(new ResourceAmount(kind, 0));
                RecipeDefinition[] legacyRead = game.recipes;
                List<CraftJob> legacyQueueRead = game.jobs;
                Check(legacyRead.Length == 0 && legacyQueueRead.Count == 0 && game.GetComponent<CraftingManager>() == null, "compatibility recipe and queue reads never mutate an old scene", log);
                CraftingManager crafting = game.GetOrCreateCraftingManager();
                Check(crafting != null && crafting.session == game && game.GetOrCreateCraftingManager() == crafting && game.GetComponents<CraftingManager>().Length == 1, "missing manager fallback creates and reuses one crafting authority", log);

                game.AddResource(ResourceKind.Wood, 3);
                bool spent = game.Spend(new[] { new ResourceAmount(ResourceKind.Wood, 2), new ResourceAmount(ResourceKind.Wood, 2) });
                Check(!spent && game.GetResource(ResourceKind.Wood) == 3, "atomic aggregated ingredient spending", log);
                Check(game.TryUpgrade(UpgradeKind.Workbench) == false && game.GetUpgrade(UpgradeKind.Workbench) == 0, "unaffordable upgrade preserves level", log);
                game.AddResource(ResourceKind.Plank, 6); game.AddResource(ResourceKind.Plate, 3);
                float[] durations = { 40, 30, 20, 15 };
                for (int i = 0; i < 3; i++) { Check(game.TryUpgrade(UpgradeKind.Workbench), "workbench T" + (i + 1), log); Check(Mathf.Approximately(game.GetCraftDuration(), durations[i + 1]), "workbench duration " + durations[i + 1], log); }
                Check(game.CraftCapacity == 2 && Mathf.Approximately(game.GetCraftQteWindow(), .208f), "T3 parallel slots and T1 wider judgment", log);
                Check(game.GetUpgradeCost(UpgradeKind.Workbench).Length == 0 && game.GetResource(ResourceKind.Plank) == 0, "every bench tier costs two planks and max tier has empty cost", log);
                game.AddResource(ResourceKind.FiberBundle, 6);
                Check(game.TryUpgrade(UpgradeKind.Backpack) && game.diver.BagCapacity == 10, "backpack T1 adds two slots", log);
                Check(game.TryUpgrade(UpgradeKind.Backpack) && game.diver.BagCapacity == 14, "backpack T2 adds four more slots", log);
                Check(game.TryUpgrade(UpgradeKind.Backpack) && game.diver.BagCapacity == 18 && Mathf.Approximately(game.GetCollectSpeedMultiplier(game.diver), 1.5f), "backpack T3 gives cumulative 50 percent collection speed", log);
                Check(Mathf.Approximately(game.GetOxygenMax(), 50) && Mathf.Approximately(tuning.oxygenLevels[3], 150) && Mathf.Approximately(tuning.ropeLengths[3], 74) && Mathf.Approximately(tuning.ropeLengths[4], 100), "rebalanced oxygen, 74m T3 and 100m T4 rope", log);
                game.upgradeLevels[(int)UpgradeKind.Workbench] = 0;
                recipe.id = "test_plank"; recipe.displayName = "Test plank";
                recipe.cost = new[] { new ResourceAmount(ResourceKind.Wood, 1) };
                recipe.output = new[] { new ResourceAmount(ResourceKind.Plank, 1) }; game.recipes = new[] { recipe };
                Check(crafting.FindRecipe(recipe.id) == recipe && ReferenceEquals(game.recipes, crafting.recipeDefinitions) && ReferenceEquals(game.jobs, crafting.jobs), "session compatibility views reference the manager's SO catalog and single live queue", log);
                int oldPlank = game.GetResource(ResourceKind.Plank);
                Check(crafting.TryCraft(recipe.id, game.captain), "manager starts automatic job from recipe SO", log);
                for (int i = 0; i < 4810; i++) game.StepSimulation(1f / 120);
                Check(game.jobs.Count == 0 && game.GetResource(ResourceKind.Plank) == oldPlank + 1, "unattended 40-second crafting at 120Hz", log);

                Check(game.TryCraft(recipe.id, game.captain), "start calibrated job", log);
                CraftJob job = game.jobs[0];
                for (int cue = 1; cue <= 10 && game.jobs.Count > 0; cue++)
                {
                    float untilCue = Mathf.Max(.001f, job.nextCue - job.elapsed);
                    game.StepSimulation(untilCue);
                    Check(game.CraftQte(job.id, job.noteDirection), "craft correct cue " + cue, log);
                    if (game.jobs.Count > 0) game.StepSimulation(game.GetCraftQteWindow() + .001f);
                }
                Check(game.jobs.Count == 0 && job.elapsed < 11, "perfect QTE finishes in about 10 seconds", log);
                Check(game.TryCraft(recipe.id, game.captain), "start cancellable job", log);
                int woodAfterStart = game.GetResource(ResourceKind.Wood);
                Check(game.CancelCraft(game.jobs[0].id) && game.GetResource(ResourceKind.Wood) == woodAfterStart, "cancel consumes materials without refund", log);

                game.generatorTier = 1; game.power = 10; game.AddResource(ResourceKind.Fuel, 1);
                Check(game.BurnFuel() && Mathf.Approximately(game.power, 25), "one fuel adds exactly 15 percent", log);
                game.lastCrankAt = -100;
                game.generatorTier = 1;
                Check(game.CrankGenerator(true) && !game.CrankGenerator(true) && Mathf.Approximately(game.power, 26), "crank cadence rejects duplicate strokes", log);
                game.StepSimulation(1);
                Check(Mathf.Approximately(game.power, 26), "generator has no idle power income", log);
                Check(!game.CrankGenerator(true), "fuel upgrade keeps crank but three-second cooldown remains", log);
                game.StepSimulation(2);
                Check(game.CrankGenerator(true) && Mathf.Approximately(game.power, 27), "crank remains usable after generator upgrade", log);
                game.engineRepaired = true;
                Check(game.DamageEngine("test") && !game.engineRepaired && game.engineRepairVersion == 1, "damage invalidates engine repair version", log);
                game.engineEverRepaired = true;
                Check(game.TryRepairDevice(StationKind.Engine), "repeat engine repair is free", log);
                for (int i = 0; i < game.engineFaultFixed.Length; i++) { game.RevealEngineFault(i); Check(game.TryRepairEngineFault(i, game.engineFaultTypes[i]), "matched engine fault " + i, log); }
                Check(game.engineRepaired, "all four faults complete engine repair", log);

                game.diver.SetOnboard(false, game.BoatAnchor);
                game.HandleCommand(game.diver, new CrewCommand { role = CrewRole.Diver, interact = true });
                Check(!game.diver.onboard, "swimming at anchor plus interact never self-boards", log);
                game.SetWinchActive(true); game.diver.SetOnboard(false, game.BoatAnchor + Vector3.down * 10);
                game.winchStartDistance = .5f;
                float before = Vector3.Distance(game.diver.Position, game.BoatAnchor);
                Check(game.TryWinchQte(tuning.winchCenter), "captain good winch cue accepted", log);
                float after = Vector3.Distance(game.diver.Position, game.BoatAnchor);
                Check(Mathf.Abs((before - after) - tuning.winchGoodDistance) < .01f, "opening winch early never teleports a later deep diver", log);
                game.winchAttemptedThisCycle = false;
                Check(game.TryWinchQte(0), "bad winch cue accepted for failure feedback", log);
                Check(Vector3.Distance(game.diver.Position, game.BoatAnchor) >= after, "failed winch cue cannot pull target closer", log);
                game.winchJam = 0; game.winchAttemptedThisCycle = false;
                game.diver.SetOnboard(false, game.BoatAnchor + Vector3.down * .5f);
                game.diver.backpack.Add(ResourceKind.Metal); int metalBefore = game.GetResource(ResourceKind.Metal);
                game.TryWinchQte(tuning.winchCenter);
                Check(game.diver.onboard && game.diver.backpack.Count == 0 && game.GetResource(ResourceKind.Metal) == metalBefore + 1, "only captain winch boards and banks exact backpack", log);

                game.diver.SetOnboard(false, game.BoatAnchor + Vector3.down * 10);
                game.diver.oxygen = 100; game.diverInvulnerable = 0;
                Check(game.RegisterSharkBite() && game.diverFainted, "basic suit exhausts at one two-unit shark bite", log);
                game.diverFainted = false; game.diverBites = 0; game.diverInvulnerable = 0;
                game.upgradeLevels[(int)UpgradeKind.DivingSuit] = 1;
                Check(game.RegisterSharkBite() && !game.diverFainted && game.diverBites == 2, "suit T1 survives one two-unit shark hit", log);
                game.diverInvulnerable = 0; game.upgradeLevels[(int)UpgradeKind.DivingSuit] = 3;
                tuning.upgradedSuitImmunity = 1;
                Check(!game.RegisterSharkBite() && game.diverBites == 2 && !game.diverFainted, "suit immunity prevents hit count and faint", log);
                tuning.upgradedSuitImmunity = .25f;
                game.upgradeLevels[(int)UpgradeKind.Rope] = 3; game.circuitRepaired = true;
                game.winchActive = game.winchElectric = false; game.signalCount = 0; game.lastSignalAt = -100;
                game.SignalCaptain(); game.SignalCaptain(); game.SignalCaptain();
                Check(game.winchElectric, "three consecutive signals trigger installed electric winch", log);
                game.winchActive = game.winchElectric = false;
                game.diver.SetOnboard(true, game.captain.Position + Vector3.right);
                game.diver.SetDowned(true);
                game.HandleCommand(game.captain, new CrewCommand { role = CrewRole.Captain, interactHeld = true });
                game.StepSimulation(4.9f);
                Check(game.diver.downed, "revive cannot complete before five seconds", log);
                game.HandleCommand(game.captain, new CrewCommand { role = CrewRole.Captain }); game.StepSimulation(.01f);
                Check(game.reviveProgress[0] == 0, "release interrupts revive and resets progress", log);
                game.HandleCommand(game.captain, new CrewCommand { role = CrewRole.Captain, interactHeld = true }); game.StepSimulation(5);
                Check(!game.diver.downed, "five continuous seconds revive teammate", log);

                game.radioStage = 2; game.elapsed = tuning.daySeconds * 2 + tuning.daySeconds * .9f;
                Check(!game.IsShadowSpawnAllowed, "shadow excluded before three complete days", log);
                game.elapsed = tuning.daySeconds * 3 + tuning.daySeconds * .9f;
                Check(game.IsShadowSpawnAllowed, "shadow requires chapter three deep water and late night", log);
                game.radioStage = 1;
                Check(!game.IsShadowSpawnAllowed, "shadow excluded before story chapter three", log);
                game.radioStage = 2; game.day = 4;
                game.upgradeLevels[(int)UpgradeKind.Sail] = 0;
                float normalDrift = game.GetStormDriftSpeed();
                game.upgradeLevels[(int)UpgradeKind.Sail] = 2;
                Check(Mathf.Abs(game.GetStormDriftSpeed() - normalDrift * .4f) < .0001f, "sail T2 reduces storm drift sixty percent", log);

                game.elapsed = tuning.daySeconds - .001f; game.StepSimulation(.01f);
                Check(game.day == 2, "day rollover uses authoritative elapsed seconds", log);
                game.circuitRepaired = game.engineRepaired = true; game.radioStage = 3;
                float beforeVoyageElapsed = game.elapsed;
                Check(game.StartRescueVoyage(), "complete radio and devices unlock voyage", log);
                Check(Mathf.Approximately(game.elapsed, beforeVoyageElapsed) && game.day == 2 && game.QuestChapter == 4, "voyage changes story chapter without skipping natural time or days", log);
                game.rescueArrivalX = 900; game.winchActive = true; game.rescueProgress = 0;
                game.StepSimulation(.1f);
                Check(game.rescueProgress == 0 && game.phase == SessionPhase.Playing && game.QuestText.Contains("等待第4日"), "early voyage waits for fourth-day channel even at rescue coordinates", log);
                game.elapsed = tuning.daySeconds * 3; game.rescueArrivalX = 900;
                game.winchActive = true; game.rescueProgress = 0; game.StepSimulation(game.rescueWinchSeconds + .01f);
                Check(game.phase == SessionPhase.Won && game.rescueProgress == 1, "day-four actual winch rescue reaches ending", log);

                // Preserve the world's actual grouped input order: four candidates per area.
                // A prefix-only selection passes a count check but silently omits the third area.
                GameObject worldRoot = new GameObject("Shark selection fixture"); worldRoot.transform.SetParent(fixture.transform, false);
                WorldGenerator spawnWorld = worldRoot.AddComponent<WorldGenerator>(); game.world = spawnWorld;
                spawnWorld.areaCenters = new[] { 0f, 80f, 160f };
                float surface = spawnWorld.WaterSurface;
                spawnWorld.sharkSpawnPoints.Add(new Vector3(0, surface - 12, 0)); // invalid shallow candidate first
                foreach (float center in spawnWorld.areaCenters)
                {
                    spawnWorld.sharkSpawnPoints.Add(new Vector3(center - 11.9f, surface - 29, 0));
                    spawnWorld.sharkSpawnPoints.Add(new Vector3(center - 14, surface - 39.5f, 0));
                    spawnWorld.sharkSpawnPoints.Add(new Vector3(center + 14, surface - 43, 0));
                    spawnWorld.sharkSpawnPoints.Add(new Vector3(center - 14, surface - 46.5f, 0));
                }
                tuning.maximumSharks = 6;
                List<Vector3> sharkPoints = game.SelectInitialSharkSpawnPoints();
                int[] perArea = new int[3]; int deepPoints = 0; bool allLegal = true, canBecomeWhiteShark = false;
                foreach (Vector3 point in sharkPoints)
                {
                    int area = point.x < 40 ? 0 : point.x < 120 ? 1 : 2; perArea[area]++;
                    if (surface - point.y >= 36) deepPoints++;
                    allLegal &= surface - point.y >= tuning.sharkMinimumDepth;
                    canBecomeWhiteShark |= point.x >= tuning.deepBoundary;
                }
                Check(sharkPoints.Count == 6 && perArea[0] == 2 && perArea[1] == 2 && perArea[2] == 2, "grouped shark candidates distribute six spawns evenly across all three areas", log);
                Check(allLegal && deepPoints > sharkPoints.Count - deepPoints, "shark selection filters shallow points and favors deep points", log);
                Check(canBecomeWhiteShark, "natural shark selection includes the deep sea needed for day-three white-shark mutation", log);
                int savedDay=game.day, boostedDeep=0, baselineDeep=0;float savedBonus=tuning.deepSharkSpawnBonus;bool retainedMiddle=false;
                for(int sampleDay=1;sampleDay<=256;sampleDay++)
                {
                    game.day=sampleDay;tuning.deepSharkSpawnBonus=0;
                    foreach(var point in game.SelectInitialSharkSpawnPoints())if(surface-point.y>=36)baselineDeep++;
                    tuning.deepSharkSpawnBonus=savedBonus;
                    foreach(var point in game.SelectInitialSharkSpawnPoints()){if(surface-point.y>=36)boostedDeep++;else retainedMiddle=true;}
                }
                game.day=savedDay;tuning.deepSharkSpawnBonus=savedBonus;
                Check(boostedDeep>baselineDeep,"deep shark bonus increases deep occupancy across daily seeds without raising the global cap",log);
                Check(retainedMiddle,"middle-band shark danger remains possible across daily seeds",log);
                tuning.maximumSharks = 2;
                Check(game.SelectInitialSharkSpawnPoints().Count == 2 && spawnWorld.sharkSpawnPoints.Count == 13, "shark sampling honors a smaller cap without consuming world candidate data", log);
                tuning.maximumSharks = 6;
                return "PASS " + log.Count + " gameplay checks\n" + string.Join("\n", log);
            }
            finally
            {
                if (game != null) foreach (LifelineEnemy enemy in game.enemies.ToArray()) if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy.gameObject);
                UnityEngine.Object.DestroyImmediate(fixture);
                UnityEngine.Object.DestroyImmediate(recipe); UnityEngine.Object.DestroyImmediate(tuning);
                GameSession.RestoreCurrentAfterValidation(previousCurrent);
                UnityEngine.Random.state = previousRandomState;
            }
        }

        static CrewActor Actor(Transform parent, GameSession game, CrewRole role, Vector3 position)
        {
            GameObject root = new GameObject(role.ToString()); root.transform.SetParent(parent, true); root.transform.position = position;
            root.AddComponent<Rigidbody>(); root.AddComponent<CapsuleCollider>();
            CrewActor actor = root.AddComponent<CrewActor>(); actor.enabled = false;
            actor.body = root.GetComponent<Rigidbody>(); actor.session = game; actor.role = role; actor.onboard = true;
            actor.oxygenMax = actor.oxygen = game.tuning.baseOxygen; return actor;
        }

        static void Check(bool condition, string name, List<string> log)
        {
            if (!condition) throw new InvalidOperationException("FAILED gameplay check: " + name);
            log.Add("✓ " + name);
        }
    }
}
