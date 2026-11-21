using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Seeded ruins built from connected cell graphs and matching 3D collision walls.</summary>
    public partial class WorldGenerator : MonoBehaviour
    {
        public GameSession session;
        public GameTuning tuning;
        public int seed = 2718;
        public int activeDay = 1, generatedSeed;
        public bool generatedCave, generatedEel;
        public Material[] placeholderMaterials;
        [Tooltip("Optional materials indexed by ResourceKind. Prefab palettes are preserved if unset.")]
        public Material[] resourceMaterials;
        public PixelWorldArt pixelArt;
        public PixelOceanArt oceanArt;
        public GameObject lootPrefab, cratePrefab, sharkPrefab, shadowPrefab, trapdoorPrefab, piranhaPrefab;
        public List<LootPickup> pickups = new List<LootPickup>();
        public Transform generatedRoot;
        public Transform solidRoot;
        public List<Vector3> sharkSpawnPoints = new List<Vector3>();
        public List<Vector3> piranhaSpawnPoints = new List<Vector3>();
        public List<Vector3> caveGuardSpawnPoints = new List<Vector3>();
        [Header("城区中层设备打捞 / City middle-depth equipment salvage")]
        [Range(0, 1)] public float filterSalvageChance = .30f;
        public Vector2 filterSalvageDepths = new Vector2(20, 35);
        [Min(.1f)] public float filterSalvageSeconds = 3f;
        [Range(0, 6)] public int filterGuardMinimum = 2;
        [Min(.1f)] public float filterGuardRadius = 6f;
        [Tooltip("Optional replaceable visual prefab; gameplay remains on FilterSalvage / LootPickup.")]
        public GameObject filterSalvageVisualPrefab;
        public FilterSalvage filterSalvage;
        [Header("洞穴占位材质 / Replaceable cave surfaces")]
        public Material caveWallMaterial, caveBackgroundMaterial;
        public Color caveWallColor = new Color(.90f, .60f, .30f);
        public Color caveBackgroundColor = new Color(.18f, .13f, .24f);
        Material generatedCaveWall, generatedCaveBackground;
        public List<MazeLayout> layouts = new List<MazeLayout>();
        public List<TrapdoorGate> trapdoors = new List<TrapdoorGate>();
        public List<UnderwaterHideSpot> hideSpots = new List<UnderwaterHideSpot>();
        public List<RuinConnection> regionalConnections = new List<RuinConnection>();

        [Header("Geometry (metres, XY physics)")]
        public Rect worldBounds = new Rect(-22, -55, 244.22223f, 65);
        public float waterSurface = -1.5f;
        [Min(2.6f)] public float roomSize = 3f;
        [Min(1.9f)] public float doorWidth = 2.2f;
        [Min(.1f)] public float wallThickness = .24f;
        [Min(.5f)] public float wallDepth = 1.8f;
        [Range(0, 31)] public int geometryLayer;
        [Range(0, 31)] public int pickupLayer;
        [Range(0, 1)] public float ruinLoopChance = .2f;
        [Range(0, 1)] public float caveLoopChance = .04f;
        [Tooltip("Chance of an extra left/right opening in addition to each ruin's guaranteed top, bottom, left and right exits. Caves excluded.")]
        [Range(0, 1)] public float extraSideOpeningChance = .55f;
        public bool generateCaves = true;
        public bool forceCave;
        public bool drawLabels = true;
        public bool drawRuinTitles;
        [Min(.001f)] public float interactionLabelSize = .06f;
        [Min(.1f)] public float labelVisibleDistance = 2.4f;
        [Min(.1f)] public float collectionRadius = 1.65f;
        [Tooltip("Legacy crate ID reservations only. Actual active counts use the building feature ranges below.")]
        [Min(0)] public int cratesPerZone = 2;
        [Header("Random building features (inclusive count ranges, caves excluded)")]
        [Min(1)] public int largeBuildingMinimumRooms = 24;
        public Vector2Int smallBuildingHideCount = new Vector2Int(2, 3);
        public Vector2Int largeBuildingHideCount = new Vector2Int(3, 5);
        public Vector2Int smallBuildingCrateCount = new Vector2Int(1, 2);
        public Vector2Int largeBuildingCrateCount = new Vector2Int(2, 4);
        public Vector3 buildingCrateOffset = new Vector3(.72f, -.20f, -.12f);
        public Vector3 buildingHideOffset = new Vector3(-.65f, -.08f, 0);
        [Min(.1f)] public float maximumLootDepth = 50f;
        [Tooltip("Horizontal sea areas; all contain the complete depth sequence.")]
        public float[] areaCenters = { 0, 80, 160 };
        public float[] bandTopDepths = { 10, 21.5f, 36 };
        public Vector2Int[] bandRoomCounts = { new Vector2Int(5, 3), new Vector2Int(7, 4), new Vector2Int(9, 5) };
        public float[] bandCellSizes = { 3, 3, 2.8f };
        [Header("Route coverage (additional buildings, original building sizes retained)")]
        public bool extendRouteCoverage = true;
        [Min(2.2f)] public float coverageBuildingGap = 2.5f;
        [Tooltip("Order of the four resource weight components. Can switch raw or finished-material drops.")]
        public ResourceKind[] weightedResourceKinds = { ResourceKind.Fiber, ResourceKind.Wood, ResourceKind.Metal, ResourceKind.Fuel };
        public Vector4 shallowResourceWeights = new Vector4(35, 35, 20, 10);
        public Vector4 middleResourceWeights = new Vector4(30, 30, 25, 15);
        public Vector4 deepResourceWeights = new Vector4(25, 25, 30, 20);
        [Header("Independent deep-area / deepest-band events")]
        [Range(0, 1)] public float caveBaseChance = .05f;
        [Range(0, 1)] public float eelBaseChance = .05f;
        [Range(0, 1)] public float eventChancePerDayAfterThree = .02f;
        [Range(0, 1)] public float eventChanceCap = .15f;
        public int dailySeedStride = 104729;
        [Header("Underwater trapdoors")]
        public float trapdoorMinimumDepth = 30;
        [Range(0, 1)] public float trapdoorChance = .18f;
        public float trapdoorInteractionDistance = 1.9f;

        [Header("Validation (match the crew capsule)")]
        public float validationRadius = .32f;
        public float validationHeight = 1.8f;
        [Tooltip("Full collision validation is a developer check; leave off during normal daily regeneration.")]
        public bool validateOnGeneration;
        [TextArea] public string validationReport;

        public float WaterSurface => waterSurface;
        public Vector3 BoatAnchor => session != null ? session.BoatAnchor : new Vector3(0, waterSurface - .7f, 0);
        public IReadOnlyList<MazeLayout> Layouts => layouts;
        readonly List<BoxCollider> generatedWalls = new List<BoxCollider>();
        readonly List<Bounds> validationWallBounds = new List<Bounds>();
        readonly List<Bounds> pathWallCandidates = new List<Bounds>();
        static Mesh sharedCubeMesh;
        int pickupSequence;
        int[] labelFrames={-1,-1};
        Transform[] labelTargets=new Transform[2];
        public int forceCaveDay=-1;
        [Tooltip("Optional one-day filter salvage override. Acquired filters remain unique.")]
        public int forceFilterDay=-1;
        public Color nightOceanColor=new Color(.025f,.075f,.14f),duskOceanColor=new Color(.07f,.19f,.27f);
        Material[] fallbackMaterials;

        [ContextMenu("Generate configured seed")]
        public void GenerateConfiguredSeed() => Generate(seed);

        public void Generate(int requestedSeed) => Generate(requestedSeed, session != null ? session.day : activeDay);

        internal void Generate(int requestedSeed, int generationDay)
        {
            seed = requestedSeed;
            activeDay = Mathf.Max(1, generationDay);
            GenerateInternal(false);
        }

        public void RefreshDay(int day) => RefreshForDay(day);
        public void RefreshForDay(int day)
        {
            activeDay = Mathf.Max(1, day);
            GenerateInternal(true);
            ResolveRefreshOverlap(session != null ? session.diver : null);
            ResolveRefreshOverlap(session != null ? session.captain : null);
        }

        public float EventChance(float baseChance) => Mathf.Min(tuning != null ? tuning.deepEventChanceCap : eventChanceCap,
            baseChance + Mathf.Max(0, activeDay - 3) * (tuning != null ? tuning.deepEventChancePerDayAfterThree : eventChancePerDayAfterThree));

        public static int ComputeDaySeed(int baseSeed, int day, int stride = 104729)
            => unchecked(baseSeed + (Mathf.Max(1, day) - 1) * stride);

        public Vector2Int RollDeepEvents(System.Random random)
        {
            return new Vector2Int(random.NextDouble() < EventChance(tuning != null ? tuning.caveChance : caveBaseChance) ? 1 : 0,
                random.NextDouble() < EventChance(tuning != null ? tuning.eelCaveChance : eelBaseChance) ? 1 : 0);
        }

        void GenerateInternal(bool preserveCargo)
        {
            if(session!=null)foreach(var actor in session.Crew)if(actor!=null)actor.SetHidden(false);
            DiverRope.NotifyGeometryChanged();
            var cargo = new List<LootPickup>();
            if (preserveCargo)
                foreach (var pickup in pickups)
                    if (pickup != null && (pickup.attached || pickup.persistAcrossDays) && !pickup.consumed)
                    {
                        pickup.transform.SetParent(transform, true);
                        pickup.mazeIndex = pickup.mazeCell = -1;
                        cargo.Add(pickup);
                    }
            ResetWorld(); hideSpots.Clear();if(activeDay>forceCaveDay)forceCaveDay=-1;
            if (activeDay > forceFilterDay) forceFilterDay = -1;
            generatedSeed = ComputeDaySeed(seed, activeDay, dailySeedStride);
            var random = new System.Random(generatedSeed);
            generatedRoot = Child(transform, "Generated");
            solidRoot = Child(generatedRoot, "Solids");
            var lootRoot = Child(generatedRoot, "Resources and cargo");
            EnsurePalette();
            if (oceanArt == null || oceanArt.backdropShader == null) BuildSea();
            // Only the world edges constrain swimming; rooms have real navigable portals.
            float underwaterHeight = waterSurface - worldBounds.yMin;
            float underwaterCenterY = (waterSurface + worldBounds.yMin) * .5f;
            Solid("West seabed boundary", new Vector3(worldBounds.xMin - .5f, underwaterCenterY, 0), new Vector3(1, underwaterHeight, wallDepth), 0);
            Solid("East seabed boundary", new Vector3(worldBounds.xMax + .5f, underwaterCenterY, 0), new Vector3(1, underwaterHeight, wallDepth), 0);
            Solid("Seafloor", new Vector3(worldBounds.center.x, worldBounds.yMin - .5f, 0), new Vector3(worldBounds.width, 1, wallDepth), 0);

            MazeLayout deep = null;
            var sideRows = new int[bandRoomCounts.Length];
            for (int band = 0; band < sideRows.Length; band++)
                sideRows[band] = random.Next(band == sideRows.Length - 1 ? 1 : 0,
                    band == sideRows.Length - 1 ? Mathf.Min(3, bandRoomCounts[band].y) : bandRoomCounts[band].y);
            var areaMazes = new MazeLayout[areaCenters.Length, bandRoomCounts.Length];
            for (int area = 0; area < areaCenters.Length; area++)
                for (int band = 0; band < Mathf.Min(bandTopDepths.Length, bandRoomCounts.Length); band++)
                {
                    var size = bandRoomCounts[band];
                    int count = band == 0 ? tuning != null ? tuning.shallowLootCount : 9
                        : band == 1 ? tuning != null ? tuning.middleLootCount : 21 : tuning != null ? tuning.deepLootCount : 40;
                    var maze = AddRuin("AREA " + (area + 1) + " / DEPTH " + Mathf.RoundToInt(bandTopDepths[band]) + "m", size.x, size.y,
                        areaCenters[area], waterSurface - bandTopDepths[band], random, count, band, lootRoot,
                        band < bandCellSizes.Length ? bandCellSizes[band] : roomSize, area, sideRows[band]);
                    areaMazes[area, band] = maze;
                    if (area == areaCenters.Length - 1 && band == bandRoomCounts.Length - 1) deep = maze;
                }
            for (int area = 0; area + 1 < areaCenters.Length; area++)
                for (int band = 0; band < bandRoomCounts.Length; band++)
                    ConnectRuins(areaMazes[area, band], areaMazes[area + 1, band], "Open-water area passage");
            for (int area = 0; area < areaCenters.Length; area++)
                for (int band = 0; band + 1 < bandRoomCounts.Length; band++)
                    ConnectDepthBands(areaMazes[area, band], areaMazes[area, band + 1]);
            // Preserve the legacy event rolls/IDs. Every placed cave now guarantees an eel; the independent no-cave eel roll remains.
            Vector2Int events = RollDeepEvents(random);
            bool caveOccurs = events.x != 0, eelOccurs = events.y != 0;
            generatedCave = false;
            generatedEel = deep != null && eelOccurs;
            MazeLayout rewardMaze = deep;
            if (deep != null && generateCaves && (forceCave || forceCaveDay==activeDay || caveOccurs))
            {
                float mainRight = deep.topLeft.x + deep.columns * deep.cellSize;
                float exitY = deep.Center(deep.rightExitRow * deep.columns + deep.columns - 1).y;
                float caveLeft = mainRight + 1f;
                var cave = MazeLayout.Create("04 / BRANCH CAVE", 5, 4, 2.4f,
                    new Vector2(caveLeft, exitY + 3.6f), random, caveLoopChance, true);
                if (CanPlaceMaze(cave, out _))
                {
                    generatedCave = true;
                    layouts.Add(cave);
                    BuildMaze(cave, 3);
                    Solid("Cave connector ceiling", new Vector3(mainRight + .5f, exitY + doorWidth * .5f + wallThickness * .5f, 0), new Vector3(1 + wallThickness, wallThickness, wallDepth), 0);
                    Solid("Cave connector floor", new Vector3(mainRight + .5f, exitY - doorWidth * .5f - wallThickness * .5f, 0), new Vector3(1 + wallThickness, wallThickness, wallDepth), 0);
                    ConnectRuins(deep, cave, "Branch cave passage");
                    rewardMaze = cave;
                    generatedEel = true;
                    if (!eelOccurs) SpawnPickup(lootRoot, cave.Center(cave.rewardCell), ResourceKind.Eel, false, false, layouts.Count - 1, cave.rewardCell).name = "Guaranteed cave eel generator";
                    BuildCaveGuardPoints(cave);
                }
            }
            if (eelOccurs && rewardMaze != null)
                SpawnPickup(lootRoot, rewardMaze.Center(rewardMaze.rewardCell), ResourceKind.Eel, false, false, layouts.IndexOf(rewardMaze), rewardMaze.rewardCell).name = "Independent deep eel reward";
            foreach (var maze in layouts) BuildTrapdoors(maze, random);
            SpawnPickup(lootRoot, new Vector3(-2.8f, -6.4f, 0), ResourceKind.Wood, false, false, -1, -1);
            SpawnPickup(lootRoot, new Vector3(0, -6.8f, 0), ResourceKind.Fiber, false, false, -1, -1);
            SpawnPickup(lootRoot, new Vector3(2.8f, -6.4f, 0), ResourceKind.Metal, false, false, -1, -1);
            BuildSupplementalLife(lootRoot);
            // Equipment uses its own RNG and is appended after all legacy pickups. Existing
            // room graphs, resource IDs, fish, trapdoors and deep-event rolls remain unchanged.
            BuildFilterSalvage(lootRoot);
            // Append with separate random streams and IDs, after every legacy generation step.
            // Existing saves still resolve the original rooms, gates, resources and event rolls.
            if (extendRouteCoverage) BuildRouteCoverage(lootRoot, sideRows);
            // Features have their own per-building stream and run after every legacy generation
            // step. Changing their ranges therefore cannot reroll resources, gates or deep events.
            BuildRandomBuildingFeatures(lootRoot);
            foreach (var pickup in cargo)
            {
                pickup.transform.SetParent(generatedRoot, true); pickups.Add(pickup);
                if(pickup.persistAcrossDays && OverlapsGeneratedWall(pickup.transform.position))
                {
                    Vector3 original=pickup.transform.position, safe=original;float nearest=float.PositiveInfinity;
                    foreach(var maze in layouts)for(int cell=0;cell<maze.Count;cell++)
                    {
                        Vector3 candidate=maze.Center(cell);float distance=(candidate-original).sqrMagnitude;
                        if(distance<nearest&&!OverlapsGeneratedWall(candidate)){safe=candidate;nearest=distance;}
                    }
                    pickup.transform.position=safe;
                }
                if(pickup.lifeline!=null)pickup.lifeline.ResetRoute();
                PrepareRestoredPickup(pickup);
            }
            Physics.SyncTransforms();
            if (validateOnGeneration) ValidateGeneratedWorld(out validationReport);
            else validationReport = "Not run: use Validate generated collision and reachability for the full developer check.";
        }

        void BuildRouteCoverage(Transform lootRoot, int[] sideRows)
        {
            int bands = Mathf.Min(bandRoomCounts.Length, bandTopDepths.Length);
            if (areaCenters.Length == 0 || bands == 0) return;
            var coverageRandom = new System.Random(unchecked(generatedSeed ^ 0x6c29af1));
            var gateRandom = new System.Random(unchecked(generatedSeed ^ 0x31f49bd));
            int legacySequence = pickupSequence;
            // The extra maze indices make this ID segment unique. A filter appearing that day
            // must not shift any additional building's save IDs.
            pickupSequence = 0;
            for (int band = 0; band < bands; band++)
            {
                float cellSize = band < bandCellSizes.Length ? bandCellSizes[band] : roomSize;
                float width = bandRoomCounts[band].x * cellSize;
                float spacing = width + Mathf.Max(2.2f, coverageBuildingGap);
                var originals = new List<float>(areaCenters); originals.Sort();
                var centers = new List<float>();
                for (int i = 0; i + 1 < originals.Count; i++)
                {
                    int segments = Mathf.Max(1, Mathf.FloorToInt((originals[i + 1] - originals[i]) / spacing));
                    for (int n = 1; n < segments; n++)
                        centers.Add(Mathf.Lerp(originals[i], originals[i + 1], n / (float)segments));
                }
                float last = originals[originals.Count - 1];
                float eastCenter = worldBounds.xMax - width * .5f - Mathf.Max(2.2f, coverageBuildingGap);
                float firstEastCenter = last + spacing;
                // Reserve the existing branch-cave location even on days without a cave.
                // Its single-entry topology and short corridor therefore remain unchanged.
                if (band == bands - 1)
                    firstEastCenter = Mathf.Max(firstEastCenter, last + width * .5f + 1 + 5 * 2.4f
                        + Mathf.Max(2.2f, coverageBuildingGap) + width * .5f);
                int eastCount = firstEastCenter <= eastCenter ? 1 + Mathf.FloorToInt((eastCenter - firstEastCenter) / spacing) : 0;
                for (int n = 0; n < eastCount; n++)
                    centers.Add(firstEastCenter + n * spacing + (eastCenter - firstEastCenter - (eastCount - 1) * spacing) * .5f);
                foreach (float center in centers)
                {
                    int area = SeaAreaAt(center);
                    int count = band == 0 ? tuning != null ? tuning.shallowLootCount : 9
                        : band == 1 ? tuning != null ? tuning.middleLootCount : 21 : tuning != null ? tuning.deepLootCount : 40;
                    var size = bandRoomCounts[band];
                    var maze = AddRuin("ROUTE " + Mathf.RoundToInt(center) + "m / DEPTH " + Mathf.RoundToInt(bandTopDepths[band]) + "m",
                        size.x, size.y, center, waterSurface - bandTopDepths[band], coverageRandom, count, band, lootRoot,
                        cellSize, area, band < sideRows.Length ? sideRows[band] : -1);
                    if (maze == null) continue;
                    BuildTrapdoors(maze, gateRandom);
                    // Fish in new buildings have their own RNG, never the legacy daily fish stream.
                    for (int fishCell = 0; fishCell < maze.Count; fishCell++)
                        if (coverageRandom.NextDouble() < (tuning != null ? tuning.underwaterFishChance : .12f))
                            SpawnPickup(lootRoot, maze.Center(fishCell) + new Vector3(-.64f, .12f, -.08f), ResourceKind.Fish,
                                false, false, layouts.IndexOf(maze), fishCell);
                }
            }
            pickupSequence += legacySequence;
            // Old long crossings would pass through newly inserted buildings. Replace only those
            // crossings with local connections between each depth band's sorted neighbours.
            regionalConnections.RemoveAll(c => c.name == "Open-water area passage");
            for (int band = 0; band < bands; band++)
            {
                var row = layouts.FindAll(m => !m.entranceOnLeft && Mathf.Abs(m.topLeft.y - (waterSurface - bandTopDepths[band])) < .01f);
                row.Sort((a, b) => a.topLeft.x.CompareTo(b.topLeft.x));
                for (int i = 0; i + 1 < row.Count; i++)
                {
                    bool caveBetween = layouts.Exists(c => c.entranceOnLeft && c.topLeft.x > row[i].topLeft.x
                        && c.topLeft.x < row[i + 1].topLeft.x);
                    if (band == bands - 1 && caveBetween) ConnectAboveRuins(row[i], row[i + 1]);
                    else ConnectRuins(row[i], row[i + 1], "Adjacent route buildings");
                }
            }
        }

        int SeaAreaAt(float x)
        {
            float shallowEnd = tuning != null ? tuning.shallowBoundary : 200f * 24f / 90f;
            float deepStart = tuning != null ? tuning.deepBoundary : 200f * 56f / 90f;
            return x < shallowEnd ? 0 : x < deepStart ? 1 : 2;
        }

        void ConnectAboveRuins(MazeLayout from, MazeLayout to)
        {
            Vector3 start = from.Center(from.entranceCell), end = to.Center(to.entranceCell);
            float y = Mathf.Max(from.topLeft.y, to.topLeft.y) + validationHeight * .5f + wallThickness * .5f + .08f;
            regionalConnections.Add(new RuinConnection
            {
                name = "Open-water route above cave", fromMaze = layouts.IndexOf(from), toMaze = layouts.IndexOf(to),
                fromCell = from.entranceCell, toCell = to.entranceCell, from = start, to = end,
                waypoints = new List<Vector3> { start, new Vector3(start.x, y, 0), new Vector3(end.x, y, 0), end }
            });
        }

        MazeLayout AddRuin(string title, int columns, int rows, float centerX, float top, System.Random random, int lootCount, int tier, Transform lootRoot, float cellSize = 0, int areaIndex = 0, int sideRow = -1)
        {
            cellSize = cellSize > 0 ? cellSize : roomSize;
            var maze = MazeLayout.Create(title, columns, rows, cellSize,
                new Vector2(centerX - columns * cellSize * .5f, top), random, ruinLoopChance);
            maze.OpenSidePassages();
            maze.leftExitRow = maze.rightExitRow = sideRow >= 0 ? sideRow : rows / 2;
            // Neighbouring ruins share the height of their paired side openings. Extra sea-edge
            // openings vary with the daily seed; the deepest east opening also serves the cave.
            maze.openLeft = areaIndex > 0 || random.NextDouble() < extraSideOpeningChance;
            maze.openRight = areaIndex + 1 < areaCenters.Length || tier == bandRoomCounts.Length - 1 && generateCaves
                || random.NextDouble() < extraSideOpeningChance;
            // Keep the legacy random stream for loot/events and existing cell graphs. Exterior openings
            // use their own daily seed, so old saves keep matching resource IDs and room topology.
            var exitRandom = new System.Random(unchecked(generatedSeed ^ areaIndex * 73856093 ^ tier * 19349663 ^ 0x61a7e5));
            maze.ConfigureRuinExits(exitRandom, extraSideOpeningChance);
            // The final east face borders the branch cave. Its paired main opening is the safe lane;
            // another hole here could face a cave wall across the short connector.
            if (areaIndex == areaCenters.Length - 1 && tier == bandRoomCounts.Length - 1 && generateCaves)
                maze.extraRightExitRow = -1;
            if (!CanPlaceMaze(maze, out string placementReason))
            {
                Debug.LogWarning(title + " omitted: " + placementReason, this);
                return null;
            }
            int index = layouts.Count;
            layouts.Add(maze);
            BuildMaze(maze, tier);
            var cells = new List<int>();
            for (int i = 0; i < maze.Count; i++) cells.Add(i);
            for (int i = cells.Count - 1; i > 0; i--) { int j = random.Next(i + 1); int value = cells[i]; cells[i] = cells[j]; cells[j] = value; }
            for (int i = 0; i < Mathf.Min(lootCount, maze.Count); i++)
            {
                int cell = cells[i];
                Vector3 p = maze.Center(cell);
                if (waterSurface - p.y > maximumLootDepth) continue;
                bool cabinet = i % 7 == 4;
                var pickup = SpawnPickup(lootRoot, p, RollResource(random, areaIndex), false, cabinet, index, cell);
                if (cabinet)
                {
                    pickup.contents.Clear();
                    for (int n = 0; n < 3; n++) pickup.contents.Add(RollResource(random, areaIndex));
                    pickup.RefreshVisual();
                }
            }
            for (int i = 0; i < cratesPerZone; i++)
            {
                int cell = i == 0 ? maze.entranceCell : maze.rewardCell;
                SpawnPickup(lootRoot, maze.Center(cell) + new Vector3(.76f, -.35f, -.12f), ResourceKind.Wood, true, false, index, cell);
            }
            // More candidate points in deeper bands, all at depth >=25. Session caps live sharks at the configured maximum (default eight).
            int spawnCount = tier >= 2 ? 3 : tier == 1 ? 1 : 0;
            for (int i = 0; i < spawnCount; i++)
                sharkSpawnPoints.Add(new Vector3(centerX + (i % 2 == 0 ? -1 : 1) * (columns * cellSize * .5f + 1.4f),
                    Mathf.Min(waterSurface - 25.5f, top - (i + 1) * rows * cellSize / (spawnCount + 1)), 0));
            return maze;
        }

        void BuildMaze(MazeLayout maze, int tier)
        {
            var roomRoot = Child(solidRoot, maze.name);
            var background = Child(roomRoot, "VisualRoot");
            if (oceanArt != null && oceanArt.backdropShader != null)
                oceanArt.DressRoom(background, maze, this);
            else
            {
                var backdrop = Visual(background, "Distant silhouette", new Vector3(maze.topLeft.x + maze.columns * maze.cellSize * .5f, maze.topLeft.y - maze.rows * maze.cellSize * .5f, 2.4f), new Vector3(maze.columns * maze.cellSize, maze.rows * maze.cellSize, .25f), tier == 3 ? 0 : 1);
                if (tier == 3) backdrop.GetComponent<Renderer>().sharedMaterial = CaveMaterial(false);
            }
            for (int row = 0; row <= maze.rows; row++)
                for (int column = 0; column < maze.columns; column++)
                {
                    int below = row * maze.columns + column, above = below - maze.columns;
                    bool open = row == 0 ? maze.HasExteriorExit(column, Vector2Int.up)
                        : row == maze.rows ? maze.HasExteriorExit(above, Vector2Int.down) : maze.IsOpen(above, below);
                    WallWithPortal(roomRoot, new Vector3(maze.topLeft.x + (column + .5f) * maze.cellSize, maze.topLeft.y - row * maze.cellSize, 0), maze.cellSize, true, open, tier);
                }
            for (int column = 0; column <= maze.columns; column++)
                for (int row = 0; row < maze.rows; row++)
                {
                    int right = row * maze.columns + column, left = right - 1;
                    bool open = column == 0 ? maze.HasExteriorExit(right, Vector2Int.left)
                        : column == maze.columns ? maze.HasExteriorExit(left, Vector2Int.right) : maze.IsOpen(left, right);
                    WallWithPortal(roomRoot, new Vector3(maze.topLeft.x + column * maze.cellSize, maze.topLeft.y - (row + .5f) * maze.cellSize, 0), maze.cellSize, false, open, tier);
                }
            if (drawLabels && drawRuinTitles)
                AddLabel(background, maze.name, new Vector3(maze.topLeft.x + maze.columns * maze.cellSize * .5f, maze.topLeft.y + .45f, -.5f), .018f, new Color(.5f, .8f, .76f));
        }

        Material CaveMaterial(bool wall)
        {
            Material authored = wall ? caveWallMaterial : caveBackgroundMaterial;
            if (authored != null) return authored;
            Material material = wall ? generatedCaveWall : generatedCaveBackground;
            if (material == null)
            {
                material = new Material(Palette[0]) { name = wall ? "Cave warm sandstone / replaceable" : "Cave dark violet / replaceable" };
                if (wall) generatedCaveWall = material; else generatedCaveBackground = material;
            }
            Color color = wall ? caveWallColor : caveBackgroundColor;
            material.color = color; if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            return material;
        }

        void BuildCaveGuardPoints(MazeLayout cave)
        {
            if (tuning != null && tuning.maximumSharks <= 0) return;
            var random = new System.Random(unchecked(generatedSeed ^ 0x537ade));
            int count = tuning != null ? Mathf.Clamp(tuning.caveGuardMaximum, 1, 3) : 2;
            // Use reachable route cells near the reward, never solid walls or the reward itself.
            for (int i = 0; i < count; i++)
            {
                if (i > 0 && random.NextDouble() >= (tuning != null ? tuning.extraCaveGuardChance : .75f)) continue;
                int routeIndex = Mathf.Max(0, cave.rewardRoute.Count - 2 - i * 2);
                if (cave.rewardRoute.Count > 1) caveGuardSpawnPoints.Add(cave.Center(cave.rewardRoute[routeIndex]));
            }
        }

        void BuildSupplementalLife(Transform lootRoot)
        {
            var fishRandom = new System.Random(unchecked(generatedSeed ^ 0x1837abc));
            for (int area = 0; area < areaCenters.Length; area++)
            {
                int spawned = 0; MazeLayout fallback = null;
                for (int band = 0; band < bandRoomCounts.Length; band++)
                {
                    int index = area * bandRoomCounts.Length + band;
                    if (index >= layouts.Count || layouts[index].entranceOnLeft) continue;
                    var maze = layouts[index]; fallback = fallback ?? maze;
                    for (int cell = 0; cell < maze.Count; cell++)
                    {
                        if (fishRandom.NextDouble() >= (tuning != null ? tuning.underwaterFishChance : .12f)) continue;
                        // A small offset keeps the fish from covering the ordinary resource at cell centre.
                        Vector3 position = maze.Center(cell) + new Vector3(-.64f, .12f, -.08f);
                        SpawnPickup(lootRoot, position, ResourceKind.Fish, false, false, index, cell); spawned++;
                    }
                }
                int minimum = tuning != null ? tuning.minimumFishPerArea : 1;
                for (int n = spawned; n < minimum && fallback != null; n++)
                    SpawnPickup(lootRoot, fallback.Center(fallback.entranceCell) + new Vector3(-.64f, .12f, -.08f), ResourceKind.Fish, false, false, layouts.IndexOf(fallback), fallback.entranceCell);
            }
            var candidates = new List<Vector3>();
            Vector2 depths = tuning != null ? tuning.piranhaDepths : new Vector2(20, 35);
            foreach (var maze in layouts)
                if (!maze.entranceOnLeft)
                    for (int cell = 0; cell < maze.Count; cell++)
                    {
                        Vector3 p = maze.Center(cell); float depth = WaterSurface - p.y;
                        if (p.x >= (tuning != null ? tuning.shallowBoundary : 24) && p.x < (tuning != null ? tuning.deepBoundary : 56)
                            && depth >= depths.x && depth <= depths.y) candidates.Add(p);
                    }
            var schoolRandom = new System.Random(unchecked(generatedSeed ^ 0x49bad));
            for (int i = candidates.Count - 1; i > 0; i--) { int j = schoolRandom.Next(i + 1); var p = candidates[i]; candidates[i] = candidates[j]; candidates[j] = p; }
            int maximum = tuning != null ? Mathf.Clamp(tuning.maximumPiranhas, 0, 6) : 6;
            int schoolSize = RollPiranhaCount(schoolRandom, Mathf.Min(maximum, candidates.Count), tuning != null ? tuning.piranhaSpawnChance : .6f);
            for (int i = 0; i < schoolSize; i++) piranhaSpawnPoints.Add(candidates[i]);
        }

        public static int RollPiranhaCount(System.Random random, int maximum, float chance)
        {
            maximum = Mathf.Clamp(maximum, 0, 6);
            int count = 0;
            for (int i = 0; i < maximum; i++) if (random.NextDouble() < Mathf.Clamp01(chance)) count++;
            return maximum > 0 ? Mathf.Max(1, count) : 0;
        }

        public static bool RollFilterSalvage(int daySeed, float chance)
            => new System.Random(unchecked(daySeed ^ 0x2f71c49)).NextDouble() < Mathf.Clamp01(chance);

        void BuildFilterSalvage(Transform lootRoot)
        {
            if (session != null && session.filterInstalled) return;
            if (forceFilterDay != activeDay && !RollFilterSalvage(generatedSeed, filterSalvageChance)) return;
            int index = bandRoomCounts.Length + 1; // Second sea area, middle depth band; caves excluded.
            if (areaCenters.Length < 2 || bandRoomCounts.Length < 2 || index >= layouts.Count) return;
            var maze = layouts[index];
            if (maze.entranceOnLeft) return;
            var candidates = new List<int>();
            for (int cell = 0; cell < maze.Count; cell++)
            {
                Vector3 point = maze.Center(cell); float depth = WaterSurface - point.y;
                float west = tuning != null ? tuning.shallowBoundary : 200f * 24f / 90f;
                float east = tuning != null ? tuning.deepBoundary : 200f * 56f / 90f;
                if (point.x >= west && point.x < east && depth >= filterSalvageDepths.x && depth <= filterSalvageDepths.y)
                    candidates.Add(cell);
            }
            if (candidates.Count == 0) return;
            var random = new System.Random(unchecked(generatedSeed ^ 0x3a72613));
            int chosen = candidates[random.Next(candidates.Count)];
            var pickup = SpawnPickup(lootRoot, maze.Center(chosen) + new Vector3(.64f, -.08f, -.16f), ResourceKind.Metal, false, false, index, chosen);
            // A non-resource save ID marks this as dynamic in the existing save format, so an
            // acquired (suppressed) device can restore its consumed state without requiring a new enum.
            pickup.saveId = FilterSalvage.SaveIdPrefix + generatedSeed + ":" + activeDay;
            pickup.name = "Salvageable seawater filter";
            ConfigureFilterSalvage(pickup);
            BiasFilterGuards(maze, candidates);
        }

        void ConfigureFilterSalvage(LootPickup pickup)
        {
            var salvage = pickup.GetComponent<FilterSalvage>();
            if (salvage == null) salvage = pickup.gameObject.AddComponent<FilterSalvage>();
            filterSalvage = salvage; salvage.pickup = pickup; salvage.salvageSeconds = filterSalvageSeconds;
            pickup.generatedVisual = true; pickup.tintPlaceholderVisuals = false;
            salvage.ConfigureVisual(filterSalvageVisualPrefab, Palette[5 % Palette.Length], Palette[6 % Palette.Length]);
            pickup.RefreshVisual();
        }

        void BiasFilterGuards(MazeLayout maze, List<int> cells)
        {
            int maximum = tuning != null ? Mathf.Clamp(tuning.maximumPiranhas, 0, 6) : 6;
            int desired = Mathf.Min(maximum, Mathf.Max(piranhaSpawnPoints.Count, filterGuardMinimum));
            if (desired <= 0) { piranhaSpawnPoints.Clear(); return; }
            var nearby = new List<Vector3>();
            foreach (int cell in cells)
            {
                Vector3 point = maze.Center(cell);
                float distance = Vector3.Distance(point, filterSalvage.transform.position);
                // Guards patrol around the device rather than occupying its interaction point.
                if (distance >= 1.5f && distance <= filterGuardRadius) nearby.Add(point);
            }
            nearby.Sort((a, b) => (a - filterSalvage.transform.position).sqrMagnitude.CompareTo((b - filterSalvage.transform.position).sqrMagnitude));
            int guardCount = Mathf.Min(desired, Mathf.Min(filterGuardMinimum, nearby.Count));
            var result = new List<Vector3>();
            for (int i = 0; i < guardCount; i++) result.Add(nearby[i]);
            foreach (Vector3 point in piranhaSpawnPoints)
                if (result.Count < desired && !result.Contains(point)) result.Add(point);
            for (int i = guardCount; result.Count < desired && i < nearby.Count; i++)
                if (!result.Contains(nearby[i])) result.Add(nearby[i]);
            piranhaSpawnPoints.Clear(); piranhaSpawnPoints.AddRange(result);
        }

        public bool CanPlaceMaze(MazeLayout maze, out string reason)
        {
            float right = maze.topLeft.x + maze.columns * maze.cellSize;
            float bottom = maze.topLeft.y - maze.rows * maze.cellSize;
            if (maze.topLeft.x < worldBounds.xMin || right > worldBounds.xMax || bottom < worldBounds.yMin
                || maze.topLeft.y > waterSurface || waterSurface - bottom > maximumLootDepth + .01f)
            {
                reason = "architecture exceeds water/world/depth bounds"; return false;
            }
            float route = tuning != null ? tuning.routeDistance : 90;
            float rope = tuning != null && tuning.ropeLengths != null && tuning.ropeLengths.Length > 0
                ? Mathf.Max(tuning.ropeLengths) : 54;
            for (int cell = 0; cell < maze.Count; cell++)
            {
                Vector3 p = maze.Center(cell);
                Vector3 anchor = new Vector3(Mathf.Clamp(p.x, 0, route), BoatAnchor.y, 0);
                if (Vector3.Distance(p, anchor) + maze.cellSize * .5f > rope)
                {
                    reason = "room exceeds the final rope from every legal ship stop"; return false;
                }
            }
            reason = ""; return true;
        }

        void ConnectRuins(MazeLayout from, MazeLayout to, string title)
        {
            if (from == null || to == null || !from.openRight || !(to.openLeft || to.entranceOnLeft)) return;
            int a = from.rightExitRow * from.columns + from.columns - 1;
            int b = to.entranceOnLeft ? to.entranceCell : to.leftExitRow * to.columns;
            Vector3 start = from.Center(a), end = to.Center(b);
            regionalConnections.Add(new RuinConnection
            {
                name = title, fromMaze = layouts.IndexOf(from), toMaze = layouts.IndexOf(to),
                fromCell = a, toCell = b, from = start, to = end,
                waypoints = new List<Vector3> { start, end }
            });
        }

        void ConnectDepthBands(MazeLayout upper, MazeLayout lower)
        {
            if (upper == null || lower == null) return;
            if (upper.bottomExitColumn >= 0 && !lower.entranceOnLeft)
            {
                int upperCell = (upper.rows - 1) * upper.columns + upper.bottomExitColumn;
                Vector3 upperPoint = upper.Center(upperCell), lowerPoint = lower.Center(lower.entranceCell);
                float gapY = (upper.topLeft.y - upper.rows * upper.cellSize + lower.topLeft.y) * .5f;
                regionalConnections.Add(new RuinConnection
                {
                    name = "Top-bottom depth passage", fromMaze = layouts.IndexOf(upper), fromCell = upperCell,
                    toMaze = layouts.IndexOf(lower), toCell = lower.entranceCell, from = upperPoint, to = lowerPoint,
                    waypoints = new List<Vector3> { upperPoint, new Vector3(upperPoint.x, gapY, 0), new Vector3(lowerPoint.x, gapY, 0), lowerPoint }
                });
            }
            bool left = upper.openLeft && lower.openLeft;
            if (!left && !(upper.openRight && lower.openRight)) return;
            int a = (left ? upper.leftExitRow : upper.rightExitRow) * upper.columns + (left ? 0 : upper.columns - 1);
            int b = (left ? lower.leftExitRow : lower.rightExitRow) * lower.columns + (left ? 0 : lower.columns - 1);
            Vector3 start = upper.Center(a), end = lower.Center(b);
            float outsideX = left ? Mathf.Min(upper.topLeft.x, lower.topLeft.x) - 1.4f
                : Mathf.Max(upper.topLeft.x + upper.columns * upper.cellSize, lower.topLeft.x + lower.columns * lower.cellSize) + 1.4f;
            regionalConnections.Add(new RuinConnection
            {
                name = "Exterior depth passage", fromMaze = layouts.IndexOf(upper), fromCell = a,
                toMaze = layouts.IndexOf(lower), toCell = b, from = start, to = end,
                waypoints = new List<Vector3> { start, new Vector3(outsideX, start.y, 0), new Vector3(outsideX, end.y, 0), end }
            });
        }

        void WallWithPortal(Transform parent, Vector3 center, float length, bool horizontal, bool open, int tier)
        {
            if (!open)
            {
                Solid("Wall", center, horizontal ? new Vector3(length + wallThickness, wallThickness, wallDepth) : new Vector3(wallThickness, length + wallThickness, wallDepth), tier == 3 ? 0 : 2, parent);
                return;
            }
            float opening = Mathf.Min(doorWidth, length - .12f);
            float piece = (length - opening) * .5f;
            float offset = opening * .5f + piece * .5f;
            for (int side = -1; side <= 1; side += 2)
                Solid("Portal jamb", center + (horizontal ? Vector3.right : Vector3.up) * (offset * side),
                    horizontal ? new Vector3(piece + wallThickness, wallThickness, wallDepth) : new Vector3(wallThickness, piece + wallThickness, wallDepth), 2, parent);
        }

        void BuildSea()
        {
            var scenery = Child(generatedRoot, "Ocean VisualRoot");
            var ocean=Visual(scenery, "Deep ocean", new Vector3(worldBounds.center.x, (worldBounds.yMin + waterSurface) * .5f, 7), new Vector3(worldBounds.width, waterSurface - worldBounds.yMin, .25f), 0);
            var cycle=ocean.AddComponent<DayNightBackdrop>();cycle.session=session;cycle.nightColor=nightOceanColor;cycle.duskColor=duskOceanColor;
            Visual(scenery, "Surface", new Vector3(worldBounds.center.x, waterSurface, 3), new Vector3(worldBounds.width, .07f, .2f), 5);
            for (int i = 0; i < 14; i++)
                Visual(scenery, "Depth stratum", new Vector3(worldBounds.center.x, -5 - i * 4, 5.8f), new Vector3(worldBounds.width, .035f, .1f), 1);
        }

        void BuildTrapdoors(MazeLayout maze, System.Random random)
        {
            foreach (var edge in maze.passages)
            {
                Vector3 a = maze.Center(edge.x), b = maze.Center(edge.y), position = (a + b) * .5f;
                if (waterSurface - position.y <= trapdoorMinimumDepth || random.NextDouble() >= trapdoorChance) continue;
                bool horizontal = Mathf.Abs(a.y - b.y) > .01f;
                GameObject go = trapdoorPrefab != null ? Instantiate(trapdoorPrefab, position, Quaternion.identity, solidRoot) : new GameObject("Interactive trapdoor");
                go.transform.SetParent(solidRoot, true); go.transform.position = position; go.layer = geometryLayer;
                var gate = go.GetComponent<TrapdoorGate>();
                if (gate == null) gate = go.AddComponent<TrapdoorGate>();
                gate.world = this; gate.sideA = a; gate.sideB = b; gate.interactionDistance = trapdoorInteractionDistance;
                gate.saveId = generatedSeed + ":" + activeDay + ":gate:" + layouts.IndexOf(maze) + ":" + Mathf.Min(edge.x, edge.y) + "-" + Mathf.Max(edge.x, edge.y);
                go.name = "Trapdoor " + layouts.IndexOf(maze) + " " + Mathf.Min(edge.x, edge.y) + "-" + Mathf.Max(edge.x, edge.y);
                gate.Configure(horizontal, Mathf.Min(doorWidth, maze.cellSize - .12f) - wallThickness, wallDepth, Palette[3 % Palette.Length]);
                ConfigureInteractionLabel(gate.label, gate.transform);
                trapdoors.Add(gate);
            }
        }

        public bool TryInteractTrapdoor(CrewActor actor) => FindTrapdoor(actor)?.TryInteract(actor) == true;

        public TrapdoorGate FindTrapdoor(CrewActor actor)
        {
            TrapdoorGate nearest = null; float best = float.PositiveInfinity;
            foreach (var gate in trapdoors)
            {
                if (gate == null || !gate.InRange(actor)) continue;
                float distance = (actor.Position - gate.transform.position).sqrMagnitude;
                if (distance < best) { nearest = gate; best = distance; }
            }
            return nearest;
        }

        public void ResolveCrewRefreshOverlap(){ResolveRefreshOverlap(session!=null?session.captain:null);ResolveRefreshOverlap(session!=null?session.diver:null);}
        void ResolveRefreshOverlap(CrewActor actor)
        {
            if (actor == null || actor.onboard) return;
            Vector3 position = actor.Position;
            // A new gate may close on an existing swimmer; open it before checking wall clearance.
            foreach (var gate in trapdoors)
                if (gate != null && gate.barrier != null)
                {
                    Bounds bounds = gate.barrier.bounds;
                    bounds.Expand(new Vector3(validationRadius * 2, validationHeight, validationRadius * 2));
                    if (bounds.Contains(position)) gate.SetOpen(true);
                }
            if (!OverlapsGeneratedWall(position)) return;
            Vector3 anchor = BoatAnchor;
            float rope = session != null ? session.ropeLength : 20;
            Vector3 safe = anchor + Vector3.down * .5f;
            float nearest = float.PositiveInfinity;
            foreach (var maze in layouts)
                for (int cell = 0; cell < maze.Count; cell++)
                {
                    Vector3 candidate = maze.Center(cell);
                    if (Vector3.Distance(candidate, anchor) > rope - .1f || OverlapsGeneratedWall(candidate)) continue;
                    float distance = (candidate - position).sqrMagnitude;
                    if (distance < nearest) { safe = candidate; nearest = distance; }
                }
            actor.SetOnboard(false, safe);
        }

        bool OverlapsGeneratedWall(Vector3 position)
        {
            foreach (var wall in generatedWalls)
            {
                if (wall == null || !wall.enabled || !wall.gameObject.activeInHierarchy) continue;
                Bounds bounds = wall.bounds;
                bounds.Expand(new Vector3(validationRadius * 2, validationHeight, validationRadius * 2));
                if (bounds.Contains(position)) return true;
            }
            return false;
        }

        LootPickup SpawnPickup(Transform parent, Vector3 position, ResourceKind kind, bool crate, bool cabinet, int mazeIndex, int cell)
        {
            GameObject prefab = crate ? cratePrefab : lootPrefab;
            GameObject go = prefab != null ? Instantiate(prefab, position, Quaternion.identity, parent) : new GameObject();
            go.name = crate ? "Empty cargo crate" : cabinet ? "Salvage cabinet" : kind.ToString();
            go.transform.SetParent(parent, true); go.transform.position = position; go.layer = pickupLayer;
            var pickup = go.GetComponent<LootPickup>();
            if (pickup == null) pickup = go.AddComponent<LootPickup>();
            pickup.world = this; pickup.resource = kind; pickup.amount = 1;
            pickup.saveId = generatedSeed + ":" + activeDay + ":pickup:" + mazeIndex + ":" + cell + ":"
                + (crate ? "crate" : cabinet ? "cabinet" : "loose") + ":" + pickupSequence++;
            pickup.isCrate = crate; pickup.isCabinet = cabinet;
            pickup.contents = new List<ResourceKind>(); pickup.attached = false; pickup.consumed = false;
            pickup.capacity = tuning != null ? tuning.crateCapacity : 3;
            pickup.collectSeconds = tuning != null ? tuning.collectSeconds : .72f;
            pickup.cabinetSeconds = tuning != null ? tuning.cabinetSeconds : 1.5f;
            pickup.interactionDistance = collectionRadius; pickup.mazeIndex = mazeIndex; pickup.mazeCell = cell;
            var collider = go.GetComponent<Collider>();
            if (collider == null) { var box = go.AddComponent<BoxCollider>(); box.size = crate ? new Vector3(1.05f, .65f, .8f) : Vector3.one * .5f; collider = box; }
            collider.isTrigger = true;
            // A prefab can bring its own authored VisualRoot, animator and materials.
            if (go.transform.Find("VisualRoot") == null)
            {
                pickup.generatedVisual = true;
                pickup.visualRoot = Child(go.transform, "VisualRoot");
                var size = crate ? new Vector3(1.05f, .65f, .65f) : cabinet ? new Vector3(.75f, .9f, .6f) : Vector3.one * .38f;
                Visual(pickup.visualRoot, "Container", position, size, crate || cabinet ? 3 : 4 + (int)kind % 3);
                if (drawLabels) pickup.label = AddLabel(pickup.visualRoot, "", position + new Vector3(0, .68f, -.6f), .1f, new Color(.7f, .94f, .85f));
            }
            else pickup.visualRoot = go.transform.Find("VisualRoot");
            if (pickup.label == null && drawLabels)
            {
                pickup.label = AddLabel(go.transform, "", position + new Vector3(0, .68f, -.6f), .1f, new Color(.7f, .94f, .85f));
                pickup.generatedVisual = true;
            }
            if (resourceMaterials != null && resourceMaterials.Length > 0) pickup.resourceMaterials = resourceMaterials;
            else if (pickup.resourceMaterials == null || pickup.resourceMaterials.Length == 0)
            {
                var palette = Palette;
                pickup.resourceMaterials = new[] { palette[3 % palette.Length], palette[6 % palette.Length], palette[5 % palette.Length], palette[4 % palette.Length], palette[3 % palette.Length], palette[3 % palette.Length], palette[6 % palette.Length], palette[5 % palette.Length], palette[4 % palette.Length], palette[5 % palette.Length] };
            }
            if (!crate && !cabinet && pickup.tintPlaceholderVisuals && pickup.visualRoot != null)
            {
                Material material = pickup.resourceMaterials[(int)kind % pickup.resourceMaterials.Length];
                foreach (var renderer in pickup.visualRoot.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.GetComponent<TextMesh>() == null) renderer.sharedMaterial = material;
            }
            PrepareRoomFish(pickup);
            ConfigureInteractionLabel(pickup.label, pickup.transform);
            pickup.RefreshVisual(); pickups.Add(pickup);
            return pickup;
        }

        public bool TryCollect(CrewActor actor)
        {
            var nearest = FindNearest(actor, false);
            return nearest != null && nearest.TryCollect(actor);
        }
        public bool TryLoadCrate(CrewActor actor)
        {
            var nearest = FindNearest(actor, true);
            return nearest != null && nearest.TryLoad(actor);
        }
        public LootPickup FindNearest(CrewActor actor, bool cratesOnly) => FindNearest(actor, cratesOnly, false);

        LootPickup FindNearest(CrewActor actor, bool cratesOnly, bool swimmingFish)
        {
            if (actor == null) return null;
            LootPickup best = null; float distance = float.PositiveInfinity;
            foreach (var pickup in pickups)
            {
                if (pickup == null || !pickup.gameObject.activeInHierarchy || pickup.attached || pickup.IsSwimmingFish != swimmingFish ||
                    !pickup.InRange(actor) || (cratesOnly && !pickup.isCrate)) continue;
                if (swimmingFish && !HasFishAccess(actor, pickup)) continue;
                if (!cratesOnly && pickup.isCrate && pickup.contents.Count == 0) continue;
                float candidate = (pickup.transform.position - actor.Position).sqrMagnitude;
                if (candidate < distance) { distance = candidate; best = pickup; }
            }
            return best;
        }

        /// <summary>Applies current world presentation and interaction tuning to a restored dynamic container.</summary>
        public void PrepareRestoredPickup(LootPickup pickup)
        {
            if (pickup == null) return;
            pickup.world = this; pickup.gameObject.layer = pickupLayer;
            pickup.interactionDistance = collectionRadius;
            pickup.collectSeconds = tuning != null ? tuning.collectSeconds : .72f;
            pickup.cabinetSeconds = tuning != null ? tuning.cabinetSeconds : 1.5f;
            if (resourceMaterials != null && resourceMaterials.Length > 0) pickup.resourceMaterials = resourceMaterials;
            if (pickup.visualRoot == null) pickup.visualRoot = pickup.transform.Find("VisualRoot");
            if (pickup.label == null && drawLabels)
            {
                pickup.label = AddLabel(pickup.transform, "", pickup.transform.position + new Vector3(0, .68f, -.6f), interactionLabelSize, new Color(.7f, .94f, .85f));
                pickup.generatedVisual = true;
            }
            if (!pickup.isCrate && !pickup.isCabinet && pickup.tintPlaceholderVisuals && pickup.visualRoot != null
                && pickup.resourceMaterials != null && pickup.resourceMaterials.Length > 0)
                foreach (var renderer in pickup.visualRoot.GetComponentsInChildren<MeshRenderer>())
                    if (renderer.GetComponent<TextMesh>() == null) renderer.sharedMaterial = pickup.resourceMaterials[(int)pickup.resource % pickup.resourceMaterials.Length];
            PrepareRoomFish(pickup);
            ConfigureInteractionLabel(pickup.label, pickup.transform);
            if (!string.IsNullOrEmpty(pickup.saveId) && pickup.saveId.StartsWith(FilterSalvage.SaveIdPrefix, StringComparison.Ordinal))
            {
                EnsurePalette(); ConfigureFilterSalvage(pickup);
                if (session != null && session.filterInstalled) { pickup.consumed = true; pickup.gameObject.SetActive(false); }
            }
            pickup.RefreshVisual();
        }

        void ConfigureInteractionLabel(TextMesh label, Transform owner)
        {
            if (label == null) return;
            var view = label.GetComponent<WorldInteractionLabel>();
            if (view == null) view = label.gameObject.AddComponent<WorldInteractionLabel>();
            view.world = this; view.owner = owner; view.label = label; view.characterSize = interactionLabelSize;
            var pickup = owner.GetComponent<LootPickup>();
            view.cargo = pickup != null && pickup.isCrate && !pickup.isLostCargo ? pickup : null;
            view.RefreshNow();
        }

        public bool IsAtHideSpot(CrewActor actor) => FindHideSpot(actor) != null;
        public Transform FindHideSpot(CrewActor actor)
        {
            if (actor == null) return null;
            foreach (var hide in hideSpots)
                if (hide != null && Vector2.Distance(actor.Position, hide.transform.position) <= hide.range)
                    return hide.transform;
            return null;
        }
        public bool TryHide(CrewActor actor)
        {
            if(actor==null||actor.onboard||session==null||session.IsIncapacitated(actor))return false;
            if(session.IsHidden(actor)){actor.SetHidden(false);session.Tell(GameNames.Role(actor.role)+"离开藏匿处。");session.Changed();return true;}
            if(!IsAtHideSpot(actor))return false;
            actor.SetHidden(true);
            if (actor.role == CrewRole.Diver) session.GetComponent<TutorialProgress>()?.Observe(TutorialAction.Hide);
            session.Tell(GameNames.Role(actor.role)+"进入藏匿处。", actor);session.Changed();return true;
        }
        public Transform GetLabelFocus()=>GetLabelFocus(session!=null?session.diver:null);
        public bool IsCargoLabelVisible(LootPickup cargo)
        {
            if (session == null || session.phase != SessionPhase.Playing) return false;
            if (OnlineVoyage.InProgress)
                return cargo.InRange(OnlineVoyage.Current.LocalRole == CrewRole.Captain ? session.captain : session.diver);
            return cargo.InRange(session.captain) || cargo.InRange(session.diver);
        }
        public bool IsLabelFocus(Transform target)
        {
            if(session==null)return false;
            if (OnlineVoyage.InProgress)
                return GetLabelFocus(OnlineVoyage.Current.LocalRole == CrewRole.Captain ? session.captain : session.diver) == target;
            foreach(var actor in session.Crew)if(GetLabelFocus(actor)==target)return true;
            return false;
        }
        public Transform GetLabelFocus(CrewActor actor)
        {
            if(!Application.isPlaying||!drawLabels||actor==null||actor.onboard||actor.downed||session.IsHidden(actor))return null;
            int index=(int)actor.role;if(labelFrames[index]==Time.frameCount)return labelTargets[index];
            labelFrames[index]=Time.frameCount;
            Transform labelFocus=null;Vector3 position=actor.Position;
            float best = labelVisibleDistance * labelVisibleDistance;
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.IsSwimmingFish || pickup.attached || pickup.consumed || !pickup.gameObject.activeInHierarchy) continue;
                float distance = (pickup.transform.position - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; labelFocus = pickup.transform;
            }
            foreach (var gate in trapdoors)
            {
                if (gate == null || gate.destroyed || !gate.gameObject.activeInHierarchy) continue;
                float distance = (gate.transform.position - position).sqrMagnitude;
                if (distance >= best) continue;
                best = distance; labelFocus = gate.transform;
            }
            foreach(var hide in hideSpots)
            {
                if(hide==null)continue;float distance=(hide.transform.position-position).sqrMagnitude;
                if(distance<best){best=distance;labelFocus=hide.transform;}
            }
            labelTargets[index]=labelFocus;return labelFocus;
        }

        [ContextMenu("Validate generated collision and reachability")]
        public void ValidateConfiguredWorld() { ValidateGeneratedWorld(out validationReport); Debug.Log(validationReport, this); }

        public bool ValidateGeneratedWorld(out string report)
        {
            Physics.SyncTransforms();
            // This cache is not serialized. Rebuild it when validation runs after a domain reload.
            generatedWalls.Clear();
            validationWallBounds.Clear();
            if (solidRoot != null)
                foreach (var wall in solidRoot.GetComponentsInChildren<BoxCollider>())
                    if (!wall.isTrigger && wall.GetComponentInParent<TrapdoorGate>() == null)
                    {
                        generatedWalls.Add(wall);
                        if (!wall.enabled || !wall.gameObject.activeInHierarchy) continue;
                        Bounds bounds = wall.bounds;
                        bounds.Expand(new Vector3(validationRadius * 2, validationHeight, validationRadius * 2));
                        validationWallBounds.Add(bounds);
                    }
            int cells = 0, checkedDoors = 0;
            float anchorY = session != null ? session.BoatAnchor.y : waterSurface - .7f;
            float routeEnd = tuning != null ? tuning.routeDistance : 90;
            float maximumRope = tuning != null && tuning.ropeLengths != null && tuning.ropeLengths.Length > 0 ? Mathf.Max(tuning.ropeLengths) : 54;
            foreach (var maze in layouts)
            {
                if (maze.ReachableCellCount() != maze.Count) { report = maze.name + ": disconnected cell graph"; return false; }
                float right = maze.topLeft.x + maze.columns * maze.cellSize;
                float bottom = maze.topLeft.y - maze.rows * maze.cellSize;
                if (maze.topLeft.x < worldBounds.xMin || right > worldBounds.xMax || maze.topLeft.y > worldBounds.yMax || bottom < worldBounds.yMin
                    || waterSurface - bottom > maximumLootDepth + .01f)
                { report = maze.name + ": architecture exceeds world or maximum depth bounds"; return false; }
                cells += maze.Count;
                for (int cell = 0; cell < maze.Count; cell++)
                {
                    Vector3 center = maze.Center(cell);
                    if (!worldBounds.Contains(center)) { report = maze.name + ": room lies outside the playable world bounds"; return false; }
                    Vector3 legalAnchor = new Vector3(Mathf.Clamp(center.x, 0, routeEnd), anchorY, 0);
                    if (Vector3.Distance(center, legalAnchor) + maze.cellSize * .5f > maximumRope)
                    { report = maze.name + ": room exceeds the final rope from the nearest legal ship position"; return false; }
                }
                foreach (var passage in maze.passages)
                {
                    if (!CapsulePathClear(maze.Center(passage.x), maze.Center(passage.y)))
                    { report = maze.name + ": physical doorway obstructed " + passage; return false; }
                    checkedDoors++;
                }
                Vector3 entrance = maze.Center(maze.entranceCell);
                Vector3 outside = entrance + (maze.entranceOnLeft ? Vector3.left * maze.cellSize : Vector3.up * (maze.cellSize * .9f));
                if (!CapsulePathClear(entrance, outside)) { report = maze.name + ": entrance obstructed"; return false; }
            }
            foreach (var pickup in pickups)
            {
                if (pickup == null) continue;
                if (waterSurface - pickup.transform.position.y > maximumLootDepth + .01f) { report = "Loot below maximum reachable depth"; return false; }
                Vector3 nearestAnchor = new Vector3(Mathf.Clamp(pickup.transform.position.x, 0, routeEnd), anchorY, 0);
                if (Vector3.Distance(pickup.transform.position, nearestAnchor) > maximumRope)
                { report = "Loot beyond the final rope from the nearest legal ship position"; return false; }
                if (pickup.mazeIndex >= 0 && !CapsulePathClear(layouts[pickup.mazeIndex].Center(pickup.mazeCell), pickup.transform.position))
                { report = "Loot obstructed: " + pickup.name; return false; }
            }
            int exteriorCount = 0;
            foreach (var maze in layouts)
                foreach (var exit in maze.ExteriorExits())
                {
                    Vector3 center = maze.Center(exit.cell);
                    // Stop after the complete capsule clears this doorway. A full extra cell can
                    // overshoot the narrow open-water gap and touch the next building's ceiling.
                    float clearance = exit.direction.y != 0 ? validationHeight * .5f : validationRadius;
                    float distance = maze.cellSize * .5f + clearance + wallThickness * .5f + .05f;
                    Vector3 outside = center + new Vector3(exit.direction.x, exit.direction.y, 0) * distance;
                    if (!worldBounds.Contains(outside) || !CapsulePathClear(center, outside))
                    { report = maze.name + ": exterior portal obstructed " + exit.direction; return false; }
                    exteriorCount++;
                }
            foreach (var connection in regionalConnections)
                for (int point = 0; point < connection.waypoints.Count; point++)
                {
                    Vector3 p = connection.waypoints[point];
                    Vector3 anchor = new Vector3(Mathf.Clamp(p.x, 0, routeEnd), anchorY, 0);
                    if (!worldBounds.Contains(p) || Vector3.Distance(p, anchor) > maximumRope)
                    { report = connection.name + ": inter-region passage exceeds bounds or final rope"; return false; }
                    if (point > 0 && !CapsulePathClear(connection.waypoints[point - 1], p))
                    { report = connection.name + ": physical inter-region passage obstructed"; return false; }
                }
            foreach (var gate in trapdoors)
                if (gate != null && (Vector3.Distance(gate.sideA, gate.transform.position) > gate.interactionDistance
                    || Vector3.Distance(gate.sideB, gate.transform.position) > gate.interactionDistance))
                { report = "Trapdoor cannot be operated from both adjacent reachable rooms"; return false; }
            report = "PASS: " + layouts.Count + " connected ruins/caves, " + cells + " reachable rooms, " + checkedDoors + " physical capsule-clear doorways, " + exteriorCount + " exterior portals, " + regionalConnections.Count + " inter-region routes, " + pickups.Count + " reachable pickups. Empty crates contain no rewards.";
            return true;
        }

        bool CapsulePathClear(Vector3 from, Vector3 to)
        {
            // Bounds are read once per explicit validation, not once per sample per wall.
            // Broad-phase the swept centre line before sampling its possible intersections.
            Bounds pathBounds = new Bounds(from, Vector3.zero); pathBounds.Encapsulate(to);
            pathWallCandidates.Clear();
            foreach (var wallBounds in validationWallBounds)
                if (pathBounds.Intersects(wallBounds)) pathWallCandidates.Add(wallBounds);
            if (pathWallCandidates.Count == 0) return true;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .15f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, i / (float)steps);
                foreach (var bounds in pathWallCandidates) if (bounds.Contains(point)) return false;
            }
            return true;
        }

        public void ResetWorld()
        {
            if (generatedRoot != null) RemoveObject(generatedRoot.gameObject);
            generatedRoot = null; solidRoot = null; pickups.Clear(); layouts.Clear(); trapdoors.Clear(); sharkSpawnPoints.Clear(); piranhaSpawnPoints.Clear(); caveGuardSpawnPoints.Clear(); regionalConnections.Clear(); generatedWalls.Clear();
            validationWallBounds.Clear(); pathWallCandidates.Clear();
            filterSalvage = null;
            pickupSequence = 0;labelFrames=new[]{-1,-1};labelTargets=new Transform[2];
        }

        BoxCollider Solid(string title, Vector3 center, Vector3 size, int material, Transform parent = null)
        {
            var root = new GameObject(title); root.layer = geometryLayer;
            root.transform.SetParent(parent != null ? parent : solidRoot, true); root.transform.position = center;
            var collider = root.AddComponent<BoxCollider>(); collider.size = size;
            var view = Visual(root.transform, "VisualRoot", center, size, material);
            if (parent != null && parent.name.Contains("BRANCH CAVE"))
                view.GetComponent<Renderer>().sharedMaterial = pixelArt != null && pixelArt.caveWall != null ? pixelArt.caveWall : CaveMaterial(true);
            else if (parent != null && pixelArt != null && pixelArt.ruinWall != null)
                view.GetComponent<Renderer>().sharedMaterial = pixelArt.ruinWall;
            generatedWalls.Add(collider); return collider;
        }
        GameObject Visual(Transform parent, string title, Vector3 position, Vector3 size, int material)
        {
            if (sharedCubeMesh == null)
            {
                var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sharedCubeMesh = source.GetComponent<MeshFilter>().sharedMesh;
                RemoveObject(source);
            }
            var mesh = new GameObject(title);
            mesh.transform.SetParent(parent, true); mesh.transform.position = position; mesh.transform.localScale = size;
            mesh.AddComponent<MeshFilter>().sharedMesh = sharedCubeMesh;
            mesh.AddComponent<MeshRenderer>().sharedMaterial = Palette[material % Palette.Length];
            return mesh;
        }
        static Transform Child(Transform parent, string title)
        {
            var result = new GameObject(title).transform; result.SetParent(parent, false); return result;
        }
        static TextMesh AddLabel(Transform parent, string text, Vector3 position, float size, Color color)
        {
            var child = Child(parent, "Label"); child.position = position;
            var label = child.gameObject.AddComponent<TextMesh>(); label.text = text;
            label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
            label.fontSize = 48; label.characterSize = size; label.color = color;
            return label;
        }
        public ResourceKind RollResource(System.Random random, int areaIndex)
        {
            Vector4 weights = areaIndex >= 2 ? deepResourceWeights : areaIndex == 1 ? middleResourceWeights : shallowResourceWeights;
            float wood = Mathf.Max(0, weights.x), metal = Mathf.Max(0, weights.y), fiber = Mathf.Max(0, weights.z);
            float roll = (float)random.NextDouble() * Mathf.Max(.01f, wood + metal + fiber + Mathf.Max(0, weights.w));
            int slot = roll < wood ? 0 : roll < wood + metal ? 1 : roll < wood + metal + fiber ? 2 : 3;
            var defaults = new[] { ResourceKind.Fiber, ResourceKind.Wood, ResourceKind.Metal, ResourceKind.Fuel };
            return weightedResourceKinds != null && slot < weightedResourceKinds.Length ? weightedResourceKinds[slot] : defaults[slot];
        }
        Material[] Palette => placeholderMaterials != null && placeholderMaterials.Length > 0 ? placeholderMaterials : fallbackMaterials;
        void EnsurePalette()
        {
            if (placeholderMaterials != null && placeholderMaterials.Length > 0 || fallbackMaterials != null) return;
            var colors = new[] { new Color(.075f, .30f, .40f), new Color(.12f, .40f, .47f), new Color(.43f, .66f, .64f), new Color(.67f, .43f, .23f), new Color(.99f, .77f, .32f), new Color(.22f, .84f, .74f), new Color(.76f, .88f, .85f) };
            fallbackMaterials = new Material[colors.Length];
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Standard");
            for (int i = 0; i < colors.Length; i++)
            {
                var material = new Material(shader) { name = "LifeLine world " + i };
                material.color = colors[i]; if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colors[i]);
                fallbackMaterials[i] = material;
            }
        }
        void OnDestroy()
        {
            if (generatedCaveWall != null) RemoveObject(generatedCaveWall);
            if (generatedCaveBackground != null) RemoveObject(generatedCaveBackground);
            if (fallbackMaterials != null) foreach (var material in fallbackMaterials) if (material != null) RemoveObject(material);
        }
        static void RemoveObject(UnityEngine.Object target)
        {
            if (target is GameObject go) go.SetActive(false);
            if (target is Collider collider) collider.enabled = false;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
