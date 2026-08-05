using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class ResponsiveBubbleMenuLayout : MonoBehaviour
{
    [Header("Grid")]
    [SerializeField] private int rows = 4;
    [SerializeField] private int columns = 9;
    [SerializeField] private float maximumBubbleSize = 150f;
    [SerializeField] private float minimumBubbleSize = 78f;
    [SerializeField] private float minimumHorizontalSpacing = 10f;
    [SerializeField] private float minimumVerticalSpacing = 18f;
    [Range(0.35f, 0.85f)]
    [SerializeField] private float contentFillPercent = 0.46f;

    [Header("Safe Area Padding")]
    [SerializeField] private float leftPadding = 75f;
    [SerializeField] private float rightPadding = 75f;
    [SerializeField] private float topPadding = 20f;
    [SerializeField] private float bottomPadding = 20f;

    private readonly List<RectTransform> rowRects = new List<RectTransform>();
    private RectTransform rectTransform;
    private Vector2 lastSize;

    private void Awake()
    {
        ResolveReferences();
        DisableExistingLayoutGroups();
        ApplyLayout();
    }

    private void OnEnable()
    {
        ResolveReferences();
        DisableExistingLayoutGroups();
        ApplyLayout();
    }

    private void Update()
    {
        if (rectTransform == null)
        {
            ResolveReferences();
        }

        if (rectTransform == null)
        {
            return;
        }

        Vector2 currentSize = rectTransform.rect.size;
        if (currentSize != lastSize)
        {
            ApplyLayout();
        }
    }

    private void OnRectTransformDimensionsChange()
    {
        ApplyLayout();
    }

    [ContextMenu("Apply Bubble Layout")]
    public void ApplyLayout()
    {
        ResolveReferences();
        if (rectTransform == null)
        {
            return;
        }

        Rect rect = rectTransform.rect;
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return;
        }

        lastSize = rect.size;
        int activeRows = Mathf.Max(1, Mathf.Min(rows, rowRects.Count));
        int activeColumns = Mathf.Max(1, GetColumnCount());
        float availableWidth = Mathf.Max(1f, rect.width - leftPadding - rightPadding);
        float availableHeight = Mathf.Max(1f, rect.height - topPadding - bottomPadding);

        float widthLimitedSize = (availableWidth - minimumHorizontalSpacing * (activeColumns - 1)) / activeColumns;
        float heightLimitedSize = (availableHeight - minimumVerticalSpacing * (activeRows - 1)) / activeRows;
        float bubbleSize = Mathf.Clamp(Mathf.Min(maximumBubbleSize, widthLimitedSize, heightLimitedSize), minimumBubbleSize, maximumBubbleSize);

        float horizontalSpacing = activeColumns > 1
            ? Mathf.Max(minimumHorizontalSpacing, (availableWidth - bubbleSize * activeColumns) / (activeColumns - 1))
            : 0f;
        float verticalSpacing = activeRows > 1
            ? Mathf.Max(minimumVerticalSpacing, (availableHeight - bubbleSize * activeRows) / (activeRows - 1))
            : 0f;

        float gridWidth = bubbleSize * activeColumns + horizontalSpacing * (activeColumns - 1);
        float gridHeight = bubbleSize * activeRows + verticalSpacing * (activeRows - 1);
        float startX = rect.xMin + leftPadding + (availableWidth - gridWidth) * 0.5f + bubbleSize * 0.5f;
        float startY = rect.yMax - topPadding - (availableHeight - gridHeight) * 0.5f - bubbleSize * 0.5f;

        for (int rowIndex = 0; rowIndex < activeRows; rowIndex++)
        {
            RectTransform row = rowRects[rowIndex];
            if (row == null)
            {
                continue;
            }

            row.anchorMin = new Vector2(0.5f, 0.5f);
            row.anchorMax = new Vector2(0.5f, 0.5f);
            row.pivot = new Vector2(0.5f, 0.5f);
            row.sizeDelta = Vector2.zero;
            row.anchoredPosition = Vector2.zero;

            int rowChildCount = row.childCount;
            float rowStartX = startX;
            if (rowChildCount > 0 && rowChildCount < activeColumns)
            {
                float rowWidth = bubbleSize * rowChildCount + horizontalSpacing * (rowChildCount - 1);
                rowStartX = rect.xMin + leftPadding + (availableWidth - rowWidth) * 0.5f + bubbleSize * 0.5f;
            }

            for (int columnIndex = 0; columnIndex < rowChildCount; columnIndex++)
            {
                RectTransform bubble = row.GetChild(columnIndex) as RectTransform;
                if (bubble == null)
                {
                    continue;
                }

                bubble.anchorMin = new Vector2(0.5f, 0.5f);
                bubble.anchorMax = new Vector2(0.5f, 0.5f);
                bubble.pivot = new Vector2(0.5f, 0.5f);
                bubble.sizeDelta = new Vector2(bubbleSize, bubbleSize);
                bubble.anchoredPosition = new Vector2(
                    rowStartX + columnIndex * (bubbleSize + horizontalSpacing),
                    startY - rowIndex * (bubbleSize + verticalSpacing));
                ResizeBubbleContent(bubble);
            }
        }
    }

    private void ResizeBubbleContent(RectTransform bubble)
    {
        if (bubble == null)
        {
            return;
        }

        Image shellImage = bubble.GetComponent<Image>();
        Image[] images = bubble.GetComponentsInChildren<Image>(true);
        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (image == null || image == shellImage)
            {
                continue;
            }

            RectTransform imageRect = image.GetComponent<RectTransform>();
            if (imageRect == null)
            {
                continue;
            }

            float inset = (1f - contentFillPercent) * 0.5f;
            imageRect.anchorMin = new Vector2(inset, inset);
            imageRect.anchorMax = new Vector2(1f - inset, 1f - inset);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.anchoredPosition = Vector2.zero;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        rowRects.Clear();
        int rowLimit = Mathf.Max(1, rows);
        for (int i = 0; i < transform.childCount && rowRects.Count < rowLimit; i++)
        {
            if (transform.GetChild(i) is RectTransform row)
            {
                rowRects.Add(row);
            }
        }
    }

    private int GetColumnCount()
    {
        int maxColumns = Mathf.Max(1, columns);
        for (int i = 0; i < rowRects.Count; i++)
        {
            if (rowRects[i] != null)
            {
                maxColumns = Mathf.Max(maxColumns, rowRects[i].childCount);
            }
        }

        return maxColumns;
    }

    private void DisableExistingLayoutGroups()
    {
        foreach (LayoutGroup layoutGroup in GetComponentsInChildren<LayoutGroup>(true))
        {
            layoutGroup.enabled = false;
        }
    }

    private void OnValidate()
    {
        rows = Mathf.Max(1, rows);
        columns = Mathf.Max(1, columns);
        minimumBubbleSize = Mathf.Max(1f, minimumBubbleSize);
        maximumBubbleSize = Mathf.Max(minimumBubbleSize, maximumBubbleSize);
        contentFillPercent = Mathf.Clamp(contentFillPercent, 0.35f, 0.85f);
        minimumHorizontalSpacing = Mathf.Max(0f, minimumHorizontalSpacing);
        minimumVerticalSpacing = Mathf.Max(0f, minimumVerticalSpacing);
        leftPadding = Mathf.Max(0f, leftPadding);
        rightPadding = Mathf.Max(0f, rightPadding);
        topPadding = Mathf.Max(0f, topPadding);
        bottomPadding = Mathf.Max(0f, bottomPadding);
    }
}
