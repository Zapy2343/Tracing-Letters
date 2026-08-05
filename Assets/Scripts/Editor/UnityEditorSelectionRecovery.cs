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
    }

    [MenuItem("Tools/Recovery/Clear Broken Inspector Selection")]
    public static void ClearBrokenInspectorSelection()
    {
        Selection.objects = Array.Empty<UnityEngine.Object>();
        ActiveEditorTracker.sharedTracker.ForceRebuild();
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

    private static void ClearOnlyIfSelectionContainsNull()
    {
        UnityEngine.Object[] selectedObjects = Selection.objects;
        if (selectedObjects == null)
        {
            ClearBrokenInspectorSelection();
            return;
        }

        for (int i = 0; i < selectedObjects.Length; i++)
        {
            if (selectedObjects[i] == null)
            {
                ClearBrokenInspectorSelection();
                return;
            }
        }
    }
}
#endif
