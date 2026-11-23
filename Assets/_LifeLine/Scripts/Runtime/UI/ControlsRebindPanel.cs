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
        public CrewInputReader onlineInput;
        public bool IsOpen => panel != null && panel.activeSelf;
        public bool IsRebinding => rebinding || (Selected != null && Selected.IsRebinding);
        private bool rebinding;
        private int lastEscapeFrame = -1, lastRebindEndedFrame = -1;
        public List<Button> rowWidgets = new();
        private CrewInputReader Selected => onlineInput != null ? onlineInput : selectedRole == CrewRole.Captain ? captainInput : diverInput;
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
            panel.SetActive(true); panel.transform.SetAsLastSibling(); GameHUDController.ConfigureModalLayer(panel,130);
            selectedRole = onlineInput != null ? onlineInput.role : CrewRole.Captain;
            if (captainTab != null) captainTab.gameObject.SetActive(onlineInput == null);
            if (diverTab != null) diverTab.gameObject.SetActive(onlineInput == null);
            BuildRows(); UiLocalization.Refresh(panel);
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
            int visibleRows = 0;
            if (Selected == null)
            {
                HideUnusedRows(0);
                return;
            }
            statusText.text = GameNames.Role(selectedRole) + " · 点击一个绑定后按新键；Esc 取消。";
            foreach (InputAction action in Selected.BindableActions)
            {
                if (action.name == "Backpack" && (onlineInput == null || onlineInput.role != CrewRole.Diver)) continue;
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    if (action.bindings[i].isComposite) continue;
                    int index = i; string actionName = action.name;
                    string partName=action.bindings[i].name;
                    string part = action.bindings[i].isPartOfComposite ? "/" + (LocalizationManager.Current!=null?LocalizationManager.Current.Format("input."+partName.ToLowerInvariant()):partName) : "";
                    Button row = RowAt(visibleRows++);
                    row.gameObject.SetActive(true);
                    Text label = row.GetComponentInChildren<Text>();
                    string binding = Selected.GetBindingLabel(actionName, index);
                    string actionLabel = actionName;
                    if (actionName == "Hide" || actionName == "Melee" || actionName == "Calibrate" || actionName == "QteQ" || actionName == "QteE")
                    {
                        actionLabel = UiLocalization.Text(actionName == "Hide" ? "藏匿互动" : actionName == "Melee" ? "近战攻击" :
                            actionName == "Calibrate" ? "节奏校准" : actionName == "QteQ" ? "节奏音符 Q" : "节奏音符 E");
                        if (!Selected.HasBinding(actionName))
                            binding = UiLocalization.Text(actionName == "Hide" ? "与互动共用" : "与校准共用");
                    }
                    label.text = UiLocalization.Text(actionLabel + part + "    [ " + binding + " ]");
                    row.onClick.RemoveAllListeners();
                    row.onClick.AddListener(() =>
                    {
                        if (rebinding) return;
                        rebinding = true; statusText.text = "请按新键 · Esc 取消";
                        Selected.BeginRebind(actionName, index, result => { rebinding = false; lastRebindEndedFrame = Time.frameCount; BuildRows(); statusText.text = UiLocalization.Text(result); });
                    });
                }
            }
            HideUnusedRows(visibleRows);
            UiLocalization.Refresh(panel);
        }

        Button RowAt(int index)
        {
            if (index == rowWidgets.Count) rowWidgets.Add(Instantiate(rowButtonPrefab, rowsRoot));
            return rowWidgets[index];
        }

        void HideUnusedRows(int firstHidden)
        {
            for (int index = firstHidden; index < rowWidgets.Count; index++)
                rowWidgets[index].gameObject.SetActive(false);
        }
    }
}
