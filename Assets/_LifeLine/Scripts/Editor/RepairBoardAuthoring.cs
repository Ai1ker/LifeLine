#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static Lifeline.NauticalUiSkin;
using Object = UnityEngine.Object;

namespace Lifeline.Editor
{
    internal static class RepairBoardAuthoring
    {
        public const string ThemePath = GameProjectBuilder.Root + "/Data/UI/RepairVisualTheme.asset";
        public const string BoardPath = GameProjectBuilder.Root + "/Prefabs/UI/RepairBoardView.prefab";
        public const string Folder = GameProjectBuilder.Root + "/Prefabs/UI/Repair";
        static readonly Color PanelText = new Color32(251, 230, 192, 255);
        static readonly string[] PartFiles = { "FuelHose", "IgnitionCoil", "DriveGear", "CoolantValve" };
        static readonly int[] PreviewPipeMasks = { 10, 12, 6, 10, 12, 5, 3, 9, 5, 3, 6, 12, 6, 3, 12, 5, 3, 9, 10, 9 };

        public static void Install()
        {
            RepairArtworkImporter.Reimport();
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(GameProjectBuilder.Root + "/Prefabs/UI", "Repair");
            var theme = AssetDatabase.LoadAssetAtPath<RepairVisualTheme>(ThemePath);
            if (theme == null) throw new InvalidOperationException("RepairVisualTheme.asset is missing.");
            BindTheme(theme);
            var engine = Prefab("EngineRepairBoard", () => CreateEngine(theme));
            var tile = Prefab("FilterPipeTile", () => CreateTile(theme));
            var filter = Prefab("FilterRepairBoard", () => CreateFilter(theme, tile));
            var root = PrefabUtility.LoadPrefabContents(BoardPath);
            try
            {
                var view = root.GetComponent<RepairBoardView>();
                if (view.engineLayout == null) view.engineLayout = LinkLayout<EngineRepairLayout>(engine, root.transform);
                if (view.filterLayout == null) view.filterLayout = LinkLayout<FilterRepairLayout>(filter, root.transform);
                if (view.theme == null) view.theme = theme;
                if (root.GetComponent<LayoutElement>() == null) root.AddComponent<LayoutElement>();
                PrefabUtility.SaveAsPrefabAsset(root, BoardPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void BindTheme(RepairVisualTheme theme)
        {
            if (theme.engineSymbols == null || theme.engineSymbols.Length != PartFiles.Length) theme.engineSymbols = new Sprite[PartFiles.Length];
            for (int type = 0; type < PartFiles.Length; type++)
                if (theme.engineSymbols[type] == null) theme.engineSymbols[type] = Art(PartFiles[type]);
            theme.dryPipes = BindMasks(theme.dryPipes, "Dry");
            theme.flowingPipes = BindMasks(theme.flowingPipes, "Flow");
            EditorUtility.SetDirty(theme);
        }

        static Sprite[] BindMasks(Sprite[] sprites, string style)
        {
            if (sprites == null || sprites.Length != 16) sprites = new Sprite[16];
            for (int mask = 0; mask < sprites.Length; mask++)
                if (sprites[mask] == null) sprites[mask] = Art(style + "/" + mask.ToString("00"));
            return sprites;
        }

        static T LinkLayout<T>(GameObject prefab, Transform parent) where T : Component
        {
            var layout = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            layout.SetActive(false);
            return layout.GetComponent<T>();
        }

        static Sprite Art(string name)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(RepairArtworkImporter.Folder + "/" + name + ".png");
            return sprite != null ? sprite : throw new InvalidOperationException("Missing repair sprite: " + name);
        }

        static GameObject CreateEngine(RepairVisualTheme theme)
        {
            var root = Rect(null, "EngineRepairBoard", Vector2.zero, Vector2.one);
            PanelArt(root);
            var view = root.gameObject.AddComponent<EngineRepairLayout>();
            Instruction(root, "按住油污擦拭，或按住互动键清理当前故障；露出符号后安装对应零件。");
            var chassis = Rect(root, "Engine service bay", new Vector2(.035f,.474f), new Vector2(.965f,.88f));
            PanelArt(chassis, "EngineSocket");
            var engineSprite = theme.engineBody != null ? theme.engineBody :
                AssetDatabase.LoadAssetAtPath<Sprite>(GameProjectBuilder.Root + "/Art/Stations/StaticSprites/Engine.png");
            PutArt(chassis, "Ship engine illustration", engineSprite, new Vector2(.029f,.09f), new Vector2(.48f,.92f), true);
            view.faults = new EngineRepairLayout.FaultPort[4];
            view.parts = new EngineRepairLayout.ReplacementPart[4];
            for (int index = 0; index < 4; index++)
            {
                float portX = .51f + index % 2 * .235f;
                float portY = .55f - index / 2 * .45f;
                var socket = Rect(chassis, "Fault port " + (index+1), new Vector2(portX,portY), new Vector2(portX+.212f,portY+.36f));
                var housing = PanelArt(socket, "EngineSocket");
                var badge = Label(socket, "Fault number", 13, TextAnchor.MiddleCenter);
                badge.text = (index + 1).ToString();
                badge.color = PanelText;
                Stretch(badge.rectTransform, new Vector2(.04f,.57f), new Vector2(.23f,.95f));
                var symbol = PutArt(socket, "Revealed fault sprite", theme.engineSymbols[index], new Vector2(.22f,.11f), new Vector2(.90f,.89f), true);
                var oil = PutArt(socket, "Oil to scrub", Art("OilSmear"), new Vector2(.19f,.02f), new Vector2(.95f,.98f), false);
                oil.raycastTarget = true;
                var scrub = oil.gameObject.AddComponent<EngineOilScrub>();
                scrub.art = oil;

                Vector2 offset = new Vector2(.042f + index % 2 * .469f, .375f - index / 2 * .096f);
                var select = BoardButton(root, "Select fault " + index, (index + 1) + " · 油污遮挡  0%", offset, offset + new Vector2(.446f,.082f), out Text status);
                view.faults[index] = new EngineRepairLayout.FaultPort { housing = housing, symbol = symbol, oil = scrub, select = select, status = status };

                offset.y = .146f - index / 2 * .104f;
                var install = BoardButton(root, "Install part " + index, RepairBoardView.Parts[index], offset, offset + new Vector2(.446f,.09f), out Text partText);
                var partIcon = PutArt(install.transform, "Part sprite", theme.engineSymbols[index], new Vector2(.03f,.1f), new Vector2(.19f,.9f), true);
                Stretch(partText.rectTransform, new Vector2(.17f,.03f), new Vector2(.97f,.97f));
                view.parts[index] = new EngineRepairLayout.ReplacementPart { symbol = partIcon, install = install, actionText = partText };
            }
            return root.gameObject;
        }

        static GameObject CreateTile(RepairVisualTheme theme)
        {
            var root = Rect(null, "FilterPipeTile", Vector2.zero, Vector2.one);
            var tile = root.gameObject.AddComponent<FilterPipeTile>();
            var sprite = PanelArt(root, "PipeTile");
            sprite.raycastTarget = true;
            tile.button = root.gameObject.AddComponent<Button>();
            tile.button.targetGraphic = sprite;
            tile.button.colors = ColorBlock.defaultColorBlock;
            tile.channels = PutArt(root, "Pipe connection sprite", theme.dryPipes[PipeRepairPuzzle.North | PipeRepairPuzzle.South],
                Vector2.zero, Vector2.one, false);
            return root.gameObject;
        }

        static GameObject CreateFilter(RepairVisualTheme theme, GameObject tilePrefab)
        {
            var root = Rect(null, "FilterRepairBoard", Vector2.zero, Vector2.one);
            PanelArt(root);
            var view = root.gameObject.AddComponent<FilterRepairLayout>();
            view.tilePrefab = tilePrefab.GetComponent<FilterPipeTile>();
            Instruction(root, "旋转管道：绿色表示已经从入口流通，连接出口后加压试水。");
            var rails = Rect(root, "Pipe manifold", new Vector2(.033f,.177f), new Vector2(.967f,.877f));
            view.grid = Rect(rails, "Pipe grid", new Vector2(.147f,0), new Vector2(.853f,1));
            for (int index = 0; index < view.authoredColumns * view.authoredRows; index++)
            {
                var item = (GameObject)PrefabUtility.InstantiatePrefab(tilePrefab, view.grid);
                item.name = "Pipe " + index;
                FilterRepairLayout.PositionTile((RectTransform)item.transform, index, view.authoredColumns, view.authoredRows);
                var tile = item.GetComponent<FilterPipeTile>();
                tile.channels.sprite = index == 0 ? theme.flowingPipes[PreviewPipeMasks[index]] : theme.dryPipes[PreviewPipeMasks[index]];
                PrefabUtility.RecordPrefabInstancePropertyModifications(tile.channels);
                view.tiles.Add(tile);
            }
            view.inlet = Port(rails, "Inlet", .0735f, .875f, "入口 →", view.inletColor, out view.inletArt, out view.inletLabel);
            view.outlet = Port(rails, "Outlet", .9265f, .125f, "→ 出口", view.inactiveOutletColor, out view.outletArt, out view.outletLabel);
            view.testPressure = BoardButton(root, "Test pressure", "加压试水",
                new Vector2(.20f,.033f), new Vector2(.80f,.135f), out _);
            view.testPressureArt = view.testPressure.GetComponent<Image>();
            return root.gameObject;
        }

        static RectTransform Port(Transform rails, string name, float x, float y, string title, Color color, out Image art, out Text label)
        {
            var port = Rect(rails, name, Vector2.zero, Vector2.one);
            Vector2 anchor = new Vector2(x,y);
            Pin(port, anchor, new Vector2(.5f,.5f), Vector2.zero, new Vector2(78,54));
            art = PutArt(port, "Manifold coupling", Art("ManifoldArrow"), new Vector2(.18f,.33f), new Vector2(.82f,.95f), true);
            art.color = color;
            label = Label(port, "Port direction", 13, TextAnchor.MiddleCenter);
            label.text = title;
            label.color = color;
            Stretch(label.rectTransform, Vector2.zero, new Vector2(1,.38f));
            return port;
        }

        static void Instruction(RectTransform root, string text)
        {
            var label = Label(root, "Instruction", 16, TextAnchor.MiddleCenter);
            label.color = PanelText;
            label.text = text;
            Stretch(label.rectTransform, new Vector2(.035f,.895f), new Vector2(.965f,.98f));
        }

        static Image PanelArt(RectTransform rect, string name = "ServicePanel")
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Art(name);
            image.type = image.sprite.border == Vector4.zero ? UnityEngine.UI.Image.Type.Simple : UnityEngine.UI.Image.Type.Sliced;
            image.raycastTarget = false;
            return image;
        }

        static Image PutArt(Transform parent, string name, Sprite sprite, Vector2 min, Vector2 max, bool aspect)
        {
            var art = Image(parent, name);
            art.sprite = sprite;
            art.preserveAspect = aspect;
            Stretch(art.rectTransform, min, max);
            return art;
        }

        static Button BoardButton(Transform parent, string name, string text, Vector2 min, Vector2 max, out Text label)
        {
            var rect = Rect(parent, name, min, max);
            var image = rect.gameObject.AddComponent<Image>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            label = Label(rect, "Label", 16, TextAnchor.MiddleCenter);
            label.text = text;
            StyleButton(button, 16);
            return button;
        }

        static GameObject Prefab(string name, Func<GameObject> build)
        {
            string path = Folder + "/" + name + ".prefab";
            var old = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (old != null) return old;
            var root = build();
            try { return PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
#endif
