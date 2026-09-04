#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Clears stale editor selections that can make built-in inspectors try to serialize null targets.
/// </summary>
[InitializeOnLoad]
public static class UnityEditorSelectionRecovery
{
    private const string ClearedThisSessionKey = "UnityEditorSelectionRecovery.ClearedThisSession";

    static UnityEditorSelectionRecovery()
    {
        EditorApplication.delayCall += ClearSelectionOnceAfterReload;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.hierarchyChanged += ClearOnlyIfSelectionContainsNull;
        Selection.selectionChanged += ClearOnlyIfSelectionContainsNull;
    }

    [MenuItem("Tools/Recovery/Clear Broken Inspector Selection")]
    public static void ClearBrokenInspectorSelection()
    {
        Selection.objects = Array.Empty<UnityEngine.Object>();
        if (ActiveEditorTracker.sharedTracker != null)
        {
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        ClearOnlyIfSelectionContainsNull();

        if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
        {
            ClearOnlyIfSelectionContainsNull();
        }
    }

    private static void ClearSelectionOnceAfterReload()
    {
        if (SessionState.GetBool(ClearedThisSessionKey, false))
        {
            ClearOnlyIfSelectionContainsNull();
            return;
        }

        SessionState.SetBool(ClearedThisSessionKey, true);
        ClearBrokenInspectorSelection();
    }

    public static void ClearOnlyIfSelectionContainsNull()
    {
        UnityEngine.Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null || selectedObjects.Length == 0)
        {
            return;
        }

        bool hasNull = false;
        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (selectedObjects[i] == null || !selectedObjects[i])
            {
                hasNull = true;
                break;
            }
        }

        if (hasNull)
        {
            Selection.objects = Array.Empty<UnityEngine.Object>();
            if (ActiveEditorTracker.sharedTracker != null)
            {
                ActiveEditorTracker.sharedTracker.ForceRebuild();
            }
        }
    }

    /// <summary>
    /// Safely clears selection of a GameObject before destroying it in duplicate singleton Awake methods.
    /// </summary>
    public static void SafeDeselectBeforeDestroy(GameObject go)
    {
        if (go == null) return;

        if (Selection.activeGameObject == go)
        {
            Selection.activeGameObject = null;
        }

        UnityEngine.Object[] selected = Selection.objects;
        if (selected != null && Array.IndexOf(selected, go) >= 0)
        {
            ClearBrokenInspectorSelection();
        }
    }
}
#endif
