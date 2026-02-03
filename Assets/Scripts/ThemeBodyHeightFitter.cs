using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(GridLayoutGroup))]
[RequireComponent(typeof(LayoutElement))]
public class ThemeBodyHeightFitter : MonoBehaviour
{
    private GridLayoutGroup _grid;
    private LayoutElement _le;
    private RectTransform _rt;

    private void Awake()
    {
        _grid = GetComponent<GridLayoutGroup>();
        _le = GetComponent<LayoutElement>();
        _rt = (RectTransform)transform;
    }

    public void Refit()
    {
        if (_grid == null || _le == null) return;

        // 只统计 active child（以后做过滤/隐藏也不会错）
        int count = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).gameObject.activeSelf)
                count++;
        }

        if (count == 0)
        {
            _le.preferredHeight = 0f;
            Mark();
            return;
        }

        // 列数由你在 GridLayoutGroup 里指定
        int columns = Mathf.Max(1, _grid.constraintCount);
        int rows = Mathf.CeilToInt(count / (float)columns);

        float h =
            _grid.padding.top + _grid.padding.bottom +
            rows * _grid.cellSize.y +
            Mathf.Max(0, rows - 1) * _grid.spacing.y;

        _le.preferredHeight = h;
        Mark();
    }

    private void Mark()
    {
        LayoutRebuilder.MarkLayoutForRebuild(_rt);
        if (transform.parent is RectTransform p)
            LayoutRebuilder.MarkLayoutForRebuild(p);
    }
}
