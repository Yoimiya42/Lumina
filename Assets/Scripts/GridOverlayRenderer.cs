using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders a grid overlay (cell borders) on top of a target RectTransform.
/// Each cell has a border object (4 thin Images). You can:
/// - Set cell as completed: hide border (or change color)
/// - Highlight a set of cells: change border color/width temporarily
/// </summary>
public class GridOverlayRenderer : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform targetRect; // usually ColorImage.rectTransform

    [Header("Grid Size")]
    [Min(1)][SerializeField] private int gridX = 16;
    [Min(1)][SerializeField] private int gridY = 16;

    [Header("Line Style")]
    [SerializeField] private float lineThickness = 2f;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color highlightColor = new Color(0.7f, 0.2f, 1f, 1f); // purple-ish
    [SerializeField] private Color completedColor = new Color(1f, 1f, 1f, 0f);     // transparent (hide effect)

    [Header("Behavior")]
    [Tooltip("If true, completed cell border objects will be disabled.")]
    [SerializeField] private bool disableOnComplete = true;

    private CellBorder[,] borders;
    private bool[,] completed;

    private void Awake()
    {
        if (targetRect == null)
        {
            enabled = false;
            return;
        }

        BuildGrid();
    }

    public void Configure(int gx, int gy)
    {
        gridX = Mathf.Max(1, gx);
        gridY = Mathf.Max(1, gy);
        Rebuild();
    }

    public void Rebuild()
    {
        // Destroy old
        if (borders != null)
        {
            for (int y = 0; y < borders.GetLength(1); y++)
                for (int x = 0; x < borders.GetLength(0); x++)
                    if (borders[x, y] != null)
                        Destroy(borders[x, y].gameObject);
        }

        BuildGrid();
    }

    private void BuildGrid()
    {
        borders = new CellBorder[gridX, gridY];
        completed = new bool[gridX, gridY];

        // Ensure this overlay matches target size
        RectTransform self = transform as RectTransform;
        self.anchorMin = Vector2.zero;
        self.anchorMax = Vector2.one;
        self.offsetMin = Vector2.zero;
        self.offsetMax = Vector2.zero;

        // Create per-cell border
        for (int y = 0; y < gridY; y++)
        {
            for (int x = 0; x < gridX; x++)
            {
                GameObject cellGO = new GameObject($"Cell_{x}_{y}", typeof(RectTransform));
                cellGO.transform.SetParent(transform, false);

                RectTransform rt = cellGO.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2((float)x / gridX, (float)y / gridY);
                rt.anchorMax = new Vector2((float)(x + 1) / gridX, (float)(y + 1) / gridY);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;

                CellBorder border = cellGO.AddComponent<CellBorder>();
                border.Init(lineThickness, normalColor);

                borders[x, y] = border;
                completed[x, y] = false;
            }
        }
    }

    public void SetCellCompleted(int x, int y, bool isCompleted)
    {
        if (!InRange(x, y)) return;

        completed[x, y] = isCompleted;

        var border = borders[x, y];
        if (border == null) return;

        if (disableOnComplete)
        {
            border.gameObject.SetActive(!isCompleted);
        }
        else
        {
            border.SetColor(isCompleted ? completedColor : normalColor);
        }
    }

    public void ClearHighlights()
    {
        for (int y = 0; y < gridY; y++)
            for (int x = 0; x < gridX; x++)
            {
                if (completed[x, y]) continue;

                var border = borders[x, y];
                if (border != null && border.gameObject.activeSelf)
                    border.SetColor(normalColor);
            }
    }


    public void HighlightCell(int x, int y)
    {
        if (!InRange(x, y)) return;
        if (completed[x, y]) return;
        borders[x, y]?.SetColor(highlightColor);
    }

    private bool InRange(int x, int y) => x >= 0 && x < gridX && y >= 0 && y < gridY;

    /// <summary>
    /// Helper component that creates 4 thin Image children as border lines.
    /// </summary>
    private class CellBorder : MonoBehaviour
    {
        private Image top, bottom, left, right;

        public void Init(float thickness, Color color)
        {
            top = CreateLine("Top", thickness, color);
            bottom = CreateLine("Bottom", thickness, color);
            left = CreateLine("Left", thickness, color);
            right = CreateLine("Right", thickness, color);

            // Position lines inside this cell rect
            SetupLine(top.rectTransform, anchorMin: new Vector2(0, 1), anchorMax: new Vector2(1, 1), sizeDelta: new Vector2(0, thickness), pivot: new Vector2(0.5f, 1));
            SetupLine(bottom.rectTransform, anchorMin: new Vector2(0, 0), anchorMax: new Vector2(1, 0), sizeDelta: new Vector2(0, thickness), pivot: new Vector2(0.5f, 0));
            SetupLine(left.rectTransform, anchorMin: new Vector2(0, 0), anchorMax: new Vector2(0, 1), sizeDelta: new Vector2(thickness, 0), pivot: new Vector2(0, 0.5f));
            SetupLine(right.rectTransform, anchorMin: new Vector2(1, 0), anchorMax: new Vector2(1, 1), sizeDelta: new Vector2(thickness, 0), pivot: new Vector2(1, 0.5f));
        }

        public void SetColor(Color c)
        {
            if (top) top.color = c;
            if (bottom) bottom.color = c;
            if (left) left.color = c;
            if (right) right.color = c;
        }

        private Image CreateLine(string name, float thickness, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            Image img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.color = color;
            return img;
        }

        private void SetupLine(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 pivot)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = sizeDelta;
        }
    }
}
