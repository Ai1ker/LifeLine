using System.Collections.Generic;
using UnityEngine;

namespace Lifeline.Editor
{
    internal static class PixelSpriteAlpha
    {
        // Preserve crisp pixel edges while stripping the faint full-canvas matte common to the generated poses.
        internal const byte SolidAlpha = 112;

        internal static void RemoveMatte(Texture2D texture)
        {
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width, height = texture.height;
            var labels = new int[pixels.Length];
            var queue = new int[pixels.Length];
            var components = new List<(int size, bool atCorner)> { (0, false) };
            int largest = 0;
            for (int start = 0; start < pixels.Length; start++)
            {
                if (labels[start] != 0 || pixels[start].a < SolidAlpha) continue;
                int label = components.Count, read = 0, count = 1;
                bool atCorner = false;
                labels[start] = label;
                queue[0] = start;
                while (read < count)
                {
                    int current = queue[read++];
                    int x = current % width, y = current / width;
                    atCorner |= (x < 3 || x >= width - 3) && (y < 3 || y >= height - 3);
                    for (int row = Mathf.Max(0, y - 1); row <= Mathf.Min(height - 1, y + 1); row++)
                        for (int column = Mathf.Max(0, x - 1); column <= Mathf.Min(width - 1, x + 1); column++)
                        {
                            int next = row * width + column;
                            if (labels[next] != 0 || pixels[next].a < SolidAlpha) continue;
                            labels[next] = label;
                            queue[count++] = next;
                        }
                }
                components.Add((count, atCorner));
                largest = Mathf.Max(largest, count);
            }
            for (int i = 0; i < pixels.Length; i++)
            {
                var component = components[labels[i]];
                if (labels[i] == 0 || component.atCorner && component.size < largest * .06f) pixels[i].a = 0;
            }
            texture.SetPixels32(pixels);
        }
    }
}
