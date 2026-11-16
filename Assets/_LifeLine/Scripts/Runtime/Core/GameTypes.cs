using System;
using UnityEngine;

namespace Lifeline
{
    public enum CrewRole { Captain, Diver }
    public enum ResourceKind { Wood, Metal, Fiber, Fish, Bait, Plank, Plate, FiberBundle, Fuel, Eel }
    public enum StationKind { Workbench, TotalControl, Helm, Winch, DivePreparation, Searchlight, Fishing, HullRepair, Filter, Circuit, Generator, Engine, Turret }
    public enum UpgradeKind { Rope, Oxygen, Hull, Sail, Searchlight, Workbench, Backpack, DivingSuit }
    public enum SessionPhase { Menu, Playing, Paused, Won, Lost }

    [Serializable]
    public struct CrewCommand
    {
        public CrewRole role;
        public Vector2 move;
        public bool jump, interact, collect, melee, fire, sprint, signal, bait, hide, pause;
        [Tooltip("Continuous interaction state, used by five-second teammate revives. interact remains an edge.")]
        public bool interactHeld;
    }

    public interface ICrewInputSource
    {
        CrewCommand ReadCommand();
        void SetEnabled(bool enabled);
    }

    [Serializable]
    public class ResourceAmount
    {
        public ResourceKind resource;
        [Min(0)] public int amount;
        public ResourceAmount() { }
        public ResourceAmount(ResourceKind kind, int count) { resource = kind; amount = count; }
    }

    public static class GameNames
    {
        public static string Resource(ResourceKind kind)
        {
            switch (kind)
            {
                case ResourceKind.Wood: return "木材";
                case ResourceKind.Metal: return "金属";
                case ResourceKind.Fiber: return "纤维";
                case ResourceKind.Fish: return "鱼获";
                case ResourceKind.Bait: return "诱饵";
                case ResourceKind.Plank: return "木板";
                case ResourceKind.Plate: return "铁板";
                case ResourceKind.FiberBundle: return "纤维束";
                case ResourceKind.Fuel: return "燃油";
                default: return "电鳗";
            }
        }
        public static string Role(CrewRole role) => role == CrewRole.Captain ? "船长" : "潜水员";
        public static string Station(StationKind kind)
        {
            switch (kind)
            {
                case StationKind.Workbench: return "制作台";
                case StationKind.TotalControl: return "设备总控";
                case StationKind.Helm: return "航行船舵";
                case StationKind.Winch: return "救援绞盘";
                case StationKind.DivePreparation: return "潜水准备台";
                case StationKind.Searchlight: return "探照灯";
                case StationKind.Fishing: return "垂钓点";
                case StationKind.HullRepair: return "船体维修台";
                case StationKind.Filter: return "海水过滤器";
                case StationKind.Circuit: return "电路系统";
                case StationKind.Generator: return "动力发电机";
                case StationKind.Engine: return "船用引擎";
                default: return "甲板炮塔";
            }
        }
    }
}
