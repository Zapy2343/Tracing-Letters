using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class WelcomeSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/WelcomeScreen.unity";
    private const string LoadingScreenPrefabPath = "Assets/Prefabs/LoadingScreen.prefab";

    [MenuItem("Tools/Build Welcome Screen Scene")]
    public static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera));
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.54f, 0.79f, 1f, 1f);
        camera.orthographic = true;
        cameraObject.tag = "MainCamera";

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObject.transform.SetAsLastSibling();

        GameObject loadingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LoadingScreenPrefabPath);
        if (loadingPrefab == null)
        {
            Debug.LogError("Missing LoadingScreen prefab at " + LoadingScreenPrefabPath);
            EditorApplication.Exit(1);
            return;
        }

        GameObject loadingScreen = (GameObject)PrefabUtility.InstantiatePrefab(loadingPrefab, scene);
        loadingScreen.name = "LoadingScreen";

        RectTransform loadingRect = loadingScreen.GetComponent<RectTransform>();
        if (loadingRect != null)
        {
            loadingRect.anchorMin = Vector2.zero;
            loadingRect.anchorMax = Vector2.one;
            loadingRect.offsetMin = Vector2.zero;
            loadingRect.offsetMax = Vector2.zero;
            loadingRect.anchoredPosition = Vector2.zero;
            loadingRect.localScale = Vector3.one;
        }

        Canvas loadingCanvas = loadingScreen.GetComponent<Canvas>();
        if (loadingCanvas != null)
        {
            loadingCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            loadingCanvas.sortingOrder = 10;
        }

        WelcomeScreenController welcomeController = loadingScreen.AddComponent<WelcomeScreenController>();
        SerializedObject controller = new SerializedObject(welcomeController);
        controller.FindProperty("loadingScreen").objectReferenceValue = loadingScreen.GetComponent<LoadingScreenController>();
        controller.FindProperty("nextSceneName").stringValue = "MainScreen";
        controller.FindProperty("loadNextSceneAdditively").boolValue = true;
        controller.FindProperty("unloadWelcomeSceneAfterLoad").boolValue = true;
        controller.FindProperty("fadeDuration").floatValue = 0.45f;
        controller.FindProperty("transitionBackgroundImages").arraySize = 0;
        SerializedProperty colors = controller.FindProperty("transitionFallbackColors");
        colors.arraySize = 4;
        colors.GetArrayElementAtIndex(0).colorValue = new Color(1f, 0.72f, 0.25f, 1f);
        colors.GetArrayElementAtIndex(1).colorValue = new Color(0.98f, 0.45f, 0.62f, 1f);
        colors.GetArrayElementAtIndex(2).colorValue = new Color(0.46f, 0.86f, 0.56f, 1f);
        colors.GetArrayElementAtIndex(3).colorValue = new Color(0.5f, 0.36f, 0.95f, 1f);
        controller.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettingsFirst(ScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void AddSceneToBuildSettingsFirst(string scenePath)
    {
        string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(scenePath, true)
        };

        foreach (EditorBuildSettingsScene existingScene in EditorBuildSettings.scenes)
        {
            if (AssetDatabase.AssetPathToGUID(existingScene.path) == sceneGuid)
            {
                continue;
            }

            scenes.Add(existingScene);
        }

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
