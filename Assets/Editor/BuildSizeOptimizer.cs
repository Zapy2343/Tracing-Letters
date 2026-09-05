#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public class BuildSizeOptimizer
{
    [MenuItem("Tools/Optimize Build Size/Apply All Optimizations")]
    public static void ApplyAllOptimizations()
    {
        OptimizeTextures();
        OptimizeAudio();
        ConfigureBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Build Size Optimizer: All optimizations applied successfully!");
    }

    [MenuItem("Tools/Optimize Build Size/Optimize Textures (ASTC)")]
    public static void OptimizeTextures()
    {
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
        int updatedCount = 0;

        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages") || path.Contains("TextMesh Pro"))
                continue;

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                continue;

            bool isModified = false;

            // Enable Android platform override
            TextureImporterPlatformSettings androidSettings = importer.GetPlatformTextureSettings("Android");
            if (!androidSettings.overridden)
            {
                androidSettings.overridden = true;
                isModified = true;
            }

            // Determine max texture size & compression based on file path / usage
            int targetMaxSize = 2048;
            TextureImporterFormat targetFormat = TextureImporterFormat.ASTC_6x6;

            if (path.Contains("Patterns") || path.Contains("Background"))
            {
                targetMaxSize = 1024;
                targetFormat = TextureImporterFormat.ASTC_8x8; // Aggressive compression for large background patterns
            }
            else if (path.Contains("FXExtra") || path.Contains("CompletionFX"))
            {
                targetMaxSize = 512;
                targetFormat = TextureImporterFormat.ASTC_6x6;
            }
            else if (path.Contains("Design Letters") || path.Contains("Dotted Letters"))
            {
                targetMaxSize = 1024;
                targetFormat = TextureImporterFormat.ASTC_6x6;
            }
            else if (path.Contains("Icons") || path.Contains("Sounds Icons"))
            {
                targetMaxSize = 512;
                targetFormat = TextureImporterFormat.ASTC_6x6;
            }

            if (androidSettings.maxTextureSize != targetMaxSize)
            {
                androidSettings.maxTextureSize = targetMaxSize;
                isModified = true;
            }

            if (androidSettings.format != targetFormat)
            {
                androidSettings.format = targetFormat;
                isModified = true;
            }

            if (androidSettings.compressionQuality != (int)TextureCompressionQuality.Normal)
            {
                androidSettings.compressionQuality = (int)TextureCompressionQuality.Normal;
                isModified = true;
            }

            if (isModified)
            {
                importer.SetPlatformTextureSettings(androidSettings);
                importer.SaveAndReimport();
                updatedCount++;
            }
        }

        Debug.Log($"Build Size Optimizer: Processed {updatedCount} textures for Android ASTC optimization.");
    }

    [MenuItem("Tools/Optimize Build Size/Optimize Audio (Mono + Vorbis)")]
    public static void OptimizeAudio()
    {
        string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
        int updatedCount = 0;

        foreach (string guid in audioGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Packages"))
                continue;

            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null)
                continue;

            bool isModified = false;

            if (!importer.forceToMono)
            {
                importer.forceToMono = true;
                isModified = true;
            }

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;

            // Set Vorbis compression with quality = 0.35 (35%)
            if (settings.compressionFormat != AudioCompressionFormat.Vorbis)
            {
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                isModified = true;
            }

            if (Mathf.Abs(settings.quality - 0.35f) > 0.01f)
            {
                settings.quality = 0.35f;
                isModified = true;
            }

            // Set load type: Streaming for BGM, CompressedInMemory for voice/SFX
            AudioClipLoadType targetLoadType = AudioClipLoadType.CompressedInMemory;
            if (path.Contains("Sounds/BG") || path.Contains("ExtraBGM") || path.Contains("Moonlight") || path.Contains("Lullaby"))
            {
                targetLoadType = AudioClipLoadType.Streaming;
            }

            if (settings.loadType != targetLoadType)
            {
                settings.loadType = targetLoadType;
                isModified = true;
            }

            if (isModified)
            {
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
                updatedCount++;
            }
        }

        Debug.Log($"Build Size Optimizer: Processed {updatedCount} audio clips for mono + vorbis optimization.");
    }

    [MenuItem("Tools/Optimize Build Size/Configure Android Build Settings")]
    public static void ConfigureBuildSettings()
    {
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Android, ManagedStrippingLevel.Medium);
        EditorUserBuildSettings.buildAppBundle = true; // Build AAB for Play Store size optimization

        Debug.Log("Build Size Optimizer: Android Build Settings configured (IL2CPP, Medium Managed Stripping, AAB).");
    }
}
#endif
