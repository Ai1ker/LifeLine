using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lifeline
{
    /// <summary>One shared keyboard, two independent maps. The editor writes this as an editable .inputactions asset.</summary>
    public static class CrewInputDefaults
    {
        public const string AssetName = "LifelineControls";

        public static InputActionAsset CreateAsset()
        {
            var draft = ScriptableObject.CreateInstance<InputActionAsset>();
            draft.name = AssetName;
            AddMap(draft, "Captain", new[] { "w", "a", "s", "d" }, "w", "e", "r", "space", "q", "leftShift", "z", "x", "c");
            AddMap(draft, "Diver", new[] { "upArrow", "leftArrow", "downArrow", "rightArrow" }, "upArrow", "enter", "1", "0", "2", "rightShift", "period", "slash", "m");

            // Stable IDs keep PlayerPrefs overrides valid even when the runtime fallback is used.
            var json = draft.ToJson();
            var id = 1;
            foreach (var map in draft.actionMaps)
            {
                json = ReplaceId(json, map.id, id++);
                foreach (var action in map.actions) json = ReplaceId(json, action.id, id++);
                foreach (var binding in map.bindings) json = ReplaceId(json, binding.id, id++);
            }
            if (Application.isPlaying) UnityEngine.Object.Destroy(draft);
            else UnityEngine.Object.DestroyImmediate(draft);
            var asset = InputActionAsset.FromJson(json);
            asset.name = AssetName;
            MigrateInteractionBindings(asset, true);
            return asset;
        }

        /// <summary>Retain IDs and custom overrides while retiring only the diver's separate hide action.</summary>
        public static List<string> MigrateInteractionBindings(InputActionAsset asset, bool apply)
        {
            var changes = new List<string>();
            if (asset == null) return changes;
            var collect = asset.FindAction("Captain/Collect", false);
            if (collect != null) for (int i = 0; i < collect.bindings.Count; i++)
            {
                if (collect.bindings[i].path != "<Keyboard>/r") continue;
                changes.Add("Captain/Collect: <Keyboard>/r -> <Keyboard>/e");
                if (apply) collect.ChangeBinding(i).WithPath("<Keyboard>/e");
            }
            var hide = asset.FindAction("Diver/Hide", false);
            if (hide != null)
            {
                changes.Add("Diver/Hide: removed; use Diver/Interact to enter or leave hiding");
                if (apply) hide.RemoveAction();
            }
            return changes;
        }

        private static string ReplaceId(string json, Guid original, int index)
        {
            return original == Guid.Empty ? json : json.Replace(original.ToString(), "14693dbe-c3f1-4a80-9f00-" + index.ToString("x12"));
        }

        private static void AddMap(InputActionAsset asset, string name, string[] directions, string jump,
            string interact, string collect, string melee, string fire, string sprint, string signal, string bait, string hide)
        {
            var map = new InputActionMap(name);
            var move = map.AddAction("Move", InputActionType.Value);
            move.expectedControlType = "Vector2";
            move.AddCompositeBinding("2DVector")
                .With("Up", Key(directions[0])).With("Left", Key(directions[1]))
                .With("Down", Key(directions[2])).With("Right", Key(directions[3]));
            Button(map, "Jump", jump);
            Button(map, "Interact", interact);
            Button(map, "Collect", collect);
            Button(map, "Melee", melee);
            Button(map, "Fire", fire);
            Button(map, "Sprint", sprint);
            Button(map, "Signal", signal);
            Button(map, "Bait", bait);
            Button(map, "Hide", hide);
            Button(map, "Pause", "escape");
            asset.AddActionMap(map);
        }

        private static string Key(string name) => "<Keyboard>/" + name;
        private static void Button(InputActionMap map, string name, string key)
        {
            var action = map.AddAction(name, InputActionType.Button, Key(key));
            action.expectedControlType = "Button";
        }
    }
}
