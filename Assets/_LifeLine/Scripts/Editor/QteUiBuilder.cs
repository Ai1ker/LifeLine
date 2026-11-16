#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline.Editor
{
    /// <summary>Creates editable sprites/SO/prefab once. Existing art and authored theme values are preserved.</summary>
    public static class QteUiBuilder
    {
        public const string Art = "Assets/_LifeLine/Textures/UI/QTE";
        public const string ThemePath = "Assets/_LifeLine/Data/UI/QteVisualTheme.asset";
        public const string PrefabPath = "Assets/_LifeLine/Prefabs/UI/QteStationView.prefab";

        [MenuItem("LifeLine/UI/Install HTML Style QTE (Preserve Existing Art)")]
        public static void ApplyToGameplay()
        {
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.isDirty) throw new InvalidOperationException("Save scene edits before installing QTE views.");
            string path = GameProjectBuilder.PlayScene;
            string backup = GameProjectBuilder.Root + "/Documentation/UIBackups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_ffff");
            Directory.CreateDirectory(backup); File.Copy(path, backup + "/Gameplay.unity.bak");
            var scene = EditorSceneManager.OpenScene(path);
            int count = 0;
            foreach (var panel in UnityEngine.Object.FindObjectsByType<CrewStationPanel>(FindObjectsInactive.Include))
            { if (panel.gameObject.scene != scene) continue; Install(panel); count++; }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Installed HTML style QTE for " + count + " station panels; art preserved. Backup: " + backup);
        }

        public static void Install(CrewStationPanel panel)
        {
            if (panel.qteView != null) return;
            var prefab = BuildAssets();
            var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, panel.panel.transform);
            obj.name = "QTE presentation";
            panel.qteView = obj.GetComponent<QteStationView>();
            var rect = (RectTransform)obj.transform;
            rect.anchorMin = new Vector2(.05f, .30f); rect.anchorMax = new Vector2(.95f, .63f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            obj.SetActive(false);
            EditorUtility.SetDirty(panel);
        }

        public static GameObject BuildAssets()
        {
            Directory.CreateDirectory(Art); Directory.CreateDirectory(Path.GetDirectoryName(ThemePath));
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath)); AssetDatabase.Refresh();
            var theme = AssetDatabase.LoadAssetAtPath<QteVisualTheme>(ThemePath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<QteVisualTheme>();
                theme.keycap = Cap("Keycap", new Color(.13f, .294f, .298f), new Color(.5f, .8f, .73f));
                theme.keycapSuccess = Cap("KeycapSuccess", new Color(.41f, .725f, .596f), new Color(.61f, .94f, .75f));
                theme.keycapFailure = Cap("KeycapFailure", new Color(.48f, .26f, .227f), new Color(.84f, .56f, .47f));
                theme.ring = Texture("RingTrack", (x,y) => WhiteBand(new Vector2(x,y).magnitude, .36f, .46f));
                theme.ringFace = Texture("RingFace", (x,y) => WhiteBand(new Vector2(x,y).magnitude, 0, .355f));
                theme.hub = Texture("Hub", (x,y) => WhiteBand(new Vector2(x,y).magnitude, 0, .46f));
                theme.needle = Texture("Needle", (x,y) => new Color(1,1,1,
                    Mathf.Clamp01((.009f - Mathf.Abs(x)) * 256) * Mathf.Clamp01((y - .16f) * 256) * Mathf.Clamp01((.45f - y) * 256)));
                theme.hitLine = Texture("HitLine", (x,y) => Color.white, 8);
                theme.lane = Texture("RhythmLane", (x,y) => Mathf.Abs(y) > .47f ? new Color(.16f,.27f,.30f)
                    : Mathf.Repeat((x + .5f) * 256, 26) < 1.5f ? new Color(.12f,.21f,.235f) : new Color(.04f,.115f,.15f));
                theme.upArrow = Arrow("ArrowUp", 0); theme.leftArrow = Arrow("ArrowLeft", 1);
                theme.downArrow = Arrow("ArrowDown", 2); theme.rightArrow = Arrow("ArrowRight", 3);
                AssetDatabase.CreateAsset(theme, ThemePath); AssetDatabase.SaveAssets();
            }
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null) return existing;
            var root = new GameObject("QteStationView", typeof(RectTransform));
            try
            {
                var view = root.AddComponent<QteStationView>(); view.theme = theme;
                var lane = Rect("Moving key lane", root.transform, new Vector2(0,.2f), new Vector2(1,.8f)); view.laneRoot = lane.gameObject;
                view.laneBackground = Image("Track", lane, Vector2.zero, Vector2.one, theme.lane);
                view.laneWindow = Image("Judgment window", lane, new Vector2(.65f,0), new Vector2(.85f,1), theme.hitLine);
                view.laneHitLine = Image("Gold judgment line", lane, new Vector2(.75f,.5f), new Vector2(.75f,.5f), theme.hitLine);
                view.laneHitLine.rectTransform.sizeDelta = new Vector2(3, 66);
                Key(lane, theme, out view.laneKeycap, out view.laneGlyph, out view.laneKeyLabel);

                var circle = Rect("Skill ring", root.transform, Vector2.zero, Vector2.one); view.circleRoot = circle.gameObject;
                var aspect = circle.gameObject.AddComponent<AspectRatioFitter>(); aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent; aspect.aspectRatio = 1;
                view.ringTrack = Image("Ring track", circle, Vector2.zero, Vector2.one, theme.ring); view.ringTrack.color = theme.trackColor;
                view.ringWindow = Image("Green judgment arc", circle, Vector2.zero, Vector2.one, theme.ring);
                view.ringFace = Image("Inner face", circle, Vector2.zero, Vector2.one, theme.ringFace); view.ringFace.color = theme.faceColor;
                view.needlePivot = Rect("Needle pivot", circle, Vector2.zero, Vector2.one);
                view.ringNeedle = Image("Rotating needle", view.needlePivot, Vector2.zero, Vector2.one, theme.needle);
                view.ringHub = Image("Hub", circle, new Vector2(.5f,.5f), new Vector2(.5f,.5f), theme.hub); view.ringHub.rectTransform.sizeDelta = new Vector2(6,6);
                view.centerValue = Text("Progress", circle, new Vector2(.22f,.45f), new Vector2(.78f,.66f), 25);
                Key(circle, theme, out view.centerKeycap, out view.centerGlyph, out view.centerKeyLabel);
                view.centerKeycap.rectTransform.anchorMin = view.centerKeycap.rectTransform.anchorMax = new Vector2(.5f,.31f);
                view.SetMode(false);
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        static void Key(Transform parent, QteVisualTheme theme, out Image cap, out Image glyph, out Text label)
        {
            cap = Image("Keycap", parent, new Vector2(.5f,.5f), new Vector2(.5f,.5f), theme.keycap);
            cap.rectTransform.sizeDelta = theme.keySize; cap.type = UnityEngine.UI.Image.Type.Sliced;
            glyph = Image("Binding glyph", cap.transform, new Vector2(.25f,.25f), new Vector2(.75f,.75f), null); glyph.preserveAspect = true;
            label = Text("Current binding", cap.transform, new Vector2(.08f,.08f), new Vector2(.92f,.92f), 24);
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 10; label.resizeTextMaxSize = 24;
            label.color = theme.keyText;
        }
        static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        static Image Image(string name, Transform parent, Vector2 min, Vector2 max, Sprite sprite)
        {
            var image = Rect(name, parent, min, max).gameObject.AddComponent<Image>(); image.sprite = sprite; image.raycastTarget = false; return image;
        }
        static Text Text(string name, Transform parent, Vector2 min, Vector2 max, int size)
        {
            var text = Rect(name, parent, min, max).gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size; text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
        }
        static Color WhiteBand(float radius, float inner, float outer) => new Color(1,1,1,
            Mathf.Clamp01((outer - radius) * 256) * (inner == 0 ? 1 : Mathf.Clamp01((radius - inner) * 256)));
        static Sprite Cap(string name, Color face, Color border) => Texture(name, (x,y) =>
        {
            Vector2 p = new Vector2(Mathf.Abs(x) - .36f, Mathf.Abs(y) - .36f);
            float edge = Vector2.Max(p, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(p.x,p.y),0) - .09f;
            Color c = edge > -.025f ? border : y < -.30f ? face * .55f : face;
            c.a = Mathf.Clamp01(-edge * 256); return c;
        }, 128, true);
        static Sprite Arrow(string name, int direction) => Texture(name, (x,y) =>
        {
            Vector2 p = direction == 1 ? new Vector2(y,-x) : direction == 2 ? new Vector2(-x,-y) : direction == 3 ? new Vector2(-y,x) : new Vector2(x,y);
            bool shaft = Mathf.Abs(p.x) < .065f && p.y > -.32f && p.y < .13f;
            bool head = p.y > .02f && p.y < .35f && Mathf.Abs(p.x) < .35f - p.y;
            return new Color(1,1,1,shaft || head ? 1 : 0);
        });
        static Sprite Texture(string name, Func<float,float,Color> paint, int size = 256, bool sliced = false)
        {
            string path = Art + "/" + name + ".png";
            if (!File.Exists(path))
            {
                var texture = new Texture2D(size,size,TextureFormat.RGBA32,false);
                var pixels = new Color[size*size];
                for (int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=paint((x+.5f)/size-.5f,(y+.5f)/size-.5f);
                texture.SetPixels(pixels); texture.Apply(); File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
                importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.filterMode=FilterMode.Bilinear;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                if(sliced)importer.spriteBorder=new Vector4(18,18,18,18);
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
#endif
