#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PenDrawer))]
public class PenDrawerHintPathEditor : Editor
{
    private readonly List<Vector2> previewPath = new List<Vector2>();

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        PenDrawer drawer = (PenDrawer)target;
        TracingStrokeStep step = drawer != null ? drawer.CurrentSequenceStep : null;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Current Stroke Hint Path", EditorStyles.boldLabel);

        if (drawer == null || drawer.TracingSequence == null)
        {
            EditorGUILayout.HelpBox("Assign a Tracing Sequence on PenDrawer to edit hint paths.", MessageType.Info);
            return;
        }

        if (drawer.RevealTargetGraphic == null)
        {
            EditorGUILayout.HelpBox("Assign or select the reveal target graphic before editing hint paths.", MessageType.Info);
            return;
        }

        if (step == null)
        {
            EditorGUILayout.HelpBox("The current letter/stroke has no sequence step selected.", MessageType.Info);
            return;
        }

        EditorGUILayout.LabelField("Letter", drawer.CurrentLetterNumber.ToString());
        EditorGUILayout.LabelField("Stroke", (drawer.CurrentSequenceStepIndex + 1).ToString());
        EditorGUILayout.LabelField("Points", step.HintPathPointCount.ToString());

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Create Default Curve"))
            {
                Object assetToEdit = GetAssetToEdit(drawer);
                Undo.RecordObject(assetToEdit, "Create Hint Path");
                step.CreateDefaultHintPath(drawer.RevealTargetGraphic.rectTransform.rect);
                EditorUtility.SetDirty(assetToEdit);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Add Point"))
            {
                Object assetToEdit = GetAssetToEdit(drawer);
                Undo.RecordObject(assetToEdit, "Add Hint Path Point");
                Rect rect = drawer.RevealTargetGraphic.rectTransform.rect;
                step.AddHintPathPoint(rect.center);
                EditorUtility.SetDirty(assetToEdit);
                SceneView.RepaintAll();
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Remove Last Point"))
            {
                Object assetToEdit = GetAssetToEdit(drawer);
                Undo.RecordObject(assetToEdit, "Remove Hint Path Point");
                step.RemoveLastHintPathPoint();
                EditorUtility.SetDirty(assetToEdit);
                SceneView.RepaintAll();
            }

            if (GUILayout.Button("Clear Path"))
            {
                Object assetToEdit = GetAssetToEdit(drawer);
                Undo.RecordObject(assetToEdit, "Clear Hint Path");
                step.ClearHintPath();
                EditorUtility.SetDirty(assetToEdit);
                SceneView.RepaintAll();
            }
        }

        EditorGUILayout.HelpBox("Scene view: drag the yellow numbered handles on top of the letter. Points are saved in the letter image's local UI space, so device resolution changes will not break the path.", MessageType.None);
    }

    private void OnSceneGUI()
    {
        PenDrawer drawer = (PenDrawer)target;
        if (drawer == null || drawer.TracingSequence == null || drawer.RevealTargetGraphic == null)
        {
            return;
        }

        TracingStrokeStep step = drawer.CurrentSequenceStep;
        if (step == null || !step.HasCustomHintPath)
        {
            return;
        }

        RectTransform targetRect = drawer.RevealTargetGraphic.rectTransform;
        DrawPreviewCurve(step, targetRect);
        DrawControlHandles(step, targetRect, GetAssetToEdit(drawer));
    }

    private void DrawPreviewCurve(TracingStrokeStep step, RectTransform targetRect)
    {
        if (!step.TryBuildHintPath(previewPath) || previewPath.Count < 2)
        {
            return;
        }

        Handles.color = new Color(0.15f, 0.85f, 1f, 0.95f);
        for (int i = 0; i < previewPath.Count - 1; i++)
        {
            Vector3 from = targetRect.TransformPoint(previewPath[i]);
            Vector3 to = targetRect.TransformPoint(previewPath[i + 1]);
            Handles.DrawAAPolyLine(6f, from, to);
        }
    }

    private void DrawControlHandles(TracingStrokeStep step, RectTransform targetRect, Object assetToEdit)
    {
        Handles.color = Color.yellow;

        for (int i = 0; i < step.HintPathPointCount; i++)
        {
            Vector2 localPoint = step.GetHintPathPoint(i);
            Vector3 worldPoint = targetRect.TransformPoint(localPoint);
            float size = HandleUtility.GetHandleSize(worldPoint) * 0.08f;

            EditorGUI.BeginChangeCheck();
            Vector3 movedWorldPoint = Handles.FreeMoveHandle(
                worldPoint,
                size,
                Vector3.zero,
                Handles.SphereHandleCap);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(assetToEdit, "Move Hint Path Point");
                Vector2 movedLocalPoint = targetRect.InverseTransformPoint(movedWorldPoint);
                step.SetHintPathPoint(i, movedLocalPoint);
                EditorUtility.SetDirty(assetToEdit);
            }

            Handles.Label(worldPoint + Vector3.up * size * 1.8f, (i + 1).ToString(), EditorStyles.boldLabel);
        }
    }

    private Object GetAssetToEdit(PenDrawer drawer)
    {
        if (drawer != null && drawer.CurrentLetterAsset != null)
        {
            return drawer.CurrentLetterAsset;
        }

        return drawer != null ? drawer.TracingSequence : null;
    }
}
#endif
