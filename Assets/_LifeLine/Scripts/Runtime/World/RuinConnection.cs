using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    /// <summary>A real open-water route between two ruin side portals, not a teleport.</summary>
    [Serializable]
    public class RuinConnection
    {
        public string name;
        public int fromMaze, fromCell, toMaze, toCell;
        public Vector3 from, to;
        public List<Vector3> waypoints = new List<Vector3>();
    }
}
