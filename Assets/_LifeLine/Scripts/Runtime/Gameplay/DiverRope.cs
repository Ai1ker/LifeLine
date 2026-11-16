using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline
{
    /// <summary>
    /// An XY contact rope. The stored polyline is also the route used by the winch:
    /// every segment is swept with the swimmer's capsule, rather than ray-tested as a thin line.
    /// Contacts remain on their original side of an obstacle until a collision-free shortcut exists.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DiverRope : MonoBehaviour
    {
        public CrewActor actor;
        public Transform ignoredShipRoot;
        public LayerMask collisionMask = 1 << 8;
        [Header("Capsule clearance / metres")]
        public bool deriveClearanceFromActor = true;
        [Min(.01f)] public float clearanceRadius = .32f;
        [Min(.02f)] public float clearanceHeight = 1.6f;
        [Min(.001f)] public float collisionSkin = .035f;
        [Min(.005f)] public float cornerMargin = .065f;
        [Min(.001f)] public float contactArrivalDistance = .025f;
        [Header("Bounded obstruction search")]
        [Min(16)] public int maximumSearchNodes = 512;
        [Min(100)] public int maximumSearchCasts = 18000;
        [Min(8)] public int maximumContacts = 128;
        [Min(2)] public int maximumPullSegments = 128;
        [Tooltip("Search accepts a detour this many times longer than the straight segment.")]
        [Min(1)] public float maximumDetourRatio = 6f;
        [Min(1)] public float minimumSearchReach = 24f;
        [SerializeField] List<Vector3> points = new List<Vector3>();
        public IReadOnlyList<Vector3> Points => points;
        public float PathLength { get; private set; }
        public bool IsBlocked { get; private set; }
        public int ContactCount => Mathf.Max(0, points.Count - 2);
        public Vector3 LastPullDirection => points.Count >= 2 ? (points[points.Count - 2] - points[points.Count - 1]).normalized : Vector3.zero;

        readonly RaycastHit[] hits = new RaycastHit[128];
        readonly Collider[] overlaps = new Collider[128];
        Vector3 centerOffset;
        float planeZ;
        int searchCasts;

        sealed class SearchNode
        {
            public Vector3 point;
            public float distance = float.PositiveInfinity;
            public int parent = -1;
            public bool closed;
            public SearchNode(Vector3 position) { point = position; }
        }

        public void Configure(CrewActor swimmer, Transform shipRoot = null)
        {
            actor = swimmer; ignoredShipRoot = shipRoot;
            if (actor != null && deriveClearanceFromActor)
            {
                var capsule = actor.GetComponent<CapsuleCollider>();
                if (capsule != null)
                {
                    Vector3 scale = capsule.transform.lossyScale;
                    clearanceRadius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
                    clearanceHeight = Mathf.Max(clearanceRadius * 2, capsule.height * Mathf.Abs(scale.y));
                    centerOffset = capsule.transform.TransformVector(capsule.center);
                }
            }
        }

        public void ResetRoute()
        {
            points.Clear(); PathLength = 0; IsBlocked = false;
        }

        public Vector3[] CaptureContacts()
        {
            var result = new Vector3[ContactCount];
            for (int i = 0; i < result.Length; i++) result[i] = points[i + 1];
            return result;
        }

        public void RestoreContacts(Vector3 anchor, Vector3 endpoint, Vector3[] contacts)
        {
            ResetRoute(); planeZ = endpoint.z; points.Add(Planar(anchor));
            if (contacts != null)
                for (int i = 0; i < contacts.Length && points.Count <= maximumContacts; i++)
                    if (Finite(contacts[i])) points.Add(Planar(contacts[i]));
            points.Add(Planar(endpoint)); UpdateRoute(anchor, endpoint);
        }

        public bool UpdateRoute(Vector3 anchor, Vector3 endpoint)
        {
            if (!Finite(anchor) || !Finite(endpoint)) { IsBlocked = true; return false; }
            planeZ = endpoint.z; anchor = Planar(anchor); endpoint = Planar(endpoint);
            if (points.Count < 2) { points.Clear(); points.Add(anchor); points.Add(endpoint); }
            else { points[0] = anchor; points[points.Count - 1] = endpoint; }
            IsBlocked = false;

            // A contact is a fixed pulley until its neighbours can see one another with body clearance.
            Simplify();
            for (int segment = 0; segment < points.Count - 1; segment++)
            {
                if (SegmentClear(points[segment], points[segment + 1], out _)) continue;
                List<Vector3> detour;
                if (!FindDetour(points[segment], points[segment + 1], out detour)
                    || points.Count + detour.Count - 2 > maximumContacts + 2)
                { IsBlocked = true; continue; }
                // Detour contains both existing endpoints; insert only its interior contacts.
                for (int i = 1; i < detour.Count - 1; i++) points.Insert(segment + i, detour[i]);
                segment += detour.Count - 2;
            }
            Simplify(); RecalculateLength();
            return !IsBlocked;
        }

        void Simplify()
        {
            for (int i = 1; i < points.Count - 1;)
            {
                if (Vector3.Distance(points[i - 1], points[i]) < contactArrivalDistance
                    || SegmentClear(points[i - 1], points[i + 1], out _)) points.RemoveAt(i);
                else i++;
            }
        }

        public float Pull(CrewActor target, float distance)
        {
            if (target == null || target.body == null || points.Count < 2 || Mathf.Abs(distance) < .00001f) return 0;
            Vector3 anchor = points[0];
            if (!UpdateRoute(anchor, target.Position)) return 0;
            float beforeLength = PathLength;
            if (distance < 0)
            {
                Vector3 outward = -LastPullDirection;
                if (outward.sqrMagnitude < .5f) outward = Vector3.down;
                Vector3 next = target.Position + outward * -distance;
                MoveSafely(target, next, -distance);
                UpdateRoute(anchor, target.Position);
                return beforeLength - PathLength;
            }
            float remaining = distance;
            for (int i = 0; i < maximumPullSegments && remaining > .00001f && points.Count >= 2; i++)
            {
                Vector3 previous = target.Position;
                Vector3 pulley = points[points.Count - 2];
                float gap = Vector3.Distance(previous, pulley);
                // Reach the pulley itself before advancing. A loose arrival radius can leave
                // the capsule on the wrong side of a tight corner and recreate the same contact forever.
                if (gap <= .00001f && points.Count > 2)
                {
                    points.RemoveAt(points.Count - 2); UpdateRoute(anchor, target.Position); continue;
                }
                if (gap < .00001f) break;
                MoveSafely(target, pulley, Mathf.Min(remaining, gap));
                float travelled = Vector3.Distance(previous, target.Position);
                remaining -= travelled;
                if (travelled < .00001f) { IsBlocked = true; break; }
                UpdateRoute(anchor, target.Position);
                if (IsBlocked) break;
            }
            RecalculateLength(); return beforeLength - PathLength;
        }

        /// <summary>Same routed sweep for attached cargo. Returns a safe new position; never teleports across a wall.</summary>
        public Vector3 PullPoint(Vector3 endpoint, float distance)
        {
            if (points.Count < 2 || Mathf.Abs(distance) < .00001f) return endpoint;
            Vector3 anchor = points[0];
            if (!UpdateRoute(anchor, endpoint)) return endpoint;
            if (distance < 0)
            {
                Vector3 outward = -LastPullDirection;
                if (outward.sqrMagnitude < .5f) outward = Vector3.down;
                endpoint = SweepPoint(endpoint, endpoint + outward * -distance, -distance);
                UpdateRoute(anchor, endpoint); return endpoint;
            }
            for (int i = 0; i < maximumPullSegments && distance > .00001f; i++)
            {
                Vector3 pulley = points[points.Count - 2];
                float gap = Vector3.Distance(endpoint, pulley);
                if (gap <= .00001f && points.Count > 2)
                { points.RemoveAt(points.Count - 2); UpdateRoute(anchor, endpoint); continue; }
                if (gap < .00001f) break;
                Vector3 next = SweepPoint(endpoint, pulley, Mathf.Min(distance, gap));
                float travelled = Vector3.Distance(endpoint, next);
                if (travelled < .00001f) { IsBlocked = true; break; }
                endpoint = next; distance -= travelled;
                if (!UpdateRoute(anchor, endpoint)) break;
            }
            return endpoint;
        }

        Vector3 SweepPoint(Vector3 from, Vector3 destination, float distance)
        {
            Vector3 delta = destination - from;
            float permitted = Mathf.Min(distance, delta.magnitude);
            if (permitted < .00001f) return from;
            RaycastHit hit;
            if (!SegmentClear(from, from + delta.normalized * permitted, out hit))
                permitted = Mathf.Max(0, hit.distance - collisionSkin);
            return from + delta.normalized * permitted;
        }

        void MoveSafely(CrewActor target, Vector3 destination, float distance)
        {
            Vector3 delta = destination - target.Position;
            float permitted = Mathf.Min(distance, delta.magnitude);
            if (permitted < .00001f) return;
            RaycastHit hit;
            if (!SegmentClear(target.Position, target.Position + delta.normalized * permitted, out hit))
                permitted = Mathf.Max(0, hit.distance - collisionSkin);
            if (permitted > 0) target.MoveToward(destination, permitted);
        }

        public void ApplyLengthLimit(CrewActor target, float maximumLength)
        {
            if (target == null || target.body == null || points.Count < 2 || maximumLength <= 0) return;
            UpdateRoute(points[0], target.Position);
            if (IsBlocked) { target.body.linearVelocity = Vector3.zero; return; }
            if (PathLength > maximumLength) Pull(target, PathLength - maximumLength);
            if (PathLength < maximumLength - collisionSkin) return;
            Vector3 outward = -LastPullDirection;
            float velocity = Vector3.Dot(target.body.linearVelocity, outward);
            if (velocity > 0) target.body.linearVelocity -= outward * velocity;
        }

        public Vector3 ClampAnchorMove(Vector3 proposedAnchor, float maximumLength)
        {
            if (points.Count < 2 || maximumLength <= 0) return proposedAnchor;
            proposedAnchor = Planar(proposedAnchor);
            Vector3 current = points[0], endpoint = points[points.Count - 1];
            var saved = new List<Vector3>(points);
            float currentLength = PathLength;
            if (UpdateRoute(proposedAnchor, endpoint) && (PathLength <= maximumLength + .001f || PathLength < currentLength)) return proposedAnchor;
            float low = 0, high = 1;
            for (int i = 0; i < 10; i++)
            {
                points.Clear(); points.AddRange(saved);
                float middle = (low + high) * .5f;
                if (UpdateRoute(Vector3.Lerp(current, proposedAnchor, middle), endpoint) && PathLength <= Mathf.Max(maximumLength, currentLength) + .001f) low = middle;
                else high = middle;
            }
            points.Clear(); points.AddRange(saved);
            Vector3 allowed = Vector3.Lerp(current, proposedAnchor, low); UpdateRoute(allowed, endpoint); return allowed;
        }

        public bool IsSegmentClear(Vector3 from, Vector3 to) => SegmentClear(Planar(from), Planar(to), out _);

        bool SegmentClear(Vector3 from, Vector3 to, out RaycastHit closest)
        {
            closest = default; Vector3 difference = to - from; float distance = difference.magnitude;
            if (distance < .00001f) return true;
            Vector3 offset = Vector3.up * Mathf.Max(0, clearanceHeight * .5f - clearanceRadius);
            Vector3 center = from + centerOffset;
            int count = gameObject.scene.GetPhysicsScene().CapsuleCast(center + offset, center - offset, clearanceRadius,
                difference / distance, hits, distance, collisionMask, QueryTriggerInteraction.Ignore);
            bool clear = true; float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (Ignored(hits[i].collider) || hits[i].distance >= distance - .0001f) continue;
                if (hits[i].distance >= nearest) continue;
                clear = false; nearest = hits[i].distance; closest = hits[i];
            }
            return clear;
        }

        bool Ignored(Collider collider) => collider == null || !collider.enabled || collider.isTrigger
            || actor != null && collider.transform.IsChildOf(actor.transform)
            || ignoredShipRoot != null && collider.transform.IsChildOf(ignoredShipRoot);

        bool FindDetour(Vector3 start, Vector3 end, out List<Vector3> result)
        {
            result = null; searchCasts = 0;
            var nodes = new List<SearchNode> { new SearchNode(start) { distance = 0 }, new SearchNode(end) };
            var visitedObstacles = new HashSet<Collider>();
            float reach = Mathf.Max(minimumSearchReach, Vector3.Distance(start, end) * maximumDetourRatio);
            while (searchCasts < maximumSearchCasts)
            {
                int best = -1; float score = float.PositiveInfinity;
                for (int i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i].closed || float.IsPositiveInfinity(nodes[i].distance)) continue;
                    float estimate = nodes[i].distance + Vector3.Distance(nodes[i].point, end);
                    if (estimate < score) { best = i; score = estimate; }
                }
                if (best < 0 || score > reach) return false;
                var current = nodes[best]; current.closed = true;
                RaycastHit blocker; searchCasts++;
                if (SegmentClear(current.point, end, out blocker))
                {
                    nodes[1].parent = best;
                    result = new List<Vector3> { end };
                    int index = best;
                    while (index >= 0) { result.Add(nodes[index].point); index = nodes[index].parent; }
                    result.Reverse(); return true;
                }
                AddObstacle(blocker.collider, nodes, visitedObstacles, start, end, reach);
                for (int next = 2; next < nodes.Count && searchCasts < maximumSearchCasts; next++)
                {
                    if (nodes[next].closed) continue;
                    float cost = current.distance + Vector3.Distance(current.point, nodes[next].point);
                    if (cost >= nodes[next].distance || cost + Vector3.Distance(nodes[next].point, end) > reach) continue;
                    searchCasts++;
                    if (SegmentClear(current.point, nodes[next].point, out blocker))
                    { nodes[next].distance = cost; nodes[next].parent = best; }
                    else AddObstacle(blocker.collider, nodes, visitedObstacles, start, end, reach);
                }
            }
            return false;
        }

        void AddObstacle(Collider collider, List<SearchNode> nodes, HashSet<Collider> visited, Vector3 start, Vector3 end, float reach)
        {
            if (Ignored(collider) || nodes.Count >= maximumSearchNodes || !visited.Add(collider)) return;
            Bounds bounds = collider.bounds;
            float left = bounds.min.x - clearanceRadius - cornerMargin - centerOffset.x;
            float right = bounds.max.x + clearanceRadius + cornerMargin - centerOffset.x;
            float bottom = bounds.min.y - clearanceHeight * .5f - cornerMargin - centerOffset.y;
            float top = bounds.max.y + clearanceHeight * .5f + cornerMargin - centerOffset.y;
            AddCorner(new Vector3(left, bottom, planeZ), nodes, visited, start, end, reach);
            AddCorner(new Vector3(left, top, planeZ), nodes, visited, start, end, reach);
            AddCorner(new Vector3(right, bottom, planeZ), nodes, visited, start, end, reach);
            AddCorner(new Vector3(right, top, planeZ), nodes, visited, start, end, reach);
        }

        void AddCorner(Vector3 point, List<SearchNode> nodes, HashSet<Collider> visited, Vector3 start, Vector3 end, float reach)
        {
            if (nodes.Count >= maximumSearchNodes || Vector3.Distance(start, point) + Vector3.Distance(point, end) > reach) return;
            Vector3 offset = Vector3.up * Mathf.Max(0, clearanceHeight * .5f - clearanceRadius);
            Vector3 center = point + centerOffset;
            int count = gameObject.scene.GetPhysicsScene().OverlapCapsule(center + offset, center - offset, clearanceRadius,
                overlaps, collisionMask, QueryTriggerInteraction.Ignore);
            bool blocked = false;
            // Copy blockers before recursion: this component intentionally reuses its non-alloc query buffer.
            var adjacent = new List<Collider>();
            for (int i = 0; i < count; i++)
                if (!Ignored(overlaps[i])) { blocked = true; adjacent.Add(overlaps[i]); }
            if (blocked)
            {
                foreach (var obstacle in adjacent) AddObstacle(obstacle, nodes, visited, start, end, reach);
                return;
            }
            for (int i = 0; i < nodes.Count; i++) if ((nodes[i].point - point).sqrMagnitude < .0001f) return;
            var added = new SearchNode(point);
            // Obstacles are discovered lazily. A newly discovered corner may be visible from
            // an already expanded node, so connect it now rather than losing a valid maze route.
            for (int i = 0; i < nodes.Count && searchCasts < maximumSearchCasts; i++)
            {
                if (!nodes[i].closed) continue;
                float cost = nodes[i].distance + Vector3.Distance(nodes[i].point, point);
                if (cost >= added.distance || cost + Vector3.Distance(point, end) > reach) continue;
                searchCasts++;
                if (SegmentClear(nodes[i].point, point, out _)) { added.distance = cost; added.parent = i; }
            }
            nodes.Add(added);
        }

        void RecalculateLength()
        {
            PathLength = 0;
            for (int i = 1; i < points.Count; i++) PathLength += Vector3.Distance(points[i - 1], points[i]);
        }
        Vector3 Planar(Vector3 value) { value.z = planeZ; return value; }
        static bool Finite(Vector3 value) => !(float.IsNaN(value.x) || float.IsInfinity(value.x)
            || float.IsNaN(value.y) || float.IsInfinity(value.y) || float.IsNaN(value.z) || float.IsInfinity(value.z));
    }
}
