using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class UILine : MaskableGraphic
{
    public List<Vector2> points = new List<Vector2>();
    public float thickness = 10f;

    public void AddPoint(Vector2 point)
    {
        points.Add(point);
        SetVerticesDirty();
    }

    public void ClearPoints()
    {
        points.Clear();
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points == null || points.Count == 0) return;

        float halfThickness = thickness * 0.5f;

        // If only 1 point, draw a circle dot
        if (points.Count == 1)
        {
            AddCircle(vh, points[0], halfThickness);
            return;
        }

        int indexOffset = 0;

        // Draw line quads between consecutive points
        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 start = points[i];
            Vector2 end = points[i + 1];

            Vector2 dir = (end - start).normalized;
            if (dir == Vector2.zero) dir = Vector2.right;

            Vector2 normal = new Vector2(-dir.y, dir.x) * halfThickness;

            UIVertex v0 = UIVertex.simpleVert;
            v0.color = color;
            v0.position = start + normal;

            UIVertex v1 = UIVertex.simpleVert;
            v1.color = color;
            v1.position = start - normal;

            UIVertex v2 = UIVertex.simpleVert;
            v2.color = color;
            v2.position = end - normal;

            UIVertex v3 = UIVertex.simpleVert;
            v3.color = color;
            v3.position = end + normal;

            vh.AddVert(v0);
            vh.AddVert(v1);
            vh.AddVert(v2);
            vh.AddVert(v3);

            vh.AddTriangle(indexOffset + 0, indexOffset + 1, indexOffset + 2);
            vh.AddTriangle(indexOffset + 2, indexOffset + 3, indexOffset + 0);

            indexOffset += 4;
        }

        // Draw rounded caps & joints at each point
        for (int i = 0; i < points.Count; i++)
        {
            indexOffset = AddCircle(vh, points[i], halfThickness, indexOffset);
        }
    }

    private int AddCircle(VertexHelper vh, Vector2 center, float radius, int startIndex = 0)
    {
        int segments = 10;
        int centerIndex = vh.currentVertCount;

        UIVertex cVert = UIVertex.simpleVert;
        cVert.color = color;
        cVert.position = center;
        vh.AddVert(cVert);

        for (int i = 0; i < segments; i++)
        {
            float angle = (i / (float)segments) * Mathf.PI * 2f;
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = color;
            vert.position = pos;
            vh.AddVert(vert);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            vh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 1 + next);
        }

        return vh.currentVertCount;
    }
}
