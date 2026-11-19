using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>A large salvaged ship device. Hold Collect to send it aboard; it never becomes raw metal or occupies a backpack slot.</summary>
    public sealed class FilterSalvage : MonoBehaviour
    {
        public const string SaveIdPrefix = "filter-salvage:";
        public LootPickup pickup;
        [Min(.1f)] public float salvageSeconds = 3f;
        public Transform equipmentVisual;

        public bool TryAcquire(CrewActor actor)
        {
            if (pickup == null || !pickup.InRange(actor) || actor.session == null || !actor.session.AcquireFilter()) return false;
            actor.session.Tell("海水过滤器已打捞上船，需要2铁板修复后启用。");
            return true;
        }

        public string Prompt() => UiLocalization.Text("海水过滤器");

        public void ConfigureVisual(GameObject authoredPrefab, Material body, Material trim)
        {
            if (equipmentVisual != null || pickup == null) return;
            var visual = pickup.visualRoot;
            if (visual == null)
            {
                visual = new GameObject("VisualRoot").transform; visual.SetParent(transform, false); pickup.visualRoot = visual;
                // Dynamic save restoration can start from a primitive cube; its root mesh is
                // presentation only and must not cover the distinct two-tank device placeholder.
                var genericRenderer = pickup.GetComponent<Renderer>();
                if (genericRenderer != null) genericRenderer.enabled = false;
            }
            // Keep the world label while replacing the generic raw-resource cube.
            var replaced = new List<GameObject>();
            foreach (Transform child in visual)
                if (child.GetComponent<TextMesh>() == null) replaced.Add(child.gameObject);
            foreach (var child in replaced) RemovePlaceholder(child);
            var holder = new GameObject("Equipment visual (replaceable)").transform;
            holder.SetParent(visual, false); equipmentVisual = holder;
            if (authoredPrefab != null)
            {
                Instantiate(authoredPrefab, holder, false);
                return;
            }
            Part(holder, "Filter tank A", new Vector3(-.22f, 0, 0), new Vector3(.32f, .84f, .42f), body);
            Part(holder, "Filter tank B", new Vector3(.22f, 0, 0), new Vector3(.32f, .84f, .42f), body);
            Part(holder, "Connecting pipe", new Vector3(0, .25f, -.07f), new Vector3(.75f, .13f, .16f), trim);
            Part(holder, "Base", new Vector3(0, -.43f, 0), new Vector3(.91f, .13f, .51f), trim);
        }

        static void Part(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = size;
            part.layer = parent.gameObject.layer;
            var collider = part.GetComponent<Collider>(); if (collider != null) RemovePlaceholder(collider);
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void RemovePlaceholder(Object target)
        {
            if (target is GameObject gameObject) gameObject.SetActive(false);
            if (target is Collider collider) collider.enabled = false;
            if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
        }
    }
}
