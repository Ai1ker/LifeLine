using UnityEngine;

namespace Lifeline
{
    /// <summary>Only the closest underwater interaction gets a small world-space label.</summary>
    public class WorldInteractionLabel : MonoBehaviour
    {
        public WorldGenerator world;
        public Transform owner;
        public TextMesh label;
        [Min(.001f)] public float characterSize = .06f;
        public void RefreshNow()
        {
            if (label == null) return;
            label.characterSize = characterSize;
            if(label.font!=RuntimeUIFont.GetOrCreateFont()){label.font=RuntimeUIFont.GetOrCreateFont();var rendererFont=label.GetComponent<Renderer>();if(rendererFont!=null)rendererFont.sharedMaterial=label.font.material;}
            var renderer = label.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = world != null && world.drawLabels
                && world.GetLabelFocus() == owner;
        }
        void LateUpdate() => RefreshNow();
        void OnEnable() => RefreshNow();
    }
}
