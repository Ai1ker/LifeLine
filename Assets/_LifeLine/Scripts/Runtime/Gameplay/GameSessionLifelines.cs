using UnityEngine;

namespace Lifeline
{
    public partial class GameSession
    {
        public void RegisterAttachedCargo(LootPickup cargo)
        {
            if (cargo == null || !cargo.isCrate || !cargo.attached || cargo.consumed) return;
            if (winchCargo == null && !winchSignalAuthorized)
            {
                winchCargo = cargo; winchTargetKind = "";
                GetOrCreateLifeline(true).ResetRoute();
                GetOrCreateLifeline(true).UpdateRoute(BoatAnchor, cargo.transform.position);
            }
            Changed();
        }

        public DiverRope GetOrCreateLifeline(bool cargo = false)
        {
            DiverRope route = cargo ? cargoRope : diverRope;
            if (route == null)
            {
                var root = new GameObject(cargo ? "Cargo collision rope" : "Diver collision rope");
                root.transform.SetParent(transform, false);
                route = root.AddComponent<DiverRope>();
                if (cargo) cargoRope = route; else diverRope = route;
            }
            route.Configure(diver, ship != null ? ship.transform : null);
            route.collisionMask = world != null ? 1 << world.geometryLayer : obstacleMask & ~(1 << 9) & ~(1 << 10);
            return route;
        }

        public void ResetLifelines()
        {
            if (diverRope != null) diverRope.ResetRoute();
            if (cargoRope != null) cargoRope.ResetRoute();
        }

        public void RefreshLifelines()
        {
            if (diver != null && !diver.onboard) GetOrCreateLifeline().UpdateRoute(BoatAnchor, diver.Position);
            else if (diverRope != null) diverRope.ResetRoute();
            if (winchCargo != null && winchCargo.attached && !winchCargo.consumed)
                GetOrCreateLifeline(true).UpdateRoute(BoatAnchor, winchCargo.transform.position);
            else if (cargoRope != null) cargoRope.ResetRoute();
        }

        public float DiverRopeLength
        {
            get
            {
                if (diver == null || diver.onboard) return 0;
                DiverRope route = GetOrCreateLifeline();
                route.UpdateRoute(BoatAnchor, diver.Position);
                return route.PathLength;
            }
        }

        public void RestoreLifelines(Vector3[] diverContacts, Vector3[] cargoContacts)
        {
            ResetLifelines();
            if (diver != null && !diver.onboard) GetOrCreateLifeline().RestoreContacts(BoatAnchor, diver.Position, diverContacts);
            if (winchCargo != null && winchCargo.attached && !winchCargo.consumed)
                GetOrCreateLifeline(true).RestoreContacts(BoatAnchor, winchCargo.transform.position, cargoContacts);
        }

        public bool RopeCrossesBounds(Bounds bounds)
        {
            RefreshLifelines();
            return Crosses(diverRope, bounds) || Crosses(cargoRope, bounds);
        }

        static bool Crosses(DiverRope route, Bounds bounds)
        {
            if (route == null || route.Points.Count < 2) return false;
            bounds.Expand(route.clearanceRadius * 2 + route.collisionSkin * 2);
            for (int i = 1; i < route.Points.Count; i++)
            {
                Vector3 start = route.Points[i - 1], delta = route.Points[i] - start;
                if (bounds.Contains(start) || bounds.Contains(route.Points[i])) return true;
                if (delta.sqrMagnitude > .000001f && bounds.IntersectRay(new Ray(start, delta.normalized), out float distance) && distance <= delta.magnitude) return true;
            }
            return false;
        }
    }
}
