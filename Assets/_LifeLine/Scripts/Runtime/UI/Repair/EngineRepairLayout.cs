using System;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Links the authored engine service panel to the current four faults.</summary>
    public sealed class EngineRepairLayout : MonoBehaviour
    {
        [Serializable]
        public sealed class FaultPort
        {
            public Image housing, symbol;
            public EngineOilScrub oil;
            public Button select;
            public Text status;
        }

        [Serializable]
        public sealed class ReplacementPart
        {
            public Button install;
            public Image symbol;
            public Text actionText;
        }

        public float preferredHeight = 438;
        public Color selectedPort = new Color32(255, 208, 129, 255);
        public Color fixedPort = new Color32(151, 227, 169, 255);
        public FaultPort[] faults;
        public ReplacementPart[] parts;
        RepairBoardView view;

        public void Bind(RepairBoardView owner)
        {
            view = owner;
            for (int index = 0; index < faults.Length; index++)
            {
                int slot = index;
                var port = faults[index];
                port.oil.view = owner;
                port.oil.fault = slot;
                owner.Bind("Select fault " + slot, port.select, () => { owner.panel.selectedFault = slot; Refresh(); });
            }
            for (int index = 0; index < parts.Length; index++)
            {
                int slot = index;
                var part = parts[index];
                part.symbol.sprite = owner.theme.engineSymbols[index];
                string key = owner.actor?.input != null ? owner.actor.input.GetBindingLabel("Move", index + 1) : "";
                part.actionText.text = UiLocalization.Text(RepairBoardView.Parts[index]) + " [" + key + "]";
                owner.Bind("Install part " + slot, part.install, () =>
                {
                    owner.session.TryRepairEngineFault((int)owner.panel.selectedFault, slot);
                    owner.panel.RebuildRepair();
                });
            }
            Refresh();
        }

        void Update()
        {
            if (view != null) Refresh();
        }

        void Refresh()
        {
            var session = view.session;
            for (int index = 0; index < faults.Length; index++)
            {
                var port = faults[index];
                bool repaired = session.engineFaultFixed[index];
                float cleaned = Mathf.Clamp01(session.engineFaultClean[index]);
                int type = session.engineFaultTypes[index];
                port.housing.color = repaired ? fixedPort : (int)view.panel.selectedFault == index ? selectedPort : Color.white;
                port.symbol.sprite = view.theme.engineSymbols[type];
                port.symbol.enabled = cleaned >= 1 && port.symbol.sprite != null;
                port.oil.art.enabled = cleaned < 1;
                var oil = port.oil.art.color;
                oil.a = 1 - cleaned;
                port.oil.art.color = oil;
                port.oil.art.raycastTarget = !repaired && cleaned < 1;
                string fault = repaired ? "已修复" : cleaned >= 1 ? RepairBoardView.Faults[type] : "油污遮挡";
                port.status.text = UiLocalization.Text((index + 1) + " · " + fault + "  " + cleaned.ToString("P0"));
            }
        }
    }
}
