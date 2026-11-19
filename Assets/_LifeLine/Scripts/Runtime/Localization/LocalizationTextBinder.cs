using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Display-only adapter for existing dynamic Text and TextMesh writers. Never changes the source model.</summary>
    [DefaultExecutionOrder(10000)]
    public sealed class LocalizationTextBinder : MonoBehaviour
    {
        private sealed class DisplaySource
        {
            public Component component;
            public string source, rendered;
            public string Read() => component is Text ui ? ui.text : ((TextMesh)component).text;
            public void Write(string value) { if (component is Text ui) ui.text = value; else ((TextMesh)component).text = value; }
        }
        [Tooltip("Optional explicit key for a Text or TextMesh on this object.")]
        public string localizationKey;
        public bool includeChildren = true;
        [Tooltip("Use one central binder per scene to cover both UI and world labels, including runtime instances.")]
        public bool scanWholeScene;
        public bool translateInputFields;
        [Tooltip("Use the runtime CJK fallback font for placeholder world labels. Disable when supplying an authored font/material.")]
        public bool useRuntimeFontForWorldLabels = true;
        [Min(.05f)] public float discoveryInterval = .25f;
        private readonly Dictionary<Component, DisplaySource> sources = new Dictionary<Component, DisplaySource>();
        private readonly List<Component> removed = new List<Component>();
        private float nextDiscovery;
        private bool refreshRequested;

        private void OnEnable()
        {
            LocalizationManager.LanguageChanged += OnLanguageChanged;
            RebuildTargets(); Refresh();
        }
        private void OnDisable() { LocalizationManager.LanguageChanged -= OnLanguageChanged; }
        private void OnLanguageChanged() { refreshRequested = true; }

        private void LateUpdate()
        {
            if (Time.unscaledTime >= nextDiscovery) RebuildTargets();
            Render(LocalizationManager.Current, refreshRequested);
        }
        public void RebuildTargets()
        {
            nextDiscovery = Time.unscaledTime + Mathf.Max(.05f, discoveryInterval);
            if (scanWholeScene)
            {
                foreach (var text in FindObjectsByType<Text>(FindObjectsInactive.Include)) Add(text);
                foreach (var text in FindObjectsByType<TextMesh>(FindObjectsInactive.Include)) Add(text);
            }
            else if (includeChildren)
            {
                foreach (var text in GetComponentsInChildren<Text>(true)) Add(text);
                foreach (var text in GetComponentsInChildren<TextMesh>(true)) Add(text);
            }
            else { Add(GetComponent<Text>()); Add(GetComponent<TextMesh>()); }
            removed.Clear();
            foreach (var item in sources) if (item.Value.component == null) removed.Add(item.Key);
            foreach (var component in removed) sources.Remove(component);
        }
        private void Add(Component component)
        {
            if (component == null || !component.gameObject.scene.IsValid()) return;
            if (scanWholeScene && component.gameObject.scene != gameObject.scene) return;
            if (!translateInputFields && component.GetComponentInParent<InputField>(true) != null) return;
            if (sources.ContainsKey(component)) return;
            if (component.TryGetComponent<LocalizationLiteralText>(out _)) return;
            if (useRuntimeFontForWorldLabels && component is TextMesh worldLabel)
            {
                var font = RuntimeUIFont.GetOrCreateFont();
                if (font != null)
                {
                    worldLabel.font = font;
                    var renderer = worldLabel.GetComponent<MeshRenderer>();
                    if (renderer != null) renderer.sharedMaterial = font.material;
                }
            }
            string source = component is Text ui ? ui.text : ((TextMesh)component).text;
            sources[component] = new DisplaySource { component = component, source = source, rendered = source };
            refreshRequested = true;
        }

        /// <summary>Captures external text writes, retains their source, and renders the selected language.</summary>
        public void Refresh() { Render(LocalizationManager.Current, true); }
        public void Refresh(LocalizationManager manager) { Render(manager, true); }
        private void Render(LocalizationManager manager, bool force)
        {
            if (manager == null) return;
            foreach (var item in sources.Values)
            {
                if (item.component == null) continue;
                string current = item.Read();
                bool changed = current != item.rendered;
                if (changed) item.source = current;
                if (!changed && !force) continue;
                string key = item.component.gameObject == gameObject && !string.IsNullOrEmpty(localizationKey) ? localizationKey : item.source;
                item.rendered = manager.Translate(key);
                if (current != item.rendered) item.Write(item.rendered);
            }
            refreshRequested = false;
        }
        public List<string> GetSources()
        {
            var result = new List<string>();
            foreach (var item in sources.Values) if (item.component != null) result.Add(item.source);
            return result;
        }
        public string GetSource(Component component) => component != null && sources.TryGetValue(component, out var item) ? item.source : null;
        public List<string> FindUntranslatedChinese() => LocalizationManager.Current != null
            ? LocalizationManager.Current.FindUntranslatedChinese(GetSources()) : new List<string>();
    }
}
