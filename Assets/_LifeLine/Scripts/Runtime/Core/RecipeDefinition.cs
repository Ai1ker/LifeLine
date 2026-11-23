using UnityEngine;

namespace Lifeline
{
    [CreateAssetMenu(fileName = "Recipe", menuName = "LifeLine/Recipe")]
    public class RecipeDefinition : ScriptableObject
    {
        public string id;
        [Tooltip("May be queued as a batch at a T2 or higher workbench.")] public bool allowBatch;
        [Tooltip("Hide this recipe at the workbench once its equipment or radio stage is complete.")]
        public bool hideWhenCompleted;
        public string displayName;
        [TextArea] public string description;
        public ResourceAmount[] cost = new ResourceAmount[0];
        public ResourceAmount[] output = new ResourceAmount[0];
        [Tooltip("rod, harpoon, speargun, generator, repair-bench. Empty means no unlock.")] public string toolId;
        [Tooltip("Tool/equipment IDs required when the job starts. They remain available unless also listed below.")]
        public string[] requiredToolIds = new string[0];
        [Tooltip("Tool/equipment IDs consumed atomically when the job starts. Canceling does not refund them.")]
        public string[] consumedToolIds = new string[0];
        [Tooltip("0=ordinary recipe, 1=antenna, 2=receiver, 3=amplifier.")] public int radioStage;
        public float repairAmount;
        [Min(.01f), Tooltip("Multiplier applied after workbench duration upgrades. Quick hull repair uses 0.5; ordinary recipes use 1.")]
        public float craftTimeMultiplier = 1f;
        public Sprite icon;
        public GameObject completedVisualPrefab;
    }
}
