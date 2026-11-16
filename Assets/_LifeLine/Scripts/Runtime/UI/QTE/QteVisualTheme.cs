using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    [CreateAssetMenu(fileName = "QteVisualTheme", menuName = "LifeLine/UI/QTE Visual Theme")]
    public sealed class QteVisualTheme : ScriptableObject
    {
        [Header("Replaceable PNG sprites; never used for gameplay judgment")]
        public Sprite keycap, keycapSuccess, keycapFailure;
        public Sprite lane, hitLine, ring, ringFace, needle, hub;
        public Sprite upArrow, leftArrow, downArrow, rightArrow;
        public Font keyFont;
        public Color trackColor = new Color(.16f, .255f, .29f);
        public Color faceColor = new Color(.07f, .17f, .215f);
        public Color gold = new Color(.9f, .76f, .53f);
        public Color success = new Color(.51f, .835f, .714f);
        public Color failure = new Color(.91f, .635f, .494f);
        public Color keyText = new Color(.76f, .94f, .84f);
        public Color successKeyText = new Color(.025f, .176f, .176f);
        public Color failureKeyText = new Color(.94f, .71f, .61f);
        [Range(0, 1)] public float laneHitPosition = .75f;
        [Min(.01f)] public float laneTravelPerCycle = .65f;
        public Vector2 keySize = new Vector2(48, 48);
        [Min(.1f)] public float feedbackSeconds = .65f;

        [Serializable] public sealed class BindingArtwork
        {
            public string controlPath;
            public Sprite sprite;
            [Tooltip("Zero uses the theme's default key size; positive XY overrides the complete keycap size.")]
            public Vector2 size;
            [Tooltip("Disable when the replacement artwork already contains the key name.")]
            public bool showLabel = true;
        }
        [Tooltip("Optional complete keycap art per effective Input System path, including rebound keys.")]
        public List<BindingArtwork> bindingArtwork = new List<BindingArtwork>();

        public BindingArtwork FindArtwork(string path) => bindingArtwork?.Find(item => item != null
            && string.Equals(item.controlPath, path, StringComparison.OrdinalIgnoreCase));
        public Sprite Arrow(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            switch (path.ToLowerInvariant())
            {
                case "<keyboard>/uparrow": return upArrow;
                case "<keyboard>/leftarrow": return leftArrow;
                case "<keyboard>/downarrow": return downArrow;
                case "<keyboard>/rightarrow": return rightArrow;
                default: return null;
            }
        }
    }
}
