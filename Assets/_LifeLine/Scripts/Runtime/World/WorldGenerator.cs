using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Seeded ruins built from connected cell graphs and matching 3D collision walls.</summary>
    public class WorldGenerator : MonoBehaviour
    {
        public GameSession session;
        public GameTuning tuning;
        public int seed = 2718;
        public int activeDay = 1, generatedSeed;
        public bool generatedCave, generatedEel;
        public Material[] placeholderMaterials;
        [Tooltip("Optional materials indexed by ResourceKind. Prefab palettes are preserved if unset.")]
        public Material[] resourceMaterials;
        public GameObject lootPrefab, cratePrefab, sharkPrefab, shadowPrefab, trapdoorPrefab;
        public List<LootPickup> pickups = new List<LootPickup>();
        public Transform generatedRoot;
        public Transform solidRoot;
        public List<Vector3> sharkSpawnPoints = new List<Vector3>();
        public List<MazeLayout> layouts = new List<MazeLayout>();
        public List<TrapdoorGate> trapdoors = new List<TrapdoorGate>();
        public List<UnderwaterHideSpot> hideSpots = new List<UnderwaterHideSpot>();
        public List<RuinConnection> regionalConnections = new List<RuinConnection>();

        [Header("Geometry (metres, XY physics)")]
        public Rect worldBounds = new Rect(-22, -55, 122, 65);
        public float waterSurface = -1.5f;
        [Min(2.6f)] public float roomSize = 3f;
        [Min(1.9f)] public float doorWidth = 2.2f;
        [Min(.1f)] public float wallThickness = .24f;
        [Min(.5f)] public float wallDepth = 1.8f;
        [Range(0, 31)] public int geometryLayer;
        [Range(0, 31)] public int pickupLayer;
        [Range(0, 1)] public float ruinLoopChance = .2f;
        [Range(0, 1)] public float caveLoopChance = .04f;
        [Tooltip("Extra exterior side openings. Paired regional route openings are always kept traversable.")]
        [Range(0, 1)] public float extraSideOpeningChance = .55f;
        public bool generateCaves = true;
        public bool forceCave;
        public bool drawLabels = true;
        public bool drawRuinTitles;
        [Min(.001f)] public float interactionLabelSize = .06f;
        [Min(.1f)] public float labelVisibleDistance = 2.4f;
        [Min(.1f)] public float collectionRadius = 1.65f;
        [Min(0)] public int cratesPerZone = 2;
        [Min(.1f)] public float maximumLootDepth = 50f;
        [Tooltip("Horizontal sea areas; all contain the complete depth sequence.")]
        public float[] areaCenters = { 0, 36, 72 };
        public float[] bandTopDepths = { 10, 21.5f, 36 };
        public Vector2Int[] bandRoomCounts = { new Vector2Int(5, 3), new Vector2Int(7, 4), new Vector2Int(9, 5) };
        public float[] bandCellSizes = { 3, 3, 2.8f };
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
        [TextArea] public string validationReport;

        public float WaterSurface => waterSurface;
        public Vector3 BoatAnchor => session != null ? session.BoatAnchor : new Vector3(0, waterSurface - .7f, 0);
        public IReadOnlyList<MazeLayout> Layouts => layouts;
        readonly List<BoxCollider> generatedWalls = new List<BoxCollider>();
        int pickupSequence;
        int labelFrame = -1;
        Transform labelFocus;
        Material[] fallbackMaterials;

        [ContextMenu("Generate configured seed")]
        public void GenerateConfiguredSeed() => Generate(seed);

        public void Generate(int requestedSeed)
        {
            seed = requestedSeed;
            activeDay = session != null ? Mathf.Max(1, session.day) : Mathf.Max(1, activeDay);
            GenerateInternal(false);
        }

        public void RefreshDay(int day) => RefreshForDay(day);
        public void RefreshForDay(int day)
        {
            activeDay = Mathf.Max(1, day);
            GenerateInternal(true);
            ResolveRefreshOverlap(session != null ? session.diver : null);
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
            DiverRope.NotifyGeometryChanged();
            var cargo = new List<LootPickup>();
            if (preserveCargo)
                foreach (var pickup in pickups)
                    if (pickup != null && pickup.attached && !pickup.consumed)
                    {
                        pickup.transform.SetParent(transform, true);
                        pickup.mazeIndex = pickup.mazeCell = -1;
                        cargo.Add(pickup);
                    }
            ResetWorld(); hideSpots.Clear();
            generatedSeed = ComputeDaySeed(seed, activeDay, dailySeedStride);
            var random = new System.Random(generatedSeed);
            generatedRoot = Child(transform, "Generated");
            solidRoot = Child(generatedRoot, "Solids");
            var lootRoot = Child(generatedRoot, "Resources and cargo");
            EnsurePalette();
            BuildSea();
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
            foreach(var maze in layouts)
            {
                int cell=maze.entranceCell==0?Mathf.Min(maze.Count-1,maze.columns+1):0;
                var obj=new GameObject("Ruins hiding spot");obj.transform.SetParent(generatedRoot,false);obj.transform.position=maze.Center(cell);
                var hide=obj.AddComponent<UnderwaterHideSpot>();hide.world=this;hideSpots.Add(hide);
            }
            // Independent rolls: an eel can occur in the deepest ruin even when no cave occurs.
            Vector2Int events = RollDeepEvents(random);
            bool caveOccurs = events.x != 0, eelOccurs = events.y != 0;
            generatedCave = false;
            generatedEel = deep != null && eelOccurs;
            MazeLayout rewardMaze = deep;
            if (deep != null && generateCaves && (forceCave || caveOccurs))
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
                    if (!eelOccurs) SpawnPickup(lootRoot, cave.Center(cave.rewardCell), ResourceKind.Fuel, false, false, layouts.Count - 1, cave.rewardCell).name = "Reachable cave reward";
                    foreach (int cell in cave.rewardRoute)
                        Visual(Child(generatedRoot, "Cave guide marker"), "VisualRoot", cave.Center(cell) + new Vector3(0, -.7f, .7f), new Vector3(.12f, .12f, .12f), 5);
                }
            }
            if (eelOccurs && rewardMaze != null)
                SpawnPickup(lootRoot, rewardMaze.Center(rewardMaze.rewardCell), ResourceKind.Eel, false, false, layouts.IndexOf(rewardMaze), rewardMaze.rewardCell).name = "Independent deep eel reward";
            foreach (var maze in layouts) BuildTrapdoors(maze, random);
            SpawnPickup(lootRoot, new Vector3(-2.8f, -6.4f, 0), ResourceKind.Wood, false, false, -1, -1);
            SpawnPickup(lootRoot, new Vector3(0, -6.8f, 0), ResourceKind.Fiber, false, false, -1, -1);
            SpawnPickup(lootRoot, new Vector3(2.8f, -6.4f, 0), ResourceKind.Metal, false, false, -1, -1);
            foreach (var pickup in cargo) { pickup.transform.SetParent(generatedRoot, true); pickups.Add(pickup); }
            Physics.SyncTransforms();
            ValidateGeneratedWorld(out validationReport);
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
            // More candidate points in deeper bands, all at depth >=25. Session caps live sharks at six.
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
            Visual(background, "Distant silhouette", new Vector3(maze.topLeft.x + maze.columns * maze.cellSize * .5f, maze.topLeft.y - maze.rows * maze.cellSize * .5f, 2.4f), new Vector3(maze.columns * maze.cellSize, maze.rows * maze.cellSize, .25f), tier == 3 ? 0 : 1);
            for (int row = 0; row <= maze.rows; row++)
                for (int column = 0; column < maze.columns; column++)
                {
                    int below = row * maze.columns + column, above = below - maze.columns;
                    bool open = row == 0 ? !maze.entranceOnLeft && column == maze.entranceCell % maze.columns
                        : row < maze.rows && maze.IsOpen(above, below);
                    WallWithPortal(roomRoot, new Vector3(maze.topLeft.x + (column + .5f) * maze.cellSize, maze.topLeft.y - row * maze.cellSize, 0), maze.cellSize, true, open, tier);
                }
            for (int column = 0; column <= maze.columns; column++)
                for (int row = 0; row < maze.rows; row++)
                {
                    int right = row * maze.columns + column, left = right - 1;
                    bool open = column == 0 ? (maze.entranceOnLeft && row == maze.entranceCell / maze.columns) || (maze.openLeft && row == maze.leftExitRow)
                        : column == maze.columns ? maze.openRight && row == maze.rightExitRow : maze.IsOpen(left, right);
                    WallWithPortal(roomRoot, new Vector3(maze.topLeft.x + column * maze.cellSize, maze.topLeft.y - (row + .5f) * maze.cellSize, 0), maze.cellSize, false, open, tier);
                }
            if (drawLabels && drawRuinTitles)
                AddLabel(background, maze.name, new Vector3(maze.topLeft.x + maze.columns * maze.cellSize * .5f, maze.topLeft.y + .45f, -.5f), .018f, new Color(.5f, .8f, .76f));
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
            // Small paired stones communicate broken openings without filling the swimming lane.
            foreach (Vector3 point in new[] { from.Center(a), to.Center(b) })
                Visual(generatedRoot, "Passage guide", point + new Vector3(0, -.65f, .95f), new Vector3(.2f, .1f, .15f), 5);
        }

        void ConnectDepthBands(MazeLayout upper, MazeLayout lower)
        {
            if (upper == null || lower == null) return;
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
            Visual(scenery, "Deep ocean", new Vector3(worldBounds.center.x, (worldBounds.yMin + waterSurface) * .5f, 7), new Vector3(worldBounds.width, waterSurface - worldBounds.yMin, .25f), 0);
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

        public bool TryInteractTrapdoor(CrewActor actor)
        {
            TrapdoorGate nearest = null; float best = float.PositiveInfinity;
            foreach (var gate in trapdoors)
            {
                if (gate == null || !gate.InRange(actor)) continue;
                float distance = (actor.Position - gate.transform.position).sqrMagnitude;
                if (distance < best) { nearest = gate; best = distance; }
            }
            return nearest != null && nearest.TryInteract(actor);
        }

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
                if (crate)
                {
                    pickup.cargoSlotRenderers = new Renderer[3];
                    for (int i = 0; i < 3; i++)
                        pickup.cargoSlotRenderers[i] = Visual(pickup.visualRoot, "Cargo slot " + (i + 1), position + new Vector3((i - 1) * .3f, .08f, -.4f), new Vector3(.21f, .25f, .2f), 4).GetComponent<Renderer>();
                }
                if (drawLabels) pickup.label = AddLabel(pickup.visualRoot, "", position + new Vector3(0, .68f, -.6f), .1f, new Color(.7f, .94f, .85f));
            }
            else pickup.visualRoot = go.transform.Find("VisualRoot");
            if (crate && (pickup.cargoSlotRenderers == null || pickup.cargoSlotRenderers.Length == 0))
            {
                // Optional indicators are separate from an artist's replaceable mesh hierarchy.
                var indicators = Child(go.transform, "Cargo indicators");
                pickup.cargoSlotRenderers = new Renderer[3];
                for (int i = 0; i < 3; i++)
                    pickup.cargoSlotRenderers[i] = Visual(indicators, "Cargo slot " + (i + 1), position + new Vector3((i - 1) * .3f, .08f, -.4f), new Vector3(.21f, .25f, .2f), 4).GetComponent<Renderer>();
            }
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
                foreach (var renderer in pickup.visualRoot.GetComponentsInChildren<Renderer>())
                    if (!(renderer is MeshRenderer meshRenderer) || meshRenderer.GetComponent<TextMesh>() == null) renderer.sharedMaterial = material;
            }
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
        LootPickup FindNearest(CrewActor actor, bool cratesOnly)
        {
            if (actor == null) return null;
            LootPickup best = null; float distance = float.PositiveInfinity;
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.attached || !pickup.InRange(actor) || (cratesOnly && !pickup.isCrate)) continue;
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
                foreach (var renderer in pickup.visualRoot.GetComponentsInChildren<Renderer>())
                    if (renderer.GetComponent<TextMesh>() == null) renderer.sharedMaterial = pickup.resourceMaterials[(int)pickup.resource % pickup.resourceMaterials.Length];
            ConfigureInteractionLabel(pickup.label, pickup.transform);
            pickup.RefreshVisual();
        }

        void ConfigureInteractionLabel(TextMesh label, Transform owner)
        {
            if (label == null) return;
            var view = label.GetComponent<WorldInteractionLabel>();
            if (view == null) view = label.gameObject.AddComponent<WorldInteractionLabel>();
            view.world = this; view.owner = owner; view.label = label; view.characterSize = interactionLabelSize;
            view.RefreshNow();
        }

        public bool IsAtHideSpot(CrewActor actor)
        {foreach(var hide in hideSpots)if(hide!=null&&Vector2.Distance(actor.Position,hide.transform.position)<=hide.range)return true;return false;}
        public bool TryHide(CrewActor actor)
        {
            if(actor==null||actor.onboard||session==null||!IsAtHideSpot(actor))return false;
            session.diverHidden=!session.diverHidden;session.Tell(session.diverHidden?"潜水员藏身于废墟阴影。":"潜水员离开藏身处。");return true;
        }
        public Transform GetLabelFocus()
        {
            if (!Application.isPlaying || !drawLabels || session == null || session.diver == null
                || session.diver.onboard || session.diver.downed) return null;
            if (labelFrame == Time.frameCount) return labelFocus;
            labelFrame = Time.frameCount; labelFocus = null;
            Vector3 position = session.diver.Position;
            float best = labelVisibleDistance * labelVisibleDistance;
            foreach (var pickup in pickups)
            {
                if (pickup == null || pickup.attached || pickup.consumed || !pickup.gameObject.activeInHierarchy) continue;
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
            return labelFocus;
        }

        [ContextMenu("Validate generated collision and reachability")]
        public void ValidateConfiguredWorld() { ValidateGeneratedWorld(out validationReport); Debug.Log(validationReport, this); }

        public bool ValidateGeneratedWorld(out string report)
        {
            Physics.SyncTransforms();
            // This cache is not serialized. Rebuild it when validation runs after a domain reload.
            generatedWalls.Clear();
            if (solidRoot != null)
                foreach (var wall in solidRoot.GetComponentsInChildren<BoxCollider>())
                    if (!wall.isTrigger && wall.GetComponentInParent<TrapdoorGate>() == null) generatedWalls.Add(wall);
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
            foreach (var maze in layouts)
            {
                if (maze.openLeft)
                {
                    Vector3 center = maze.Center(maze.leftExitRow * maze.columns);
                    if (!CapsulePathClear(center, center + Vector3.left * maze.cellSize))
                    { report = maze.name + ": left exterior portal obstructed"; return false; }
                }
                if (maze.openRight)
                {
                    Vector3 center = maze.Center(maze.rightExitRow * maze.columns + maze.columns - 1);
                    if (!CapsulePathClear(center, center + Vector3.right * maze.cellSize))
                    { report = maze.name + ": right exterior portal obstructed"; return false; }
                }
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
            report = "PASS: " + layouts.Count + " connected ruins/caves, " + cells + " reachable rooms, " + checkedDoors + " physical capsule-clear doorways, " + regionalConnections.Count + " inter-region routes, " + pickups.Count + " reachable pickups. Empty crates contain no rewards.";
            return true;
        }

        bool CapsulePathClear(Vector3 from, Vector3 to)
        {
            // Test our actual 3D colliders, not a parallel visual-only map or trigger pickups.
            float stem = Mathf.Max(0, validationHeight * .5f - validationRadius);
            int steps = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(from, to) / .15f));
            for (int i = 0; i <= steps; i++)
            {
                Vector3 point = Vector3.Lerp(from, to, i / (float)steps);
                foreach (var wall in generatedWalls)
                {
                    if (wall == null || wall.isTrigger || !wall.enabled || !wall.gameObject.activeInHierarchy) continue;
                    // Axis-aligned generated walls; the validation capsule is vertical like the crew.
                    Bounds bounds = wall.bounds;
                    bounds.Expand(new Vector3(validationRadius * 2, (stem + validationRadius) * 2, validationRadius * 2));
                    if (bounds.Contains(point)) return false;
                }
            }
            return true;
        }

        public void ResetWorld()
        {
            if (generatedRoot != null) RemoveObject(generatedRoot.gameObject);
            generatedRoot = null; solidRoot = null; pickups.Clear(); layouts.Clear(); trapdoors.Clear(); sharkSpawnPoints.Clear(); regionalConnections.Clear(); generatedWalls.Clear();
            pickupSequence = 0; labelFrame = -1; labelFocus = null;
        }

        BoxCollider Solid(string title, Vector3 center, Vector3 size, int material, Transform parent = null)
        {
            var root = new GameObject(title); root.layer = geometryLayer;
            root.transform.SetParent(parent != null ? parent : solidRoot, true); root.transform.position = center;
            var collider = root.AddComponent<BoxCollider>(); collider.size = size;
            Visual(root.transform, "VisualRoot", center, size, material);
            generatedWalls.Add(collider); return collider;
        }
        GameObject Visual(Transform parent, string title, Vector3 position, Vector3 size, int material)
        {
            var mesh = GameObject.CreatePrimitive(PrimitiveType.Cube); mesh.name = title;
            mesh.transform.SetParent(parent, true); mesh.transform.position = position; mesh.transform.localScale = size;
            var collider = mesh.GetComponent<Collider>(); if (collider != null) RemoveObject(collider);
            mesh.GetComponent<Renderer>().sharedMaterial = Palette[material % Palette.Length];
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
            if (fallbackMaterials == null) return;
            foreach (var material in fallbackMaterials) if (material != null) RemoveObject(material);
        }
        static void RemoveObject(UnityEngine.Object target)
        {
            if (target is GameObject go) go.SetActive(false);
            if (target is Collider collider) collider.enabled = false;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
