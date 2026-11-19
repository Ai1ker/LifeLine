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
            EnsureBackpackAction(draft);
            MigrateInteractionBindings(draft, true);

            // Stable IDs keep PlayerPrefs overrides valid even when the runtime fallback is used.
            var json = draft.ToJson();
            var id = 1;
            int addedId = 920;
            foreach (var map in draft.actionMaps)
            {
                json = ReplaceId(json, map.id, id++);
                foreach (var action in map.actions)
                    json = ReplaceId(json, action.id, action.name == "Backpack" ? 900 : AddedAction(action.name) ? addedId++ : id++);
                foreach (var binding in map.bindings)
                    json = ReplaceId(json, binding.id, binding.action == "Backpack" ? 901 : AddedAction(binding.action) ? addedId++ : id++);
            }
            if (Application.isPlaying) UnityEngine.Object.Destroy(draft);
            else UnityEngine.Object.DestroyImmediate(draft);
            var asset = InputActionAsset.FromJson(json);
            asset.name = AssetName;
            MigrateInteractionBindings(asset, true);
            return asset;
        }

        static bool AddedAction(string action) => action == "Calibrate" || action == "QteQ" || action == "QteE";

        public static bool EnsureBackpackAction(InputActionAsset asset)
        {
            var captain = asset?.FindActionMap("Captain", false);
            if (captain == null || captain.FindAction("Backpack", false) != null) return false;
            Button(captain, "Backpack", "b");
            return true;
        }

        /// <summary>Keep existing binding IDs while adding optional, independently rebindable device actions.</summary>
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
            foreach (var role in new[] { CrewRole.Captain, CrewRole.Diver })
            {
                var map = asset.FindActionMap(role.ToString(), false);
                if (map == null) continue;
                bool captain = role == CrewRole.Captain;
                Ensure(map, "Calibrate", captain ? Key("space") : Key("0"), apply, changes);
                Ensure(map, "QteQ", captain ? Key("q") : Key("numpad7"), apply, changes);
                Ensure(map, "QteE", captain ? Key("e") : Key("numpad9"), apply, changes);
                Ensure(map, "Hide", "", apply, changes);
                ClearDefault(map, "Hide", Key(captain ? "c" : "m"), apply, changes);
                ClearDefault(map, "Melee", Key(captain ? "space" : "0"), apply, changes);
            }
            return changes;
        }

        static void Ensure(InputActionMap map, string name, string path, bool apply, List<string> changes)
        {
            if (map.FindAction(name, false) != null) return;
            changes.Add(map.name + "/" + name);
            if (!apply) return;
            var action = map.AddAction(name, InputActionType.Button);
            action.expectedControlType = "Button";
            action.AddBinding(new InputBinding { path = path });
        }

        static void ClearDefault(InputActionMap map, string name, string oldPath, bool apply, List<string> changes)
        {
            var action = map.FindAction(name, false);
            if (action == null || action.bindings.Count == 0 || action.bindings[0].path != oldPath) return;
            changes.Add(map.name + "/" + name + " uses its shared key by default");
            if (apply) action.ChangeBinding(0).WithPath("");
        }

        public static string NoteAction(int note) => note == 4 ? "QteQ" : note == 5 ? "QteE" : "Move";
        public static int NoteBinding(int note) => note < 4 ? note + 1 : 0;
        public static int DirectionNote(Vector2 move) => Mathf.Abs(move.y) >= Mathf.Abs(move.x)
            ? (move.y > 0 ? 0 : 2) : (move.x < 0 ? 1 : 3);

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
