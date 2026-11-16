using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lifeline
{
    /// <summary>
    /// The ship interior and seabed are distinct maps. They share authoritative session data,
    /// not cabin geometry or physics coordinates. Runtime additive scenes also make that boundary
    /// explicit in the hierarchy; scene authoring retains cross-map references in Gameplay.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class GameMapPartition : MonoBehaviour
    {
        public GameSession session;
        public Vector3 shipMapOrigin = new Vector3(0, 1000, 0);
        public Vector3 waterMapOrigin = Vector3.zero;
        public string shipSceneName = "ShipMap";
        public string underwaterSceneName = "UnderwaterMap";
        [SerializeField, HideInInspector] Vector3 appliedShipOffset;

        public Scene ShipScene => shipScene;
        public Scene UnderwaterScene => underwaterScene;
        public Vector3 AppliedShipOffset => appliedShipOffset;
        public bool RuntimeMapsReady => shipScene.IsValid() && shipScene.isLoaded
            && underwaterScene.IsValid() && underwaterScene.isLoaded;

        Scene shipScene, underwaterScene;
        bool quitting, releasing;

        void Awake()
        {
            if (session == null) session = GetComponent<GameSession>();
            if (session != null) Initialize(session);
        }

        /// <summary>Idempotent, including a scene serialized after InitializeForAuthoring.</summary>
        public void Initialize(GameSession owner)
        {
            session = owner;
            ApplyMapOrigins();
            if (!Application.isPlaying || session == null) return;
            EnsureRuntimeMaps();
            if (session.ship != null) MoveGameplayObject(session.ship.gameObject, true);
            if (session.world != null) MoveGameplayObject(session.world.gameObject, false);
            MoveCrewToMap(session.captain);
            MoveCrewToMap(session.diver);
        }

        /// <summary>Keep one authored scene, with two separated coordinate spaces and saved references.</summary>
        public void InitializeForAuthoring(GameSession owner)
        {
            session = owner;
            ApplyMapOrigins();
        }

        void ApplyMapOrigins()
        {
            if (session == null || session.ship == null) return;
            Vector3 desired = shipMapOrigin - waterMapOrigin;
            Vector3 delta = desired - appliedShipOffset;
            if (delta.sqrMagnitude <= .000001f) return;
            session.ship.TranslateMapOrigin(delta);
            TranslateAboard(session.captain, delta);
            TranslateAboard(session.diver, delta);
            appliedShipOffset = desired;
            Physics.SyncTransforms();
        }

        void TranslateAboard(CrewActor actor, Vector3 delta)
        {
            if (actor == null || !actor.onboard) return;
            // A dynamic crew root is normally unparented. Avoid double translation in imported prefabs.
            if (session.ship != null && actor.transform.IsChildOf(session.ship.transform)) return;
            Translate(actor.transform, actor.body, delta);
        }

        static void Translate(Transform root, Rigidbody body, Vector3 delta)
        {
            Vector3 position = root.position + delta;
            if (body == null) body = root.GetComponent<Rigidbody>();
            if (body != null) body.position = position;
            root.position = position;
        }

        public Vector3 ShipToWaterPoint(Vector3 shipWorldPoint) => shipWorldPoint - appliedShipOffset;
        public Vector3 WaterToShipPoint(Vector3 waterWorldPoint) => waterWorldPoint + appliedShipOffset;

        public void MoveCrewToMap(CrewActor actor)
        {
            if (actor == null) return;
            MoveGameplayObject(actor.gameObject, actor.onboard);
        }

        /// <summary>Route spawned enemies, projectiles, lost cargo and visual proxies to their owning map.</summary>
        public void MoveGameplayObject(GameObject root, bool onShipMap)
        {
            if (!Application.isPlaying || root == null || releasing) return;
            EnsureRuntimeMaps();
            Scene destination = onShipMap ? shipScene : underwaterScene;
            if (!destination.IsValid() || !destination.isLoaded || root.scene == destination) return;
            if (root.transform.parent != null) root.transform.SetParent(null, true);
            SceneManager.MoveGameObjectToScene(root, destination);
        }

        void EnsureRuntimeMaps()
        {
            if (releasing || !Application.isPlaying) return;
            if (!shipScene.IsValid() || !shipScene.isLoaded) shipScene = CreateOwnedScene(shipSceneName);
            if (!underwaterScene.IsValid() || !underwaterScene.isLoaded) underwaterScene = CreateOwnedScene(underwaterSceneName);
        }

        Scene CreateOwnedScene(string requestedName)
        {
            string title = string.IsNullOrWhiteSpace(requestedName) ? "LifeLineMap" : requestedName;
            // A second fixture/session must never borrow and later unload another session's map.
            Scene existing = SceneManager.GetSceneByName(title);
            if (existing.IsValid() && existing.isLoaded) title += "_" + GetEntityId();
            return SceneManager.CreateScene(title);
        }

        public void ReleaseRuntimeMaps()
        {
            if (releasing) return;
            releasing = true;
            if (!Application.isPlaying || quitting) return;
            ReleaseScene(shipScene);
            ReleaseScene(underwaterScene);
        }

        static void ReleaseScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || SceneManager.sceneCount <= 1) return;
            // Single-scene menu loading already unloads both maps. The guard also supports destroying
            // just the session root, where Unity would otherwise leave the additive map objects alive.
            SceneManager.UnloadSceneAsync(scene);
        }

        void OnApplicationQuit() => quitting = true;
        void OnDestroy() => ReleaseRuntimeMaps();
    }
}
