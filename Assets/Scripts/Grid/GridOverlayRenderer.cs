using UnityEngine;
using UnityEngine.UI;

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
    [SerializeField] private Color highlightColor = new Color(0.7f, 0.2f, 1f, 1f);
    [SerializeField] private Color completedColor = new Color(1f, 1f, 1f, 0f);

    [Header("Behavior")]
    [Tooltip("If true, completed cell border objects will be disabled.")]
    [SerializeField] private bool disableOnComplete = true;

    private CellBorder[,] borders;
    private bool[,] completed;
    private RectTransform _self;

    private void Awake()
    {
        EnsureInitialized();
        EnsureBuilt();
    }

    private bool EnsureInitialized()
    {
        if (_self == null)
            _self = GetComponent<RectTransform>();

        if (_self == null)
        {
            Debug.LogError("[GridOverlayRenderer] Must be on a UI object with RectTransform.");
            enabled = false;
            return false;
        }

        if (targetRect == null)
            targetRect = transform.parent as RectTransform;

        if (targetRect == null)
        {
            Debug.LogError("[GridOverlayRenderer] targetRect is null and parent is not RectTransform.");
            enabled = false;
            return false;
        }

        return true;
    }

    public void Configure(int gx, int gy)
    {
        if (!EnsureInitialized()) return;

        gridX = Mathf.Max(1, gx);
        gridY = Mathf.Max(1, gy);
        Rebuild();
    }

    public void Rebuild()
    {
        if (!EnsureInitialized()) return;

        // Destroy old safely
        if (borders != null)
        {
            for (int y = 0; y < borders.GetLength(1); y++)
                for (int x = 0; x < borders.GetLength(0); x++)
                    if (borders[x, y] != null)
                        Destroy(borders[x, y].gameObject);
        }

        borders = null;
        completed = null;

        EnsureBuilt();
    }

    private bool IsBuilt()
    {
        return borders != null && completed != null
               && borders.GetLength(0) == gridX && borders.GetLength(1) == gridY
               && completed.GetLength(0) == gridX && completed.GetLength(1) == gridY;
    }

    private void EnsureBuilt()
    {
        if (!EnsureInitialized()) return;
        if (IsBuilt()) return;

        _self.anchorMin = Vector2.zero;
        _self.anchorMax = Vector2.one;
        _self.offsetMin = Vector2.zero;
        _self.offsetMax = Vector2.zero;

        borders = new CellBorder[gridX, gridY];
        completed = new bool[gridX, gridY];

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
        EnsureBuilt();
        if (!InRange(x, y) || completed == null || borders == null) return;

        completed[x, y] = isCompleted;

        var border = borders[x, y];
        if (border == null) return;

        if (disableOnComplete)
            border.gameObject.SetActive(!isCompleted);
        else
            border.SetColor(isCompleted ? completedColor : normalColor);
    }

    public void ClearHighlights()
    {
        EnsureBuilt();
        if (completed == null || borders == null) return;

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
        EnsureBuilt();
        if (!InRange(x, y) || completed == null || borders == null) return;
        if (completed[x, y]) return;
        borders[x, y]?.SetColor(highlightColor);
    }

    public void ApplyCompletedFromCells(float[] cells)
    {
        EnsureBuilt();
        if (cells == null || cells.Length != gridX * gridY) return;

        for (int y = 0; y < gridY; y++)
            for (int x = 0; x < gridX; x++)
            {
                int idx = y * gridX + x;
                bool done = cells[idx] >= 1f;
                SetCellCompleted(x, y, done);
            }
    }

    private bool InRange(int x, int y) => x >= 0 && x < gridX && y >= 0 && y < gridY;

    private class CellBorder : MonoBehaviour
    {
        private Image top, bottom, left, right;

        public void Init(float thickness, Color color)
        {
            top = CreateLine("Top", color);
            bottom = CreateLine("Bottom", color);
            left = CreateLine("Left", color);
            right = CreateLine("Right", color);

            SetupLine(top.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, thickness), new Vector2(0.5f, 1));
            SetupLine(bottom.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, thickness), new Vector2(0.5f, 0));
            SetupLine(left.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(thickness, 0), new Vector2(0, 0.5f));
            SetupLine(right.rectTransform, new Vector2(1, 0), new Vector2(1, 1), new Vector2(thickness, 0), new Vector2(1, 0.5f));
        }

        public void SetColor(Color c)
        {
            if (top) top.color = c;
            if (bottom) bottom.color = c;
            if (left) left.color = c;
            if (right) right.color = c;
        }

        private Image CreateLine(string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
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
