using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Lifeline
{
    /// <summary>Explicit EditMode checks on temporary generated geometry. Root invokes through its validation menu.</summary>
    public static class WorldRulesValidation
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run world checks outside Play mode.");
            GameSession previousCurrent = GameSession.Current;
            var root = new GameObject("World rules checks (temporary)");
            var tuning = ScriptableObject.CreateInstance<GameTuning>();
            var log = new StringBuilder();
            try
            {
                var world = root.AddComponent<WorldGenerator>();
                world.tuning = tuning; world.forceCave = true; world.trapdoorChance = 1;
                world.geometryLayer = FindFreeCollisionLayer(); world.drawLabels = false;
                // Unique guaranteed drops catch incorrectly using depth-band weights instead of sea-area weights.
                world.shallowResourceWeights = new Vector4(1, 0, 0, 0);
                world.middleResourceWeights = new Vector4(0, 1, 0, 0);
                world.deepResourceWeights = new Vector4(0, 0, 1, 0);
                world.Generate(8417);
                Require(world.layouts.Count == 27, "route coverage expands the original nine ruins to twenty-six, plus forced cave");
                foreach (var maze in world.layouts)
                {
                    int roots = 0;
                    for (int i = 0; i < world.solidRoot.childCount; i++) if (world.solidRoot.GetChild(i).name == maze.name) roots++;
                    Require(roots == 1, "one physical maze root, including names containing a slash");
                }
                Require(world.ValidateGeneratedWorld(out string report), report);
                log.AppendLine(report);
                int[] expectedCounts = { tuning.shallowLootCount, tuning.middleLootCount, tuning.deepLootCount };
                var actualCounts = new int[9];
                foreach (var pickup in world.pickups)
                {
                    if (pickup.mazeIndex < 0 || pickup.mazeIndex >= 9 || pickup.isCrate || pickup.resource == ResourceKind.Fish || pickup.GetComponent<FilterSalvage>() != null) continue;
                    actualCounts[pickup.mazeIndex]++;
                    ResourceKind expected = world.weightedResourceKinds[pickup.mazeIndex / 3];
                    if (pickup.isCabinet) foreach (var item in pickup.contents) Require(item == expected, "cabinet uses sea-area weights at every depth");
                    else Require(pickup.resource == expected, "loose loot uses sea-area weights at every depth");
                }
                for (int area = 0; area < 3; area++)
                {
                    for (int band = 0; band < 3; band++) Require(actualCounts[area * 3 + band] == expectedCounts[band], "depth-band resource count");
                    Require(actualCounts[area * 3] < actualCounts[area * 3 + 1] && actualCounts[area * 3 + 1] < actualCounts[area * 3 + 2], "resources increase with depth");
                    float shallowDensity = actualCounts[area * 3] / (float)world.layouts[area * 3].Count;
                    float middleDensity = actualCounts[area * 3 + 1] / (float)world.layouts[area * 3 + 1].Count;
                    float deepDensity = actualCounts[area * 3 + 2] / (float)world.layouts[area * 3 + 2].Count;
                    Require(shallowDensity < middleDensity && middleDensity < deepDensity, "occupied-room ratio increases with depth");
                }
                foreach (var point in world.sharkSpawnPoints) Require(world.WaterSurface - point.y >= tuning.sharkMinimumDepth, "sharks have no shallow spawn point");
                log.AppendLine("PASS: sea-area resource selection at all depths, increasing depth counts, sharks at depth>=25m.");
                CheckDoors(world, log);
                CheckProbabilities(world, log);

                LootPickup cargo = world.pickups.Find(p => p.isCrate);
                Require(cargo != null && cargo.contents.Count == 0, "crates start empty");
                cargo.contents.Add(ResourceKind.Fiber); cargo.contents.Add(ResourceKind.Metal); cargo.attached = true;
                string firstMap = Fingerprint(world); int firstSeed = world.generatedSeed;
                var sessionObject = new GameObject("Refresh actor session"); sessionObject.transform.SetParent(root.transform);
                var session = sessionObject.AddComponent<GameSession>();
                session.autoStart = false;
                var actorObject = new GameObject("Refresh swimmer"); actorObject.transform.SetParent(root.transform);
                var actor = actorObject.AddComponent<CrewActor>();
                actor.enabled = false;
                var capsule = actorObject.GetComponent<CapsuleCollider>();
                capsule.height = 1.8f; capsule.radius = .32f; capsule.direction = 1;
                actor.session = session; actor.body = actorObject.GetComponent<Rigidbody>();
                session.tuning = tuning; session.diver = actor; session.ropeLength = 54; world.session = session;
                actor.oxygen = 17; actor.backpack.Add(ResourceKind.Fuel); actor.downed = true;
                Vector3 oldPosition = new Vector3(world.layouts[0].topLeft.x, world.layouts[0].Center(0).y, 0);
                actor.SetOnboard(false, oldPosition);
                world.RefreshForDay(2);
                Require(world.generatedSeed != firstSeed && Fingerprint(world) != firstMap, "daily map changes");
                Require(world.pickups.Contains(cargo) && cargo.attached && !cargo.consumed
                    && cargo.contents.Count == 2 && cargo.contents[0] == ResourceKind.Fiber && cargo.contents[1] == ResourceKind.Metal, "refresh preserves exact attached cargo");
                Require(!actor.onboard && actor.downed && actor.oxygen == 17 && actor.backpack.Count == 1, "refresh preserves swimmer status/inventory without boarding");
                Require(Vector3.Distance(actor.Position, session.BoatAnchor) <= session.ropeLength, "relocation stays inside current rope");
                Physics.SyncTransforms();
                Require(!CapsuleOverlaps(actor.Position, world.geometryLayer), "refreshed swimmer is clear of actual solid colliders");
                string dayTwo = Fingerprint(world);
                world.RefreshDay(2);
                Require(Fingerprint(world) == dayTwo, "same day/base seed reproduce geometry");
                log.AppendLine("PASS: daily geometry changes/repeats deterministically, attached cargo survives, overlapping downed swimmer relocates safely without boarding or status loss.");
                return log.ToString();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(tuning);
                GameSession.RestoreCurrentAfterValidation(previousCurrent);
            }
        }

        static void CheckDoors(WorldGenerator world, StringBuilder log)
        {
            int horizontal = 0, vertical = 0;
            foreach (var gate in world.trapdoors)
            {
                Require(world.WaterSurface - gate.transform.position.y > 30, "trapdoors only below30m");
                if (gate.horizontal) horizontal++; else vertical++;
                gate.SetOpen(false); Physics.SyncTransforms();
                Require(!CapsulePathClear(gate.sideA, gate.sideB, world.geometryLayer), "closed gate physically blocks passage");
                gate.SetOpen(true); Physics.SyncTransforms();
                Require(CapsulePathClear(gate.sideA, gate.sideB, world.geometryLayer), "opened gate permits capsule passage");
            }
            Require(horizontal > 0 && vertical > 0, "horizontal and vertical trapdoors generated");
            log.AppendLine("PASS: " + horizontal + " horizontal / " + vertical + " vertical gates block when closed and clear when open (Unity 3D capsule casts).");
        }

        static void CheckProbabilities(WorldGenerator world, StringBuilder log)
        {
            world.shallowResourceWeights = new Vector4(35, 35, 20, 10);
            world.middleResourceWeights = new Vector4(30, 30, 25, 15);
            world.deepResourceWeights = new Vector4(25, 25, 30, 20);
            float[,] expected = { { .35f, .35f, .20f, .10f }, { .30f, .30f, .25f, .15f }, { .25f, .25f, .30f, .20f } };
            var random = new System.Random(44017);
            for (int area = 0; area < 3; area++)
            {
                var counts = new int[4];
                for (int n = 0; n < 20000; n++) counts[Array.IndexOf(world.weightedResourceKinds, world.RollResource(random, area))]++;
                for (int slot = 0; slot < 4; slot++) Require(Mathf.Abs(counts[slot] / 20000f - expected[area, slot]) < .015f, "sea-area weighted frequency");
            }
            int[] days = { 1, 3, 4, 8, 100 }; float[] chances = { .05f, .05f, .07f, .15f, .15f };
            for (int i = 0; i < days.Length; i++)
            {
                world.activeDay = days[i];
                Require(Mathf.Abs(world.EventChance(world.tuning.caveChance) - chances[i]) < .0001f, "cave day probability");
                Require(Mathf.Abs(world.EventChance(world.tuning.eelCaveChance) - chances[i]) < .0001f, "eel day probability");
            }
            world.activeDay = 1;
            int caveOnly = 0, eelOnly = 0, both = 0;
            for (int n = 0; n < 20000; n++)
            {
                Vector2Int events = world.RollDeepEvents(random);
                if (events.x != 0 && events.y == 0) caveOnly++;
                if (events.x == 0 && events.y != 0) eelOnly++;
                if (events.x != 0 && events.y != 0) both++;
            }
            Require(caveOnly > 700 && eelOnly > 700 && both > 20 && both < 90, "independent cave/eel, including eel without cave");
            log.AppendLine("PASS: sea-area drop weights; independent cave/eel5% at day1–3, +2 points/day afterwards, capped at15%.");
        }

        static bool CapsulePathClear(Vector3 a, Vector3 b, int layer)
        {
            Vector3 stem = Vector3.up * (.9f - .32f), delta = b - a;
            return !Physics.CapsuleCast(a + stem, a - stem, .32f, delta.normalized, delta.magnitude, 1 << layer, QueryTriggerInteraction.Ignore);
        }
        static bool CapsuleOverlaps(Vector3 point, int layer)
        {
            Vector3 stem = Vector3.up * (.9f - .32f);
            return Physics.CheckCapsule(point + stem, point - stem, .32f, 1 << layer, QueryTriggerInteraction.Ignore);
        }
        static int FindFreeCollisionLayer()
        {
            var occupied = new HashSet<int>();
            foreach (var collider in UnityEngine.Object.FindObjectsByType<Collider>()) occupied.Add(collider.gameObject.layer);
            for (int layer = 31; layer >= 12; layer--) if (!occupied.Contains(layer)) return layer;
            throw new InvalidOperationException("World checks require an unused collision layer between12 and31.");
        }
        static string Fingerprint(WorldGenerator world)
        {
            var text = new StringBuilder();
            foreach (var maze in world.layouts) foreach (var edge in maze.passages) text.Append(edge.x).Append(':').Append(edge.y).Append(',');
            return text.ToString();
        }
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("World check failed: " + message);
        }
    }
}
