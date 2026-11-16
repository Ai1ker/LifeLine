using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lifeline
{
    public class ControlsRebindPanel : MonoBehaviour
    {
        public CrewInputReader captainInput, diverInput;
        public RectTransform rowsRoot;
        public Button rowButtonPrefab;
        public Text statusText;
        public GameObject panel;
        public Button captainTab, diverTab, resetButton, closeButton;
        public CrewRole selectedRole;
        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsRebinding => rebinding || (Selected != null && Selected.IsRebinding);
        private bool rebinding;
        private int lastEscapeFrame = -1, lastRebindEndedFrame = -1;
        private readonly List<GameObject> rows = new List<GameObject>();
        private CrewInputReader Selected => selectedRole == CrewRole.Captain ? captainInput : diverInput;
        private void Awake()
        {
            if (captainTab != null) captainTab.onClick.AddListener(() => Select(CrewRole.Captain));
            if (diverTab != null) diverTab.onClick.AddListener(() => Select(CrewRole.Diver));
            if (resetButton != null) resetButton.onClick.AddListener(() => { if (!rebinding && Selected != null) { Selected.ResetBindings(); BuildRows(); } });
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }
        public void Open()
        {
            if (panel == null) return;
            panel.SetActive(true); panel.transform.SetAsLastSibling(); selectedRole = CrewRole.Captain; BuildRows(); UiLocalization.Refresh(panel);
        }
        public void Close()
        {
            if (IsRebinding) Selected?.CancelRebind();
            rebinding = false;
            if (panel != null) panel.SetActive(false);
        }
        public bool HandleEscape()
        {
            if (!IsOpen) return false;
            if (lastEscapeFrame == Time.frameCount) return true;
            lastEscapeFrame = Time.frameCount;
            if (IsRebinding) { Selected?.CancelRebind(); rebinding = false; return true; }
            // InputSystem may finish its Escape cancellation before the peer's Pause action is delivered.
            if (lastRebindEndedFrame == Time.frameCount) return true;
            Close(); return true;
        }
        private void Select(CrewRole role) { if (!rebinding) { selectedRole = role; BuildRows(); } }
        private void BuildRows()
        {
            foreach (GameObject row in rows) if(row!=null){row.SetActive(false);Destroy(row);} rows.Clear();
            if (Selected == null) return;
            statusText.text = GameNames.Role(selectedRole) + " · 点击一个绑定后按新键；Esc 取消。";
            foreach (InputAction action in Selected.BindableActions)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action.bindings[i].isComposite) continue;
                    int index = i; string actionName = action.name;
                    string partName=action.bindings[i].name;
                    string part = action.bindings[i].isPartOfComposite ? "/" + (LocalizationManager.Current!=null?LocalizationManager.Current.Format("input."+partName.ToLowerInvariant()):partName) : "";
                    Button row = Instantiate(rowButtonPrefab, rowsRoot); row.gameObject.SetActive(true); rows.Add(row.gameObject);
                    Text label = row.GetComponentInChildren<Text>();
                    label.text = UiLocalization.Text(actionName + part + "    [ " + Selected.GetBindingLabel(actionName, index) + " ]");
                    row.onClick.RemoveAllListeners();
                    row.onClick.AddListener(() =>
                    {
                        if (rebinding) return;
                        rebinding = true; statusText.text = "请按新键 · Esc 取消";
                        Selected.BeginRebind(actionName, index, result => { rebinding = false; lastRebindEndedFrame = Time.frameCount; BuildRows(); statusText.text = UiLocalization.Text(result); });
                    });
                }
            }
            UiLocalization.Refresh(panel);
        }
    }
}
