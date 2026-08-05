#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TracingSequenceAsset))]
public class TracingSequenceAssetEditor : Editor
{
    private const int DefaultLetterCount = 36;

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        TracingSequenceAsset sequence = (TracingSequenceAsset)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Letter Asset Tools", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Creates one separate Tracing Letter asset per letter and assigns them to this main sequence holder. Existing assigned assets are kept.", MessageType.None);

        if (GUILayout.Button("Create / Sync 36 Letter Assets"))
        {
            CreateOrSyncLetterAssets(sequence);
        }
    }

    private void CreateOrSyncLetterAssets(TracingSequenceAsset sequence)
    {
        string sequencePath = AssetDatabase.GetAssetPath(sequence);
        if (string.IsNullOrEmpty(sequencePath))
        {
            Debug.LogWarning("[TracingSequenceAssetEditor] Save the main Tracing Sequence asset before creating letter assets.");
            return;
        }

        string sequenceFolder = Path.GetDirectoryName(sequencePath)?.Replace("\\", "/");
        if (string.IsNullOrEmpty(sequenceFolder))
        {
            sequenceFolder = "Assets";
        }

        string letterFolder = $"{sequenceFolder}/{sequence.name}_Letters";
        EnsureFolder(letterFolder);

        Undo.RecordObject(sequence, "Create Tracing Letter Assets");

        for (int i = 0; i < DefaultLetterCount; i++)
        {
            int letterNumber = i + 1;
            TracingLetterAsset letterAsset = sequence.GetLetterAsset(letterNumber);

            if (letterAsset == null)
            {
                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{letterFolder}/Letter_{letterNumber:00}.asset");
                letterAsset = CreateInstance<TracingLetterAsset>();

                LetterSequence legacyLetter = FindLegacyLetter(sequence, letterNumber);
                if (legacyLetter != null)
                {
                    letterAsset.CopyFrom(legacyLetter, letterNumber);
                }
                else
                {
                    letterAsset.SetLetterNumber(letterNumber);
                }

                AssetDatabase.CreateAsset(letterAsset, assetPath);
            }

            sequence.SetLetterAsset(i, letterAsset);
        }

        EditorUtility.SetDirty(sequence);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[TracingSequenceAssetEditor] Created/synced {DefaultLetterCount} tracing letter assets for '{sequence.name}'.");
    }

    private LetterSequence FindLegacyLetter(TracingSequenceAsset sequence, int letterNumber)
    {
        for (int i = 0; i < sequence.LegacyLetterCount; i++)
        {
            LetterSequence legacyLetter = sequence.GetLegacyLetterAt(i);
            if (legacyLetter != null && legacyLetter.LetterNumber == letterNumber)
            {
                return legacyLetter;
            }
        }

        return null;
    }

    private void EnsureFolder(string assetFolder)
    {
        string[] parts = assetFolder.Split('/');
        string currentPath = parts[0];

        for (int i = 1; i < parts.Length; i++)
        {
            string nextPath = $"{currentPath}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, parts[i]);
            }

            currentPath = nextPath;
        }
    }
}
#endif
