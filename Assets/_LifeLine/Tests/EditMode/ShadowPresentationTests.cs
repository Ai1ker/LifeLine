using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Lifeline.Tests
{
    public sealed class ShadowPresentationTests
    {
        [Test] public void ShadowUsesTheSameVisibleWidthInEveryImportedAttackFrame()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_LifeLine/Prefabs/Enemies/Shadow.prefab");
            var art = prefab.GetComponent<PixelEnemyView>().primary;
            Assert.That(art.registeredSprites);
            const float sizeMultiplier = 2;
            var reference = OpaqueSize(art.travel[0], art.RegisteredScale(art.travel[0], sizeMultiplier));
            foreach (var sequence in new[] { art.travel, art.attack, art.dash, art.waterShot, art.basicAttack })
            {
                Assert.That(sequence, Is.Not.Empty);
                foreach (var sprite in sequence)
                {
                    var size = OpaqueSize(sprite, art.RegisteredScale(sprite, sizeMultiplier));
                    Assert.That(size.x, Is.EqualTo(art.width * sizeMultiplier).Within(.02f), sprite.name);
                    Assert.That(size.y, Is.InRange(reference.y * .93f, reference.y * 1.10f), sprite.name);
                }
            }
        }

        static Vector2 OpaqueSize(Sprite sprite, Vector3 scale)
        {
            var pixels = new Texture2D(2, 2);
            try
            {
                Assert.That(pixels.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite))));
                var colors = pixels.GetPixels32();
                Vector2Int first = new Vector2Int(pixels.width, pixels.height), last = new Vector2Int(-1, -1);
                for (int i = 0; i < colors.Length; i++)
                {
                    if (colors[i].a < 112) continue;
                    var point = new Vector2Int(i % pixels.width, i / pixels.width);
                    first = Vector2Int.Min(first, point); last = Vector2Int.Max(last, point);
                }
                var dimensions = (Vector2)(last - first + Vector2Int.one);
                return Vector2.Scale(dimensions / sprite.pixelsPerUnit, scale);
            }
            finally { Object.DestroyImmediate(pixels); }
        }
    }
}
