using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class SurvivalBalanceTests
    {
        GameObject root;GameSession game,previous;GameTuning tuning;Random.State random;float scale;
        [SetUp] public void SetUp()
        {
            previous=GameSession.Current;random=Random.state;scale=Time.timeScale;
            root=new GameObject("Survival balance fixture");game=root.AddComponent<GameSession>();game.autoStart=false;game.enabled=false;
            tuning=ScriptableObject.CreateInstance<GameTuning>();tuning.maximumSharks=0;game.tuning=tuning;
            var ship=Child("Ship");game.ship=ship.AddComponent<ShipMotor>();game.ship.enabled=false;
            game.captain=Actor();game.diver=Actor();game.Initialize();
        }
        GameObject Child(string name){var o=new GameObject(name);o.transform.SetParent(root.transform);return o;}
        CrewActor Actor(){var o=Child("Crew");o.layer=9;var a=o.AddComponent<CrewActor>();a.enabled=false;return a;}
        [TearDown] public void TearDown()
        {
            game.ClearAreaStrikes();foreach(var enemy in game.enemies.ToArray())if(enemy!=null)Object.DestroyImmediate(enemy.gameObject);
            Object.DestroyImmediate(root);Object.DestroyImmediate(tuning);GameSession.RestoreCurrentAfterValidation(previous);Time.timeScale=scale;Random.state=random;
        }
        [Test] public void DailyLightningAndBirdScalingRetainsBaseDaysAndFloors()
        {
            game.day=2;Assert.That(game.LightningInterval,Is.EqualTo(new Vector2(12,18)));Assert.That(game.LightningWarningSeconds,Is.EqualTo(2));
            game.day=3;Assert.That(game.LightningInterval,Is.EqualTo(new Vector2(11.5f,17.5f)));Assert.That(game.LightningWarningSeconds,Is.EqualTo(1.95f).Within(.001f));
            Assert.That(game.BirdInterval,Is.EqualTo(15));game.day=4;Assert.That(game.BirdInterval,Is.EqualTo(14));
            game.day=100;Assert.That(game.LightningInterval,Is.EqualTo(new Vector2(6,10)));Assert.That(game.LightningWarningSeconds,Is.EqualTo(1.2f));Assert.That(game.BirdInterval,Is.EqualTo(10));Assert.That(game.BirdExtraChances,Is.EqualTo(new Vector2(.5f,.25f)));
            game.day=1;Assert.That(game.BirdWaveCount(.05f),Is.EqualTo(3));Assert.That(game.BirdWaveCount(.15f),Is.EqualTo(2));Assert.That(game.BirdWaveCount(.8f),Is.EqualTo(1));
            game.SpawnBirdWave(.05f);Assert.That(game.enemies.Select(e=>e.transform.position).Distinct().Count(),Is.EqualTo(3));
        }

        [TestCase(1, .25f, .5f)]
        [TestCase(2, .26f, .51f)]
        [TestCase(16, .4f, .65f)]
        [TestCase(21, .4f, .7f)]
        [TestCase(100, .4f, .7f)]
        public void BirdInterruptChancesIncreaseFromDayTwoAndCapIndependently(int day, float regular, float armored)
        {
            game.day = day;
            Assert.That(game.GetBirdInterruptChance(EnemyKind.Bird), Is.EqualTo(regular).Within(.0001f));
            Assert.That(game.GetBirdInterruptChance(EnemyKind.ArmoredBird), Is.EqualTo(armored).Within(.0001f));
        }

        [TestCase(.2f, .6f, .6f)]
        [TestCase(.7f, .8f, .8f)]
        [TestCase(.9f, 1f, .6f)]
        public void BirdsSpawnInEachPhaseAndSearchlightSlowsOnlyNight(float dayFraction, float rate, float litRate)
        {
            game.SetDayClock(1, dayFraction);
            game.birdCountdown = 2 * rate;
            game.StepSimulation(1);
            Assert.That(game.birdCountdown, Is.EqualTo(rate).Within(.001f));
            Assert.That(game.enemies, Is.Empty);
            game.StepSimulation(1.01f);
            Assert.That(game.enemies.Count, Is.InRange(1, 3));

            game.circuitRepaired = true;
            game.searchlightOn = true;
            game.upgradeLevels[(int)UpgradeKind.Searchlight] = 2;
            game.power = 50;
            float before = game.birdCountdown;
            game.StepSimulation(1);
            Assert.That(game.birdCountdown, Is.EqualTo(before - litRate).Within(.001f));
        }

        [Test] public void RouteConsumesOnePercentPerMetreAndFuelUpgradePreservesCrankAndEelMeaning()
        {
            Assert.That(tuning.routeDistance,Is.EqualTo(200));Assert.That(game.ship.eastLimit,Is.EqualTo(200));
            Assert.That(tuning.shallowBoundary/200,Is.EqualTo(24f/90).Within(.0001f));Assert.That(tuning.deepBoundary/200,Is.EqualTo(56f/90).Within(.0001f));
            Assert.That(game.hull,Is.EqualTo(70));Assert.That(game.power,Is.Zero);Assert.That(game.generatorInstalled,Is.False);
            game.power=100;Assert.That(game.TryConsumeSailing(1),Is.False,"repairing the engine is a prerequisite even when electricity is available");
            game.engineRepaired=true;game.generatorInstalled=true;
            game.power=100;Assert.That(game.TryConsumeSailing(100));Assert.That(game.power,Is.Zero);Assert.That(game.TryConsumeSailing(1),Is.False);
            game.AddResource(ResourceKind.Plate,4);game.AddResource(ResourceKind.Fuel,2);Assert.That(game.UpgradeGenerator());Assert.That(game.BurnFuel());Assert.That(game.power,Is.EqualTo(15));
            Assert.That(game.UpgradeGenerator());Assert.That(game.GetResource(ResourceKind.Plate),Is.Zero);Assert.That(game.BurnFuel());Assert.That(game.power,Is.EqualTo(35));
            Assert.That(game.CrankGenerator(true));Assert.That(game.power,Is.EqualTo(36));var save=game.CaptureSaveData();Assert.That(game.TryRestoreSaveData(save,out string error),Is.True,error);Assert.That(game.efficientFuelGenerator);Assert.That(game.generatorTier,Is.EqualTo(1));
            game.StartGame();game.AddResource(ResourceKind.Eel,1);Assert.That(game.InstallEel());Assert.That(game.generatorTier,Is.EqualTo(2));Assert.That(game.infinitePower);
        }
        [Test] public void BackpackTiersAreCumulativeAndEveryTierCostsTwoBundles()
        {
            game.AddResource(ResourceKind.FiberBundle,6);int[] slots={10,14,18};float[] speed={1.25f,1.25f,1.5f};
            for(int i=0;i<3;i++){Assert.That(game.TryUpgrade(UpgradeKind.Backpack));Assert.That(game.diver.BagCapacity,Is.EqualTo(slots[i]));Assert.That(game.GetCollectSpeedMultiplier(game.diver),Is.EqualTo(speed[i]));}
            Assert.That(game.GetResource(ResourceKind.FiberBundle),Is.Zero);Assert.That(game.captain.BagCapacity,Is.EqualTo(3));Assert.That(game.GetCollectSpeedMultiplier(game.captain),Is.EqualTo(1));
        }
        [Test] public void HitUnitsAndSuitTiersStayIntegerAndCaptainIsAlwaysBaseCapacity()
        {
            Assert.That(game.GetMaxDiverBites(),Is.EqualTo(2));game.upgradeLevels[(int)UpgradeKind.DivingSuit]=1;Assert.That(game.GetMaxDiverBites(),Is.EqualTo(3));
            game.upgradeLevels[(int)UpgradeKind.DivingSuit]=2;Assert.That(game.GetMaxDiverBites(),Is.EqualTo(4));tuning.upgradedSuitImmunity=1;
            Assert.That(game.ReceiveDiverHit("test",false,1));Assert.That(game.diverBites,Is.EqualTo(1));game.diverInvulnerable=0;
            game.upgradeLevels[(int)UpgradeKind.DivingSuit]=3;Assert.That(game.ReceiveDiverHit("test",false,2),Is.False);Assert.That(game.diverBites,Is.EqualTo(1));
            game.captain.ReceiveDamage(1);Assert.That(game.captain.downed,Is.False);Assert.That(game.captain.health,Is.EqualTo(50));game.captain.ReceiveDamage(1);Assert.That(game.captain.downed);
            var save=game.CaptureSaveData();Assert.That(GameSaveValidation.Validate(save,out string error),Is.True,error);
        }
        [Test] public void LostCargoHasHintsSurvivesDailyRegenerationAndRoundTripsWithExactContents()
        {
            var world=Child("World").AddComponent<WorldGenerator>();game.world=world;world.session=game;world.tuning=tuning;world.geometryLayer=20;world.pickupLayer=21;world.areaCenters=new[]{0f};world.generateCaves=false;world.Generate(8417);
            game.BeginDive();game.diver.RestorePose(false,new Vector3(0,-7,0),Vector3.zero,Vector2.right);game.diver.backpack.AddRange(new[]{ResourceKind.Wood,ResourceKind.Fuel,ResourceKind.Fiber,ResourceKind.Metal});game.FaintDiver("test");
            var bag=world.pickups.Single(p=>p.isLostCargo);Assert.That(bag.label,Is.Not.Null);Assert.That(bag.LostCargoPrompt(),Does.Contain("2/3"));Assert.That(bag.persistAcrossDays);var contents=bag.contents.ToArray();
            game.day=2;game.elapsed=tuning.daySeconds;world.RefreshDay(2);Assert.That(world.pickups,Does.Contain(bag));Assert.That(bag.contents,Is.EqualTo(contents));
            var save=game.CaptureSaveData();Assert.That(game.TryRestoreSaveData(save,out string error),Is.True,error);bag=world.pickups.Single(p=>p.isLostCargo);Assert.That(bag.contents,Is.EqualTo(contents));Assert.That(bag.persistAcrossDays);Assert.That(bag.label,Is.Not.Null);
            world.RefreshDay(3);Assert.That(world.pickups,Does.Contain(bag));
        }
        [Test] public void QteCannotReachAJobAtAnotherStationAndBirdInterruptKeepsAutomaticWork()
        {
            var recipe=ScriptableObject.CreateInstance<RecipeDefinition>();
            try
            {
                recipe.id="fixture";recipe.cost=new ResourceAmount[0];game.recipes=new[]{recipe};Assert.That(game.TryCraft(recipe.id,game.captain));var job=game.jobs.Single();job.elapsed=job.nextCue;
                Assert.That(game.craftingManager.CraftQteAtStation(job.id,job.noteDirection,StationKind.HullRepair),Is.False);Assert.That(job.attempted,Is.False);
                game.captain.controlsCaptured=true;game.InterruptOperation(game.captain);Assert.That(game.captain.controlsCaptured,Is.False);Assert.That(game.jobs.Single(),Is.SameAs(job));
                Assert.That(game.craftingManager.CraftQteAtStation(job.id,job.noteDirection,StationKind.Workbench));
            }finally{Object.DestroyImmediate(recipe);}
        }
        [Test] public void MigrationUpdatesOnlyOldDefaultsAndLegacySaveKeepsProgressWithoutChangingOriginal()
        {
            tuning.boatSpeed=3.4f;tuning.routeDistance=90;tuning.baseOxygen=40;tuning.fuelPower=17;
            Lifeline.Editor.SurvivalBalanceMigration.UpdateTuning(tuning);Assert.That(tuning.boatSpeed,Is.EqualTo(3));Assert.That(tuning.routeDistance,Is.EqualTo(200));Assert.That(tuning.baseOxygen,Is.EqualTo(50));Assert.That(tuning.fuelPower,Is.EqualTo(17));
            game.SpawnEnemy(EnemyKind.Bird,new Vector3(50,6,0));var data=game.CaptureSaveData();data.balanceVersion=0;data.routeDistance=90;data.shipPosition.x=45;data.state.diverBites=.5f;
            Assert.That(game.TryRestoreSaveData(data,out string error),Is.True,error);Assert.That(game.ship.Position.x,Is.EqualTo(100).Within(.0001f));Assert.That(game.diverBites,Is.EqualTo(1));Assert.That(game.enemies.Single().transform.position.x,Is.EqualTo(105).Within(.0001f));Assert.That(data.shipPosition.x,Is.EqualTo(45));Assert.That(data.state.diverBites,Is.EqualTo(.5f));
        }

        [Test] public void BatchChargesFullCostAndKeepsSeparateRepairSlotAcrossSave()
        {
            var material = ScriptableObject.CreateInstance<RecipeDefinition>();
            var repair = ScriptableObject.CreateInstance<RecipeDefinition>();
            try
            {
                material.id = "fixture-material"; material.allowBatch = true;
                material.cost = new[] { new ResourceAmount(ResourceKind.Wood, 2) };
                material.output = new[] { new ResourceAmount(ResourceKind.Plank, 1) };
                repair.id = "fixture-repair"; repair.repairAmount = 10; repair.craftTimeMultiplier = .5f;
                game.recipes = new[] { material, repair };
                game.repairBenchInstalled = true; game.hull = 40;
                game.AddResource(ResourceKind.Wood, 8 - game.GetResource(ResourceKind.Wood));
                var manager = game.GetOrCreateCraftingManager();
                Assert.That(manager.TryCraft(material.id, game.captain, 2), Is.False);
                game.upgradeLevels[(int)UpgradeKind.Workbench] = 2;
                Assert.That(manager.TryCraft(material.id, game.captain, 5), Is.False);
                Assert.That(game.GetResource(ResourceKind.Wood), Is.EqualTo(8));
                Assert.That(manager.TryCraft(material.id, game.captain, 4), Is.True);
                Assert.That(manager.TryCraft(repair.id, game.diver), Is.True);
                Assert.That(manager.TryCraft(repair.id, game.diver), Is.False);
                Assert.That(manager.SlotCount(StationKind.Workbench), Is.EqualTo(1));
                Assert.That(manager.SlotCount(StationKind.HullRepair), Is.EqualTo(1));
                var save = game.CaptureSaveData();
                Assert.That(game.TryRestoreSaveData(save, out string error), Is.True, error);
                Assert.That(game.jobs.Find(job => job.recipe == material).quantity, Is.EqualTo(4));
                manager.Tick(manager.GetCraftDuration(material) * 4);
                Assert.That(game.GetResource(ResourceKind.Plank), Is.EqualTo(4));
            }
            finally { Object.DestroyImmediate(material); Object.DestroyImmediate(repair); }
        }
    }
}
