using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lifeline
{
    public partial class WorldGenerator
    {
        public HideSpotSaveState[] CaptureHideSpots()
        {
            var saved = new List<HideSpotSaveState>();
            foreach (var hide in hideSpots)
                if (hide != null) saved.Add(new HideSpotSaveState
                { position = hide.transform.position, mazeIndex = hide.mazeIndex, mazeCell = hide.mazeCell, range = hide.range });
            return saved.ToArray();
        }

        public void RestoreHideSpots(HideSpotSaveState[] saved)
        {
            // Validate every reference before replacing the generated snapshot.
            foreach (var hide in saved)
                if (hide.mazeIndex >= 0 && (hide.mazeIndex >= layouts.Count || hide.mazeCell < 0 || hide.mazeCell >= layouts[hide.mazeIndex].Count))
                    throw new InvalidOperationException("存档世界数据不完整。");
            foreach (var hide in hideSpots) if (hide != null) RemoveObject(hide.gameObject);
            hideSpots.Clear();
            foreach (var state in saved)
            {
                var obj = new GameObject("Saved hiding spot / building " + state.mazeIndex + " / room " + state.mazeCell);
                obj.transform.SetParent(generatedRoot != null ? generatedRoot : transform, false);
                obj.transform.position = state.position;
                var hide = obj.AddComponent<UnderwaterHideSpot>();
                hide.world = this; hide.mazeIndex = state.mazeIndex; hide.mazeCell = state.mazeCell; hide.range = state.range;
                hideSpots.Add(hide);
            }
        }

        public void PreserveLegacyHideLocation(Vector3 position)
        {
            foreach (var hide in hideSpots)
                if (hide != null && Vector2.Distance(position, hide.transform.position) <= hide.range) return;
            // Before randomized placement each ordinary building had a hide at room zero.
            // Move one of its new spots back there so a hidden old-save player stays hidden.
            for (int index = 0; index < layouts.Count; index++)
            {
                var maze = layouts[index];
                if (maze.entranceOnLeft) continue;
                int oldCell = maze.entranceCell == 0 ? Mathf.Min(maze.Count - 1, maze.columns + 1) : 0;
                Vector3 oldPosition = maze.Center(oldCell);
                if (Vector2.Distance(position, oldPosition) > 1.8f) continue;
                UnderwaterHideSpot nearest = null; float distance = float.PositiveInfinity;
                foreach (var hide in hideSpots)
                    if (hide != null && hide.mazeIndex == index && (hide.transform.position - oldPosition).sqrMagnitude < distance)
                    { nearest = hide; distance = (hide.transform.position - oldPosition).sqrMagnitude; }
                if (nearest != null)
                { nearest.transform.position = oldPosition; nearest.mazeCell = oldCell; nearest.range = Mathf.Max(nearest.range, 1.8f); return; }
            }
        }
    }
}
