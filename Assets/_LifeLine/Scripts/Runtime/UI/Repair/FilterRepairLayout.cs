using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lifeline
{
    /// <summary>Uses authored pipe artwork to display orientation and which cells carry water.</summary>
    public sealed class FilterRepairLayout : MonoBehaviour
    {
        public float preferredHeight = 374;
        public int authoredColumns = 5, authoredRows = 4;
        public RectTransform grid, inlet, outlet;
        public Image inletArt, outletArt, testPressureArt;
        public Text inletLabel, outletLabel;
        public Button testPressure;
        public FilterPipeTile tilePrefab;
        public List<FilterPipeTile> tiles = new();
        public Color inletColor = new Color32(136, 230, 181, 255);
        public Color inactiveOutletColor = new Color32(255, 201, 131, 255);
        public Color connectedOutletColor = new Color32(122, 251, 186, 255);
        public Color readyPressureColor = new Color32(155, 238, 186, 255);
        int columns, rows;

        public void Bind(RepairBoardView view)
        {
            var puzzle = view.hud.filterPuzzle;
            if (puzzle.rows < 1 || puzzle.columns < 1) return;
            if (puzzle.masks == null || puzzle.masks.Length != puzzle.rows * puzzle.columns)
                puzzle.Generate(view.session.worldSeed + 31);
            ConfigureTiles(puzzle.columns, puzzle.rows);
            for (int i = 0; i < puzzle.masks.Length; i++)
            {
                int slot = i;
                view.Bind("Pipe " + slot, tiles[slot].button, () =>
                {
                    puzzle.RotateTile(slot);
                    Refresh(puzzle, view.theme);
                });
            }
            view.Bind("Test pressure", testPressure, () =>
            {
                if (puzzle.IsSolved())
                {
                    view.session.CompleteRepair(StationKind.Filter);
                    view.panel.RebuildRepair();
                }
                else view.session.Tell("尚未连通出口，继续调整。");
            });
            Refresh(puzzle, view.theme);
        }

        void ConfigureTiles(int newColumns, int newRows)
        {
            if (columns == newColumns && rows == newRows) return;
            int count = newColumns * newRows;
            bool keepAuthoredAnchors = columns == 0 && newColumns == authoredColumns && newRows == authoredRows && tiles.Count >= count;
            columns = newColumns;
            rows = newRows;
            while (tiles.Count < count) tiles.Add(Instantiate(tilePrefab, grid, false));
            for (int i = 0; i < tiles.Count; i++)
            {
                tiles[i].gameObject.SetActive(i < count);
                if (i < count && !keepAuthoredAnchors)
                    PositionTile((RectTransform)tiles[i].transform, i, columns, rows);
            }
        }

        static void PositionPort(RectTransform port, int row, int rowCount)
        {
            float y = 1 - (row + .5f) / rowCount;
            port.anchorMin = new Vector2(port.anchorMin.x, y);
            port.anchorMax = new Vector2(port.anchorMax.x, y);
        }

        public static void PositionTile(RectTransform tile, int index, int columns, int rows)
        {
            int row = index / columns, column = index % columns;
            tile.anchorMin = new Vector2(column / (float)columns, 1 - (row + 1) / (float)rows);
            tile.anchorMax = new Vector2((column + 1) / (float)columns, 1 - row / (float)rows);
            tile.offsetMin = tile.offsetMax = Vector2.zero;
        }

        void Refresh(PipeRepairPuzzle puzzle, RepairVisualTheme theme)
        {
            bool[] reached = puzzle.FlowCells();
            for (int i = 0; i < puzzle.masks.Length; i++)
            {
                Sprite[] library = reached[i] ? theme.flowingPipes : theme.dryPipes;
                tiles[i].channels.sprite = library[puzzle.masks[i] & 15];
            }
            PositionPort(inlet, puzzle.sourceRow, rows);
            PositionPort(outlet, puzzle.exitRow, rows);
            bool connected = puzzle.IsSolved();
            inletArt.color = inletLabel.color = inletColor;
            outletArt.color = outletLabel.color = connected ? connectedOutletColor : inactiveOutletColor;
            testPressureArt.color = connected ? readyPressureColor : Color.white;
        }
    }
}
