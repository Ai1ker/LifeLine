using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lifeline.Editor
{
    /// <summary>Measures the opaque artwork itself so weapons, wings and fins do not set body size.</summary>
    internal sealed class PixelPoseRegistration
    {
        public readonly int width, height;
        readonly Color32[] pixels;

        public PixelPoseRegistration(Sprite sprite)
        {
            var source = new Texture2D(2, 2);
            try
            {
                if (!source.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))))
                    throw new InvalidOperationException("Cannot decode sprite source " + sprite.name);
                width = source.width;
                height = source.height;
                pixels = source.GetPixels32();
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }

        public readonly struct Landmark
        {
            public readonly Vector2 center;
            public readonly float size;
            public Landmark(Vector2 center, float size) { this.center = center; this.size = size; }
        }

        public RectInt OpaqueBounds()
        {
            int left = width, bottom = height, right = -1, top = -1;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a < PixelSpriteAlpha.SolidAlpha) continue;
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                }
            return right < left ? default : new RectInt(left, bottom, right - left + 1, top - bottom + 1);
        }

        public Landmark CrewHead(bool captain, bool upright)
        {
            float bottom = upright ? .54f : .30f;
            RectInt mark = captain
                ? LargestSwatch(c => c.r >= 158 && c.g >= 130 && c.b >= 92 && c.r - c.g >= 3 && c.r - c.g <= 61 && c.g - c.b <= 64, bottom)
                : LargestSwatch(c => c.r <= 106 && c.g >= 66 && c.b >= 72 && c.g - c.r >= 21 && c.b - c.r >= 22 && c.g > .75f * c.b, bottom);
            return ToLandmark(mark);
        }

        public Landmark EnemyCore(bool bird)
        {
            if (bird)
            {
                var beak = LargestSwatch(c => c.r > 144 && c.g > 65 && c.r - c.g > 22 && c.b < 91 && c.g - c.b > 18,
                    .16f, .57f);
                if (beak.width > 5 && beak.height > 5) return ToLandmark(beak);
            }

            // The median solid section just before the jaw ignores fins and a snapped-open mouth.
            int sampleCount = 11;
            var thickness = new int[sampleCount];
            var heights = new float[sampleCount];
            for (int sample = 0; sample < sampleCount; sample++)
            {
                int x = Mathf.Clamp(Mathf.RoundToInt(width * Mathf.Lerp(.49f, .62f, sample / (sampleCount - 1f))), 0, width - 1);
                int start = -1, longest = 0;
                for (int y = 0; y <= height; y++)
                {
                    bool solid = y < height && pixels[y * width + x].a >= 220;
                    if (solid && start < 0) start = y;
                    if (solid || start < 0) continue;
                    if (y - start > longest) { longest = y - start; heights[sample] = (y + start) * .5f; }
                    start = -1;
                }
                thickness[sample] = longest;
            }
            Array.Sort(thickness);
            Array.Sort(heights);
            return new Landmark(new Vector2(width * .555f, heights[sampleCount / 2]), thickness[sampleCount / 2]);
        }

        RectInt LargestSwatch(Func<Color32, bool> includes, float vMin, float uMin = 0)
        {
            int lowY = Mathf.FloorToInt(height * vMin), lowX = Mathf.FloorToInt(width * uMin);
            var valid = new bool[pixels.Length];
            for (int y = lowY; y < height; y++)
                for (int x = lowX; x < width; x++)
                {
                    var color = pixels[y * width + x];
                    valid[y * width + x] = color.a >= 220 && includes(color);
                }
            var queue = new int[valid.Length];
            RectInt result = default;
            int largest = 0;
            for (int i = 0; i < valid.Length; i++)
            {
                if (!valid[i]) continue;
                int read = 0, end = 1;
                valid[i] = false;
                queue[0] = i;
                int minX = width, minY = height, maxX = 0, maxY = 0;
                while (read < end)
                {
                    int position = queue[read++];
                    int x = position % width, y = position / width;
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                    if (x > lowX) Push(position - 1);
                    if (x + 1 < width) Push(position + 1);
                    if (y > lowY) Push(position - width);
                    if (y + 1 < height) Push(position + width);
                }
                if (end > largest)
                {
                    largest = end;
                    result = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
                }
                void Push(int position)
                {
                    if (!valid[position]) return;
                    valid[position] = false;
                    queue[end++] = position;
                }
            }
            return result;
        }

        static Landmark ToLandmark(RectInt mark) => new Landmark(mark.center, Mathf.Sqrt(mark.width * mark.height));

        public void SaveRegistration(Sprite sprite, float pixelsPerMetre, Vector2 origin)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite));
            Vector2 pivot = new Vector2(Mathf.Clamp01(origin.x / width), Mathf.Clamp01(origin.y / height));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteAlignment == (int)SpriteAlignment.Custom && settings.spritePivot == pivot &&
                Mathf.Approximately(importer.spritePixelsPerUnit, pixelsPerMetre)) return;
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            importer.SetTextureSettings(settings);
            importer.spritePixelsPerUnit = pixelsPerMetre;
            importer.SaveAndReimport();
        }
    }
}
