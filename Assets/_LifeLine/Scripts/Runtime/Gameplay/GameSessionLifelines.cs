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
            if (cargo && winchCargo != null) return GetCargoLifeline(winchCargo);
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

        public DiverRope GetCargoLifeline(LootPickup cargo)
        {
            if (cargo.lifeline == null)
            {
                var obj = new GameObject("Cargo collision rope"); obj.transform.SetParent(cargo.transform, false);
                cargo.lifeline = obj.AddComponent<DiverRope>();
            }
            var route = cargo.lifeline;
            route.Configure(diver, ship != null ? ship.transform : null);
            route.collisionMask = world != null ? 1 << world.geometryLayer : obstacleMask & ~(1 << 9) & ~(1 << 10);
            if (cargo == winchCargo) cargoRope = route;
            return route;
        }

        public void ResetLifelines()
        {
            if(captainRope!=null)captainRope.ResetRoute();
            if (diverRope != null) diverRope.ResetRoute();
            if (world != null) foreach (var cargo in world.pickups) if (cargo != null && cargo.lifeline != null) cargo.lifeline.ResetRoute();
            if (cargoRope != null) cargoRope.ResetRoute();
        }

        public void RefreshLifelines()
        {
            if(captain!=null&&!captain.onboard)GetCrewLifeline(captain).UpdateRoute(BoatAnchor,captain.Position);
            else if(captainRope!=null)captainRope.ResetRoute();
            if (diver != null && !diver.onboard) GetOrCreateLifeline().UpdateRoute(BoatAnchor, diver.Position);
            else if (diverRope != null) diverRope.ResetRoute();
            if (world != null) foreach (var cargo in world.pickups)
                if (IsWinchCargo(cargo)) GetCargoLifeline(cargo).UpdateRoute(BoatAnchor, cargo.transform.position);
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
            if(Crosses(captainRope,bounds))return true;
            if (Crosses(diverRope, bounds)) return true;
            if (world != null) foreach (var cargo in world.pickups)
                if (IsWinchCargo(cargo) && Crosses(cargo.lifeline, bounds)) return true;
            return false;
        }

        void EnsureCaptainLifelineView()
        {
            if(!Application.isPlaying||captain==null)return;
            foreach(var view in FindObjectsByType<DiverLifelineView>())if(view.session==this&&view.swimmer==captain)return;
            var obj=new GameObject("Captain lifeline presentation");if(maps==null)obj.transform.SetParent(transform,false);
            var line=obj.AddComponent<DiverLifelineView>();line.swimmer=captain;line.Initialize(this);
            maps?.MoveGameplayObject(obj,false);
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
