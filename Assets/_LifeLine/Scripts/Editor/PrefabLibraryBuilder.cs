#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lifeline.Editor
{
    /// <summary>Creates the replaceable asset library once. Existing assets are deliberately preserved.</summary>
    public static class PrefabLibraryBuilder
    {
        public sealed class Library
        {
            public GameTuning tuning;
            public InputActionAsset input;
            public RecipeDefinition[] recipes;
            public GameObject captain, diver, ship, loot, crate, trapdoor, shark, bird, shadow, spear, piranha;
            public Material[] worldPalette, resourcePalette;
            public Material sharkMaterial, whiteSharkMaterial, birdMaterial, shadowMaterial, spearMaterial, piranhaMaterial;
            public Button actionButton;
            public Material nightMaskMaterial;
        }

        const string Root = "Assets/_LifeLine";
        static readonly Color Navy = new Color(.08f, .20f, .28f);
        static readonly Color Teal = new Color(.19f, .84f, .72f);
        static readonly Color Gold = new Color(.99f, .77f, .32f);
        static readonly Color ButtonPaper = new Color(.86f, .9f, .85f);
        static readonly Color DarkInk = new Color(.12f, .23f, .26f);
        static readonly Color[] BrightWorldColors = {
            new Color(.075f,.30f,.40f), new Color(.12f,.40f,.47f), new Color(.43f,.66f,.64f),
            new Color(.67f,.43f,.23f), Gold, new Color(.22f,.84f,.74f), new Color(.76f,.88f,.85f)
        };
        static readonly Color[] BrightResourceColors = {
            new Color(.72f,.49f,.27f), new Color(.69f,.80f,.84f), new Color(.58f,.77f,.40f), new Color(.36f,.72f,.89f),
            new Color(.88f,.55f,.40f), new Color(.78f,.56f,.32f), new Color(.78f,.86f,.90f), new Color(.76f,.87f,.55f),
            new Color(.98f,.65f,.28f), new Color(.77f,.98f,.37f)
        };

        [MenuItem("LifeLine/Art/Apply Bright Prototype Palette")]
        public static void ApplyBrightPrototypePalette()
        {
            // Exact files created by this library; never enumerate unrelated project materials.
            EnsureFolder(Root + "/Materials");
            for (int i = 0; i < BrightWorldColors.Length; i++) ApplyGeneratedTint("World_" + i.ToString("00"), BrightWorldColors[i]);
            for (int i = 0; i < BrightResourceColors.Length; i++) ApplyGeneratedTint("Resource_" + i.ToString("00"), BrightResourceColors[i]);
            ApplyGeneratedTint("CaptainCoat", Gold); ApplyGeneratedTint("DiverSuit", Teal);
            ApplyGeneratedTint("ShipNavy", Navy); ApplyGeneratedTint("CrewSkin", new Color(.98f,.84f,.65f));
            ApplyGeneratedTint("StationTeal", new Color(.20f,.55f,.56f));
            ApplyGeneratedTint("Brass", Gold); ApplyGeneratedTint("Spear", Gold);
            string buttonPath = Root + "/Prefabs/UI/ActionButton.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(buttonPath) != null)
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(buttonPath);
                try
                {
                    var image = contents.GetComponent<Image>(); if (image != null) image.color = ButtonPaper;
                    foreach (var label in contents.GetComponentsInChildren<Text>(true)) label.color = DarkInk;
                    PrefabUtility.SaveAsPrefabAsset(contents, buttonPath);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Applied bright colours to LifeLine generated materials and ActionButton prefab.");
        }
        static void ApplyGeneratedTint(string name, Color color)
        {
            Material material = Material(name, color);
            Undo.RecordObject(material, "Bright LifeLine prototype palette");
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            EditorUtility.SetDirty(material);
        }

        [MenuItem("LifeLine/Migration/Preview Current Rules")]
        public static void PreviewCurrentRulesMigration() => RunCurrentRulesMigration(false);

        [MenuItem("LifeLine/Migration/Apply Current Rules (Preserve Custom Values)")]
        public static void ApplyCurrentRulesMigration() => RunCurrentRulesMigration(true);

        // Public pure-data seam also permits testing migration without touching any project assets.
        public static List<string> MigrateKnownTuningDefaults(GameTuning tuning, bool apply)
        {
            var changes = new List<string>();
            if (tuning == null) return changes;
            void Scalar(string name,float actual,float oldDefault,float next,Action<float> set)
            {
                if (!Mathf.Approximately(actual,oldDefault)) return;
                changes.Add(name+": "+oldDefault+" -> "+next);
                if(apply)set(next);
            }
            void ArrayValue(string name,float[] actual,float[] previous,float[] next,Action<float[]> set)
            {
                if(actual==null || actual.Length!=previous.Length)return;
                for(var i=0;i<actual.Length;i++)if(!Mathf.Approximately(actual[i],previous[i]))return;
                changes.Add(name+": ["+string.Join(",",previous)+"] -> ["+string.Join(",",next)+"]");
                if(apply)set((float[])next.Clone());
            }
            Scalar("baseOxygen",tuning.baseOxygen,60,40,value=>tuning.baseOxygen=value);
            ArrayValue("oxygenLevels",tuning.oxygenLevels,new[]{60f,90f,120f,150f},new[]{40f,70f,115f,145f},value=>tuning.oxygenLevels=value);
            // Earlier tiers may be authored. Only the recognized final rope tier changes.
            if(tuning.ropeLengths!=null && tuning.ropeLengths.Length==4 && Mathf.Approximately(tuning.ropeLengths[3],44))
            { changes.Add("ropeLengths[3]: 44 -> 74"); if(apply)tuning.ropeLengths[3]=74; }
            Scalar("generatorStrokeInterval",tuning.generatorStrokeInterval,1,3,value=>tuning.generatorStrokeInterval=value);
            Scalar("upgradedCraftSeconds (compatibility)",tuning.upgradedCraftSeconds,30,25,value=>tuning.upgradedCraftSeconds=value);
            Scalar("caveChance",tuning.caveChance,.55f,.05f,value=>tuning.caveChance=value);
            Scalar("eelCaveChance",tuning.eelCaveChance,.75f,.05f,value=>tuning.eelCaveChance=value);
            Scalar("shallowLootCount",tuning.shallowLootCount,12,9,value=>tuning.shallowLootCount=(int)value);
            Scalar("middleLootCount",tuning.middleLootCount,22,21,value=>tuning.middleLootCount=(int)value);
            Scalar("deepLootCount",tuning.deepLootCount,42,40,value=>tuning.deepLootCount=(int)value);
            return changes;
        }

        public static List<string> MigrateKnownSessionDefaults(GameSession session,RecipeDefinition quickRepair,bool apply)
        {
            var changes=new List<string>();
            if(session==null)return changes;
            var cost=session.workbenchUpgradeCost;
            if(cost!=null && cost.Length==2 && cost[0]!=null && cost[1]!=null && cost[0].amount==1 && cost[1].amount==1 &&
                (cost[0].resource==ResourceKind.Plank && cost[1].resource==ResourceKind.Plate || cost[1].resource==ResourceKind.Plank && cost[0].resource==ResourceKind.Plate))
            {
                changes.Add("workbenchUpgradeCost: 1 Plank + 1 Plate -> 2 Plank");
                if(apply)session.workbenchUpgradeCost=new[]{new ResourceAmount(ResourceKind.Plank,2)};
            }
            var recipes=session.recipes??Array.Empty<RecipeDefinition>();
            if(quickRepair!=null && Array.Find(recipes,recipe=>recipe!=null && recipe.id=="quick-hull")==null)
            {
                changes.Add("recipes: append quick-hull (existing recipe references preserved)");
                if(apply){var list=new List<RecipeDefinition>(recipes);list.Add(quickRepair);session.recipes=list.ToArray();}
            }
            return changes;
        }

        static void RunCurrentRulesMigration(bool apply)
        {
            var report=new StringBuilder(apply?"LifeLine explicit rule migration\n":"LifeLine migration preview (no assets modified)\n");
            var backupFolder=Root+"/Documentation/MigrationBackups/"+DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");
            var tuningPath=Root+"/Data/Tuning/GameTuning.asset";
            var tuning=AssetDatabase.LoadAssetAtPath<GameTuning>(tuningPath);
            if(tuning==null)report.AppendLine("No existing tuning asset; first Build uses new defaults.");
            else
            {
                var changes=MigrateKnownTuningDefaults(tuning,false);
                foreach(var change in changes)report.AppendLine("Tuning / "+change);
                if(apply && changes.Count>0)
                {
                    if(EditorUtility.IsDirty(tuning))report.AppendLine("Skipped dirty tuning asset; save your Inspector edits before applying migration.");
                    else{BackupFile(tuningPath,backupFolder);Undo.RecordObject(tuning,"LifeLine rule migration");MigrateKnownTuningDefaults(tuning,true);EditorUtility.SetDirty(tuning);AssetDatabase.SaveAssetIfDirty(tuning);}
                }
            }
            RecipeDefinition quick=AssetDatabase.LoadAssetAtPath<RecipeDefinition>(Root+"/Data/Recipes/13_QuickHull.asset");
            if(apply && quick==null)
            {
                EnsureFolder(Root+"/Data/Recipes");
                quick=Recipe("13_QuickHull","quick-hull","快速修补","1木板恢复10船体，耗时为普通修补的一半。",new[]{Cost(ResourceKind.Plank,1)},repair:10,timeMultiplier:.5f);
                report.AppendLine("Created missing quick-hull asset.");
            }
            foreach(var crewName in new[]{"Captain","Diver"})
            {
                var prefabPath=Root+"/Prefabs/Characters/"+crewName+".prefab";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                var actor=prefab!=null?prefab.GetComponent<CrewActor>():null;
                if(actor==null || !Mathf.Approximately(actor.waterBottomY,-45f))continue;
                report.AppendLine(crewName+" / waterBottomY: -45 -> -54");
                if(!apply)continue;
                var stage=PrefabStageUtility.GetCurrentPrefabStage();
                if(EditorUtility.IsDirty(prefab) || EditorUtility.IsDirty(actor) || stage!=null && stage.assetPath==prefabPath)
                {report.AppendLine("Skipped edited/open prefab; save and close Prefab Mode before migrating "+crewName);continue;}
                BackupFile(prefabPath,backupFolder);
                var contents=PrefabUtility.LoadPrefabContents(prefabPath);
                try
                {
                    contents.GetComponent<CrewActor>().waterBottomY=-54f;
                    PrefabUtility.SaveAsPrefabAsset(contents,prefabPath);
                }
                finally{PrefabUtility.UnloadPrefabContents(contents);}
            }
            var scenePath=GameProjectBuilder.PlayScene;
            if(!File.Exists(scenePath))report.AppendLine("No existing Gameplay scene; first Build adds all current recipes and defaults.");
            else
            {
                var scene=SceneManager.GetSceneByPath(scenePath);
                var opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
                try
                {
                    if(apply && scene.isDirty)report.AppendLine("Skipped dirty Gameplay; save your scene edits before applying migration.");
                    else
                    {
                        var backedUp=false;
                        foreach(var root in scene.GetRootGameObjects())foreach(var session in root.GetComponentsInChildren<GameSession>(true))
                        {
                            var changes=MigrateKnownSessionDefaults(session,quick,false);
                            if(!apply && quick==null && Array.Find(session.recipes??Array.Empty<RecipeDefinition>(),recipe=>recipe!=null && recipe.id=="quick-hull")==null)
                                report.AppendLine("Gameplay / append missing quick-hull after creating its asset.");
                            foreach(var change in changes)report.AppendLine("Gameplay / "+change);
                            if(apply && changes.Count>0)
                            {
                                if(!backedUp){BackupFile(scenePath,backupFolder);backedUp=true;}
                                Undo.RecordObject(session,"LifeLine rule migration");MigrateKnownSessionDefaults(session,quick,true);EditorUtility.SetDirty(session);
                            }
                        }
                        if(apply && backedUp){EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene,scenePath);}
                    }
                }
                finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
            }
            if(apply){AssetDatabase.Refresh();report.AppendLine("Backups (only changed existing files): "+backupFolder);}
            report.AppendLine("Unrecognized custom values are preserved. Removed legacy cost arrays are unused; new cost fields retain their new defaults.");
            Debug.Log(report.ToString());
        }

        static void BackupFile(string source,string folder)
        {
            EnsureFolder(folder);
            var target=folder+"/"+Path.GetFileName(source)+".bak";
            File.Copy(source,target,false);
            if(File.Exists(source+".meta"))File.Copy(source+".meta",target+".meta.bak",false);
        }

        public static Library Build()
        {
            foreach (var folder in new[] { "Materials", "Data/Tuning", "Data/Recipes", "Prefabs/Characters", "Prefabs/Ship", "Prefabs/Stations", "Prefabs/World", "Prefabs/Enemies", "Prefabs/UI" })
                EnsureFolder(Root + "/" + folder);
            var library = new Library();
            library.tuning = LoadOrCreateData<GameTuning>(Root + "/Data/Tuning/GameTuning.asset");
            library.input = InputAssetBuilder.CreateOrUpdateAsset();
            library.recipes = BuildRecipes();
            library.worldPalette = Materials("World", BrightWorldColors);
            library.resourcePalette = Materials("Resource", BrightResourceColors);
            library.sharkMaterial = Material("Shark", new Color(.25f,.47f,.55f));
            library.whiteSharkMaterial = Material("WhiteShark", new Color(.83f,.91f,.94f));
            library.piranhaMaterial = Material("Piranha", new Color(.95f,.40f,.25f));
            library.birdMaterial = Material("Bird", new Color(.66f,.67f,.69f));
            library.shadowMaterial = Material("Shadow", new Color(.16f,.07f,.23f));
            library.spearMaterial = Material("Spear", Gold);
            var crewCaptain = Material("CaptainCoat", Gold);
            var crewDiver = Material("DiverSuit", Teal);
            var dark = Material("ShipNavy", Navy);
            var pale = Material("CrewSkin", new Color(.98f,.84f,.65f));
            var station = Material("StationTeal", new Color(.20f,.55f,.56f));
            var brass = Material("Brass", Gold);
            library.captain = Prefab("Characters/Captain", () => Crew("Captain", CrewRole.Captain, library.input, crewCaptain, dark, pale, brass));
            library.diver = Prefab("Characters/Diver", () => Crew("Diver", CrewRole.Diver, library.input, crewDiver, dark, pale, brass));
            var stations = new Dictionary<StationKind, GameObject>();
            foreach (StationKind kind in Enum.GetValues(typeof(StationKind)))
            {
                var stationKind = kind;
                stations[kind] = Prefab("Stations/" + kind, () => Station(stationKind, station, dark, brass));
            }
            library.ship = Prefab("Ship/Ship", () => Ship(stations, dark, library.resourcePalette[(int)ResourceKind.Plank], brass));
            library.loot = Prefab("World/Loot", () => Pickup(false, library.resourcePalette));
            library.crate = Prefab("World/Crate", () => Pickup(true, library.resourcePalette));
            library.trapdoor = Prefab("World/Trapdoor", () => Trapdoor(library.worldPalette[2], brass));
            library.shark = Prefab("Enemies/Shark", () => Enemy("Shark", EnemyKind.Shark, library.sharkMaterial, dark, brass));
            library.piranha = Prefab("Enemies/Piranha", () => Enemy("Piranha", EnemyKind.Piranha, library.piranhaMaterial, dark, brass));
            library.bird = Prefab("Enemies/Bird", () => Enemy("Bird", EnemyKind.Bird, library.birdMaterial, dark, brass));
            library.shadow = Prefab("Enemies/Shadow", () => Enemy("Shadow", EnemyKind.Shadow, library.shadowMaterial, dark, brass));
            library.spear = Prefab("Enemies/Spear", () => Spear(library.spearMaterial));
            library.actionButton = Prefab("UI/ActionButton", ActionButton).GetComponent<Button>();
            library.nightMaskMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/NightMask.mat");
            var nightShader = Shader.Find("LifeLine/UI/Night Visibility");
            if (library.nightMaskMaterial == null && nightShader != null) library.nightMaskMaterial = Material("NightMask", Color.white, nightShader);
            AssetDatabase.SaveAssets();
            return library;
        }

        static GameObject Crew(string name, CrewRole role, InputActionAsset input, Material suit, Material dark, Material skin, Material brass)
        {
            var root = new GameObject(name) { layer = 9 };
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false; body.isKinematic = false;
            body.constraints = RigidbodyConstraints.FreezePositionZ | RigidbodyConstraints.FreezeRotation;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var collider = root.AddComponent<CapsuleCollider>(); collider.height = 1.8f; collider.radius = .32f; collider.center = Vector3.zero;
            var reader = root.AddComponent<CrewInputReader>(); reader.role = role; reader.actions = input;
            var visual = Child(root.transform, "VisualRoot");
            Primitive(visual, "Suit", PrimitiveType.Capsule, new Vector3(0,-.12f,0), new Vector3(.56f,.58f,.48f), suit);
            Primitive(visual, "Head", PrimitiveType.Sphere, new Vector3(0,.52f,-.015f), Vector3.one*.49f, role == CrewRole.Diver ? brass : skin);
            Primitive(visual, "BootLeft", PrimitiveType.Cube, new Vector3(-.17f,-.72f,-.02f), new Vector3(.23f,.24f,.48f), dark);
            Primitive(visual, "BootRight", PrimitiveType.Cube, new Vector3(.17f,-.72f,-.02f), new Vector3(.23f,.24f,.48f), dark);
            if (role == CrewRole.Captain)
            {
                Primitive(visual, "Cap", PrimitiveType.Cube, new Vector3(0,.75f,0), new Vector3(.58f,.16f,.5f), dark);
                Primitive(visual, "Belt", PrimitiveType.Cube, new Vector3(0,-.12f,-.25f), new Vector3(.57f,.11f,.06f), brass);
            }
            else
            {
                Primitive(visual, "Faceplate", PrimitiveType.Cube, new Vector3(0,.52f,-.24f), new Vector3(.38f,.25f,.08f), dark);
                Primitive(visual, "OxygenTank", PrimitiveType.Capsule, new Vector3(0,-.08f,.28f), new Vector3(.3f,.4f,.3f), brass);
            }
            var animator = visual.gameObject.AddComponent<Animator>();
            var actor = root.AddComponent<CrewActor>();
            actor.role = role; actor.body = body; actor.input = reader; actor.visualRoot = visual; actor.animator = animator;
            actor.groundMask = (1 << 8) | (1 << 10);
            var presentation = root.AddComponent<CrewPresentation>(); presentation.actor = actor; presentation.visualRoot = visual; presentation.animator = animator;
            SetLayer(root, 9);
            return root;
        }

        static GameObject Ship(Dictionary<StationKind, GameObject> stations, Material hullMaterial, Material deckMaterial, Material trim)
        {
            var root = new GameObject("Ship") { layer = 10 };
            var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate; body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ;
            var motor = root.AddComponent<ShipMotor>(); motor.body = body;
            var visual = Child(root.transform, "VisualRoot");
            Primitive(visual, "HullBack", PrimitiveType.Cube, new Vector3(0,-2.1f,1.3f), new Vector3(24,4.2f,.2f), hullMaterial);
            Solid(root.transform, "DeckWest", new Vector3(-6.45f,-.2f,0), new Vector3(11.1f,.4f,2.2f), deckMaterial);
            Solid(root.transform, "DeckEast", new Vector3(6.45f,-.2f,0), new Vector3(11.1f,.4f,2.2f), deckMaterial);
            var hatch = Solid(root.transform, "LadderHatch", new Vector3(0,-.2f,0), new Vector3(1.8f,.4f,2.2f), trim);
            Solid(root.transform, "CabinFloor", new Vector3(0,-4.2f,0), new Vector3(24,.4f,2.2f), deckMaterial);
            Solid(root.transform, "CabinWestWall", new Vector3(-12,-2.1f,0), new Vector3(.25f,4,2.2f), hullMaterial);
            Solid(root.transform, "CabinEastWall", new Vector3(12,-2.1f,0), new Vector3(.25f,4,2.2f), hullMaterial);
            var hatchControl = root.AddComponent<ShipLadderHatch>(); hatchControl.hatchCollider = hatch.GetComponent<Collider>();
            for (var x = -1; x <= 1; x += 2)
                Primitive(visual, "LadderRail", PrimitiveType.Cube, new Vector3(x*.4f,-1.8f,.75f), new Vector3(.09f,4.4f,.1f), trim);
            for (var i = 0; i < 11; i++)
                Primitive(visual, "LadderRung", PrimitiveType.Cube, new Vector3(0,-3.8f+i*.4f,.7f), new Vector3(.9f,.065f,.12f), trim);
            var stationRoot = Child(root.transform, "Stations");
            foreach (var entry in stations)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(entry.Value, stationRoot);
                instance.transform.localPosition = new Vector3(StationX(entry.Key),0,0);
            }
            SetLayer(root, 10);
            return root;
        }

        static bool Cabin(StationKind kind) => kind == StationKind.HullRepair || kind == StationKind.Filter || kind == StationKind.Circuit || kind == StationKind.Generator || kind == StationKind.Engine;
        static float StationX(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Fishing: return -10.5f;
                case StationKind.Workbench: return -7.2f;
                case StationKind.TotalControl: return -4.2f;
                case StationKind.Helm: return -1.2f;
                case StationKind.Winch: return 1.8f;
                case StationKind.DivePreparation: return 4.8f;
                case StationKind.Searchlight: return 7.8f;
                case StationKind.Turret: return 10.8f;
                case StationKind.HullRepair: return -9f;
                case StationKind.Filter: return -5.5f;
                case StationKind.Circuit: return -2f;
                case StationKind.Generator: return 3f;
                default: return 8f;
            }
        }

        static GameObject Station(StationKind kind, Material stationMaterial, Material dark, Material trim)
        {
            var root = new GameObject(kind.ToString()) { layer = 10 };
            var anchor = root.AddComponent<StationAnchor>(); anchor.kind = kind; anchor.displayName = GameNames.Station(kind); anchor.interactionRadius = 1.35f;
            var floor = Cabin(kind) ? -4f : 0f;
            var point = Child(root.transform, "InteractionPoint"); point.localPosition = new Vector3(0,floor+.9f,0); anchor.interactionPoint = point;
            var visual = Child(root.transform, "VisualRoot"); anchor.visualRoot = visual;
            Primitive(visual, "Housing", PrimitiveType.Cube, new Vector3(0,floor+.42f,.4f), new Vector3(1.3f,.84f,.65f), stationMaterial);
            Primitive(visual, "Panel", PrimitiveType.Cube, new Vector3(0,floor+.8f,-.01f), new Vector3(.95f,.28f,.1f), dark);
            if (kind == StationKind.Helm || kind == StationKind.Winch || kind == StationKind.Generator)
            {
                var wheel = Primitive(visual, "Wheel", PrimitiveType.Cylinder, new Vector3(0,floor+1.1f,-.07f), new Vector3(.7f,.07f,.7f), trim);
                wheel.localRotation = Quaternion.Euler(90,0,0);
                Primitive(visual, "Spoke", PrimitiveType.Cube, new Vector3(0,floor+1.1f,-.17f), new Vector3(.06f,.8f,.05f), dark);
            }
            else if (kind == StationKind.Searchlight || kind == StationKind.Turret)
            {
                Primitive(visual, "Head", PrimitiveType.Sphere, new Vector3(0,floor+1.15f,.1f), new Vector3(.7f,.5f,.55f), trim);
                Primitive(visual, "Barrel", PrimitiveType.Cube, new Vector3(.55f,floor+1.15f,.1f), new Vector3(.8f,.15f,.2f), dark);
            }
            else Primitive(visual, "Indicator", PrimitiveType.Sphere, new Vector3(.4f,floor+.8f,-.11f), Vector3.one*.12f, trim);
            var labelRoot = Child(visual,"StationLabel"); labelRoot.localPosition = new Vector3(0,floor+1.7f,-.6f);
            var label = labelRoot.gameObject.AddComponent<TextMesh>(); label.text = GameNames.Station(kind); label.anchor = TextAnchor.MiddleCenter;
            label.fontSize = 40; label.characterSize = .09f; label.color = Cabin(kind) ? new Color(.86f,.95f,.9f) : DarkInk;
            SetLayer(root, 10);
            return root;
        }

        static GameObject Pickup(bool crate, Material[] palette)
        {
            var root = new GameObject(crate ? "Crate" : "Loot") { layer = 11 };
            var collider = root.AddComponent<BoxCollider>(); collider.isTrigger = true;
            collider.size = crate ? new Vector3(1.05f,.65f,.8f) : Vector3.one*.5f;
            var pickup = root.AddComponent<LootPickup>(); pickup.isCrate = crate; pickup.amount = 1; pickup.capacity = 3;
            pickup.resourceMaterials = palette;
            var visual = Child(root.transform,"VisualRoot"); pickup.visualRoot = visual;
            Primitive(visual,"Body",PrimitiveType.Cube,Vector3.zero,crate ? new Vector3(1.05f,.65f,.65f) : Vector3.one*.38f,palette[(int)ResourceKind.Wood]);
            if (crate)
                for (var i = -1; i <= 1; i += 2)
                    Primitive(visual,"MetalBand",PrimitiveType.Cube,new Vector3(i*.32f,0,-.34f),new Vector3(.08f,.7f,.06f),palette[(int)ResourceKind.Metal]);
            SetLayer(root,11);
            return root;
        }

        static GameObject Trapdoor(Material frame, Material trim)
        {
            var root = new GameObject("Trapdoor") { layer = 8 };
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(.18f,1.96f,1.8f); collider.isTrigger = false;
            var gate = root.AddComponent<TrapdoorGate>(); gate.barrier = collider; gate.interactionDistance = 1.9f;
            var visual = Child(root.transform,"VisualRoot"); gate.visualRoot = visual;
            visual.localScale = collider.size;
            Primitive(visual,"DoorPanel",PrimitiveType.Cube,Vector3.zero,Vector3.one,frame);
            Primitive(visual,"DoorStripe",PrimitiveType.Cube,new Vector3(0,0,-.51f),new Vector3(1.02f,.14f,.025f),trim);
            var labelRoot = Child(root.transform,"Interaction label"); labelRoot.localPosition = new Vector3(0,1.3f,-1);
            gate.label = labelRoot.gameObject.AddComponent<TextMesh>(); gate.label.text = "HATCH / ENTER";
            gate.label.characterSize = .1f; gate.label.fontSize = 48; gate.label.anchor = TextAnchor.MiddleCenter; gate.label.color = Gold;
            SetLayer(root,8);
            return root;
        }

        static GameObject Enemy(string name, EnemyKind kind, Material material, Material dark, Material trim)
        {
            var root = new GameObject(name) { layer = 13 };
            var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;
            var collider = root.AddComponent<SphereCollider>(); collider.isTrigger = true; collider.radius = kind == EnemyKind.Shadow ? 2.8f : kind == EnemyKind.Bird ? .5f : 1f;
            var enemy = root.AddComponent<LifelineEnemy>(); enemy.kind = kind; enemy.body = body; enemy.hitCollider = collider;
            enemy.health = enemy.maxHealth = kind == EnemyKind.Shadow ? 20 : kind == EnemyKind.Shark ? 2 : 1;
            enemy.geometryMask = 1 << 8;
            var visual = Child(root.transform,"VisualRoot"); enemy.visualRoot = visual;
            var size = kind == EnemyKind.Shadow ? new Vector3(7,2.4f,1.6f) : kind == EnemyKind.Bird ? new Vector3(1.1f,.45f,.6f) : new Vector3(2.4f,.8f,.8f);
            Primitive(visual,"Body",PrimitiveType.Sphere,Vector3.zero,size,material);
            if (kind == EnemyKind.Bird)
            {
                Primitive(visual,"Wings",PrimitiveType.Cube,new Vector3(0,.08f,.1f),new Vector3(2,.1f,.65f),dark);
                Primitive(visual,"Beak",PrimitiveType.Cube,new Vector3(.6f,0,0),new Vector3(.25f,.13f,.15f),trim);
            }
            else
            {
                var tail = Primitive(visual,"Tail",PrimitiveType.Cube,new Vector3(-size.x*.46f,0,.05f),new Vector3(.38f,size.y*.9f,.25f),material);
                tail.localRotation = Quaternion.Euler(0,0,35);
                Primitive(visual,"Eye",PrimitiveType.Sphere,new Vector3(size.x*.28f,size.y*.12f,-size.z*.48f),Vector3.one*(kind == EnemyKind.Shadow ? .24f : .12f),trim);
            }
            SetLayer(root,13);
            return root;
        }

        static GameObject Spear(Material material)
        {
            var root = new GameObject("Spear") { layer = 13 };
            root.AddComponent<LifelineSpear>();
            var visual = Child(root.transform,"VisualRoot");
            Primitive(visual,"Shaft",PrimitiveType.Cube,Vector3.zero,new Vector3(.06f,.85f,.06f),material);
            Primitive(visual,"Tip",PrimitiveType.Sphere,new Vector3(0,.45f,0),new Vector3(.11f,.16f,.11f),material);
            SetLayer(root,13);
            return root;
        }

        static GameObject ActionButton()
        {
            var root = new GameObject("ActionButton",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button),typeof(LayoutElement),typeof(RuntimeUIFont));
            var rect = root.GetComponent<RectTransform>(); rect.sizeDelta = new Vector2(320,42);
            var image = root.GetComponent<Image>(); image.color = ButtonPaper;
            var button = root.GetComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.normalColor = Color.white; colors.highlightedColor = new Color(.7f,1,.9f); colors.pressedColor = Gold; colors.selectedColor = new Color(.7f,1,.9f); colors.disabledColor = new Color(.45f,.5f,.5f); button.colors = colors;
            var layout = root.GetComponent<LayoutElement>(); layout.preferredHeight = layout.minHeight = 42; layout.flexibleWidth = 1;
            var textObject = new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)); textObject.transform.SetParent(root.transform,false);
            var textRect = textObject.GetComponent<RectTransform>(); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(12,2); textRect.offsetMax = new Vector2(-12,-2);
            var text = textObject.GetComponent<Text>(); text.text = "操作"; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = 16; text.color = DarkInk; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            return root;
        }

        static RecipeDefinition[] BuildRecipes()
        {
            return new[] {
                Recipe("01_Plank","plank","木板","2份木材加工为1块木板。",new[] { Cost(ResourceKind.Wood,2) },new[] { Cost(ResourceKind.Plank,1) }),
                Recipe("02_Plate","plate","铁板","2份金属加工为1块铁板。",new[] { Cost(ResourceKind.Metal,2) },new[] { Cost(ResourceKind.Plate,1) }),
                Recipe("03_Bundle","bundle","纤维束","2份纤维加工为1束。",new[] { Cost(ResourceKind.Fiber,2) },new[] { Cost(ResourceKind.FiberBundle,1) }),
                Recipe("04_Bait","bait","鲨鱼诱饵","投放后将附近鲨鱼引开8秒。",new[] { Cost(ResourceKind.Fish,1),Cost(ResourceKind.Fiber,1) },new[] { Cost(ResourceKind.Bait,1) }),
                Recipe("05_Rod","rod","钓鱼竿","解锁垂钓与优质鱼。",new[] { Cost(ResourceKind.Plank,1),Cost(ResourceKind.FiberBundle,1) },tool:"rod"),
                Recipe("06_Harpoon","harpoon","鱼叉","近战鱼叉；制作射矛枪时消耗1把。",new[] { Cost(ResourceKind.Plate,2) },tool:"harpoon"),
                Recipe("07_Speargun","speargun","射矛枪","消耗1把鱼叉；独立鱼枪键发射，再按召回。",new[] { Cost(ResourceKind.Plate,2),Cost(ResourceKind.FiberBundle,2) },tool:"speargun",consumedTools:new[]{"harpoon"}),
                Recipe("14_Generator","generator","手摇发电机","2金属、2木材、1纤维建造船舱手摇发电机。",new[]{Cost(ResourceKind.Metal,2),Cost(ResourceKind.Wood,2),Cost(ResourceKind.Fiber,1)},tool:"generator"),
                Recipe("15_RepairBench","repair-bench","船体维修台","1木材、1金属建造船体维修台。",new[]{Cost(ResourceKind.Wood,1),Cost(ResourceKind.Metal,1)},tool:"repair-bench"),
                Recipe("08_Antenna","antenna","无线电·天线","先修复船舱电路。",new[] { Cost(ResourceKind.Plank,1),Cost(ResourceKind.Plate,1) },radio:1),
                Recipe("09_Receiver","receiver","无线电·接收器","接收并定位求救信号。",new[] { Cost(ResourceKind.Plate,2),Cost(ResourceKind.FiberBundle,1) },radio:2),
                Recipe("10_Amplifier","amplifier","无线电·放大器","完成电台后从总控发起救援。",new[] { Cost(ResourceKind.Plate,2),Cost(ResourceKind.FiberBundle,2) },radio:3),
                Recipe("11_PatchHull","patch-hull","船体补强","消耗木板、铁板恢复40船体。",new[] { Cost(ResourceKind.Plank,1),Cost(ResourceKind.Plate,1) },repair:40),
                Recipe("13_QuickHull","quick-hull","快速修补","1木板恢复10船体，耗时为普通修补的一半。",new[] { Cost(ResourceKind.Plank,1) },repair:10,timeMultiplier:.5f),
                Recipe("12_RestoreHull","restore-hull","船体大修","消耗木板、铁板恢复90船体。",new[] { Cost(ResourceKind.Plank,2),Cost(ResourceKind.Plate,2) },repair:90)
            };
        }

        static ResourceAmount Cost(ResourceKind resource,int amount) => new ResourceAmount(resource,amount);
        static RecipeDefinition Recipe(string filename,string id,string title,string description,ResourceAmount[] cost,ResourceAmount[] output=null,string tool="",int radio=0,float repair=0,float timeMultiplier=1f,string[] consumedTools=null)
        {
            var path = Root+"/Data/Recipes/"+filename+".asset";
            var existing = AssetDatabase.LoadAssetAtPath<RecipeDefinition>(path); if (existing != null) return existing;
            var recipe = ScriptableObject.CreateInstance<RecipeDefinition>(); recipe.id=id; recipe.displayName=title; recipe.description=description;
            recipe.cost=cost; recipe.output=output??Array.Empty<ResourceAmount>(); recipe.toolId=tool; recipe.radioStage=radio; recipe.repairAmount=repair;
            recipe.allowBatch = id == "plank" || id == "plate" || id == "bundle" || id == "bait";
            recipe.craftTimeMultiplier=timeMultiplier; recipe.consumedToolIds=consumedTools??Array.Empty<string>();
            AssetDatabase.CreateAsset(recipe,path); return recipe;
        }
        static T LoadOrCreateData<T>(string path) where T:ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path); if(existing!=null)return existing;
            var asset=ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset,path); return asset;
        }
        static Material[] Materials(string prefix,Color[] colors)
        {
            var result=new Material[colors.Length]; for(var i=0;i<result.Length;i++)result[i]=Material(prefix+"_"+i.ToString("00"),colors[i]); return result;
        }
        static Material Material(string name,Color color,Shader shader=null)
        {
            var path=Root+"/Materials/"+name+".mat"; var existing=AssetDatabase.LoadAssetAtPath<Material>(path); if(existing!=null)return existing;
            shader=shader??Shader.Find("Universal Render Pipeline/Unlit")??Shader.Find("Unlit/Color");
            if(shader==null)throw new InvalidOperationException("LifeLine placeholder shader is unavailable.");
            var material=new Material(shader){name=name}; material.color=color;
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            AssetDatabase.CreateAsset(material,path); return material;
        }
        static GameObject Prefab(string relativePath,Func<GameObject> build)
        {
            var path=Root+"/Prefabs/"+relativePath+".prefab"; var existing=AssetDatabase.LoadAssetAtPath<GameObject>(path); if(existing!=null)return existing;
            GameObject temporary=null;
            try{temporary=build(); return PrefabUtility.SaveAsPrefabAsset(temporary,path);}
            finally{if(temporary!=null)UnityEngine.Object.DestroyImmediate(temporary);}
        }
        static Transform Child(Transform parent,string name)
        {var child=new GameObject(name).transform;child.SetParent(parent,false);return child;}
        static Transform Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
        {
            var item=GameObject.CreatePrimitive(type);item.name=name;item.transform.SetParent(parent,false);item.transform.localPosition=position;item.transform.localScale=scale;
            UnityEngine.Object.DestroyImmediate(item.GetComponent<Collider>());item.GetComponent<Renderer>().sharedMaterial=material;return item.transform;
        }
        static GameObject Solid(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var item=GameObject.CreatePrimitive(PrimitiveType.Cube);item.name=name;item.transform.SetParent(parent,false);item.transform.localPosition=position;item.transform.localScale=scale;item.GetComponent<Renderer>().sharedMaterial=material;return item;
        }
        static void SetLayer(GameObject root,int layer)
        {foreach(var child in root.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;}
        static void EnsureFolder(string path)
        {
            var parts=path.Split('/');var parent=parts[0];
            for(var i=1;i<parts.Length;i++){var next=parent+"/"+parts[i];if(!AssetDatabase.IsValidFolder(next))AssetDatabase.CreateFolder(parent,parts[i]);parent=next;}
        }
    }
}
#endif
