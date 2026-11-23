using UnityEngine;
namespace Lifeline
{
    [CreateAssetMenu(fileName="RepairVisualTheme",menuName="LifeLine/UI/Repair Visual Theme")]
    public sealed class RepairVisualTheme : ScriptableObject
    {
        public Sprite board, terminal, oilPatch, engineBody, pipeTile;
        public Sprite[] circuitSymbols=new Sprite[4];
        public Sprite[] engineSymbols=new Sprite[4];
        [Tooltip("Indexed by the pipe exit mask: north 1, east 2, south 4, west 8.")]
        public Sprite[] dryPipes = new Sprite[16], flowingPipes = new Sprite[16];
        public Color background=new Color(.06f,.15f,.19f), ink=new Color(.85f,.94f,.91f);
        public Color flowing=new Color(.38f,.88f,.65f), pipe=new Color(.55f,.68f,.71f), oil=new Color(.10f,.065f,.045f);
        public Color[] wires={new Color(.5f,.74f,.54f),new Color(.94f,.74f,.44f),new Color(.47f,.75f,.89f),new Color(.66f,.60f,.90f)};
    }
}
