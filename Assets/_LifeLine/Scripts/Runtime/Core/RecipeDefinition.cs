using UnityEngine;

namespace Lifeline
{
    [CreateAssetMenu(fileName = "Recipe", menuName = "LifeLine/Recipe")]
    public class RecipeDefinition : ScriptableObject
    {
        public string id;
        public string displayName;
        [TextArea] public string description;
        public ResourceAmount[] cost = new ResourceAmount[0];
        public ResourceAmount[] output = new ResourceAmount[0];
        [Tooltip("rod, harpoon, speargun. Empty means no tool unlock.")] public string toolId;
        [Tooltip("0=ordinary recipe, 1=antenna, 2=receiver, 3=amplifier.")] public int radioStage;
        public float repairAmount;
        [Min(.01f), Tooltip("Multiplier applied after workbench duration upgrades. Quick hull repair uses 0.5; ordinary recipes use 1.")]
        public float craftTimeMultiplier = 1f;
        public Sprite icon;
        public GameObject completedVisualPrefab;
    }
}
