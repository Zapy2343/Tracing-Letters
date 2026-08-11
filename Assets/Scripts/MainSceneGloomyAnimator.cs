using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Adds a gloomy, kid-friendly idle motion pass to MainScreen without requiring every UI object to be wired manually.
/// </summary>
public class MainSceneGloomyAnimator : MonoBehaviour
{
    private enum MotionKind
    {
        SoftBob,
        ButtonBreath,
        CloudDrift,
        FlowerSway,
        MusicFloat,
        PanelFloat,
        GlowPulse
    }

    private struct MotionTarget
    {
        public RectTransform Rect;
        public Graphic Graphic;
        public Vector2 BasePosition;
        public Vector3 BaseScale;
        public Quaternion BaseRotation;
        public Color BaseColor;
        public MotionKind Kind;
        public float Delay;
        public float Amplitude;
        public float Speed;
    }

    [Header("Scene Filter")]
    [SerializeField] private string sceneName = "MainScreen";

    [Header("Motion")]
    [SerializeField] private bool animateOnStart = true;
    [SerializeField] private float globalIntensity = 1f;
    [SerializeField] private float scanDelay = 0.1f;

    [Header("Gloom Glow")]
    [SerializeField] private Color warmGlowTint = new Color(1f, 0.84f, 0.45f, 1f);
    [SerializeField] private Color coolGlowTint = new Color(0.58f, 0.78f, 1f, 1f);

    private readonly List<MotionTarget> targets = new List<MotionTarget>();
    private float startTime;
    private bool hasScanned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapActiveScene()
    {
        TryCreateForScene(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryCreateForScene(scene);
    }

    private static void TryCreateForScene(Scene scene)
    {
        if (!scene.IsValid() || scene.name != "MainScreen")
        {
            return;
        }

        if (FindFirstObjectByType<MainSceneGloomyAnimator>() != null)
        {
            return;
        }

        GameObject animatorObject = new GameObject("Main Scene Gloomy Animator");
        SceneManager.MoveGameObjectToScene(animatorObject, scene);
        animatorObject.AddComponent<MainSceneGloomyAnimator>();
    }

    private void Start()
    {
        if (!animateOnStart || SceneManager.GetActiveScene().name != sceneName)
        {
            enabled = false;
            return;
        }

        startTime = Time.unscaledTime + Mathf.Max(0f, scanDelay);
    }

    private void Update()
    {
        if (!hasScanned && Time.unscaledTime >= startTime)
        {
            ScanScene();
            hasScanned = true;
        }

        if (!hasScanned)
        {
            return;
        }

        float time = Time.unscaledTime;
        float intensity = Mathf.Max(0f, globalIntensity);

        for (int i = 0; i < targets.Count; i++)
        {
            MotionTarget target = targets[i];
            if (target.Rect == null)
            {
                continue;
            }

            ApplyMotion(target, time, intensity);
        }
    }

    private void ScanScene()
    {
        targets.Clear();

        RectTransform[] rects = FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int animatedCount = 0;

        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == null || ShouldSkip(rect))
            {
                continue;
            }

            if (!TryGetMotion(rect, animatedCount, out MotionTarget target))
            {
                continue;
            }

            targets.Add(target);
            animatedCount++;
        }
    }

    private bool ShouldSkip(RectTransform rect)
    {
        if (!rect.gameObject.scene.IsValid() || rect.gameObject.scene.name != sceneName)
        {
            return true;
        }

        if (rect.GetComponent<UILine>() != null)
        {
            return true;
        }

        string lowerName = rect.name.ToLowerInvariant();
        return lowerName.Contains("penstroke") ||
            lowerName.Contains("sequence") ||
            lowerName.Contains("mask") ||
            lowerName.Contains("viewport") ||
            lowerName.Contains("content");
    }

    private bool TryGetMotion(RectTransform rect, int index, out MotionTarget target)
    {
        target = new MotionTarget
        {
            Rect = rect,
            Graphic = rect.GetComponent<Graphic>(),
            BasePosition = rect.anchoredPosition,
            BaseScale = rect.localScale,
            BaseRotation = rect.localRotation,
            Kind = MotionKind.SoftBob,
            Delay = index * 0.17f,
            Amplitude = 1f,
            Speed = 1f
        };

        if (target.Graphic != null)
        {
            target.BaseColor = target.Graphic.color;
        }

        string name = rect.name.ToLowerInvariant();

        if (name.Contains("cloud"))
        {
            target.Kind = MotionKind.CloudDrift;
            target.Amplitude = 9f;
            target.Speed = 0.45f;
            return true;
        }

        if (name.Contains("flower"))
        {
            target.Kind = MotionKind.FlowerSway;
            target.Amplitude = 3.5f;
            target.Speed = 0.9f;
            return true;
        }

        if (name.Contains("music note"))
        {
            target.Kind = MotionKind.MusicFloat;
            target.Amplitude = 8f;
            target.Speed = 0.8f;
            return true;
        }

        if (name.Contains("button") || name.Contains("back"))
        {
            target.Kind = MotionKind.ButtonBreath;
            target.Amplitude = 0.035f;
            target.Speed = 1.1f;
            return true;
        }

        if (name.Contains("game type holder") ||
            name.Contains("no ads img") ||
            name.Contains("support learning img") ||
            name.Contains("icon"))
        {
            target.Kind = MotionKind.PanelFloat;
            target.Amplitude = 4f;
            target.Speed = 0.7f;
            return true;
        }

        if (name.Contains("hand"))
        {
            target.Kind = MotionKind.GlowPulse;
            target.Amplitude = 0.045f;
            target.Speed = 1.25f;
            return true;
        }

        return false;
    }

    private void ApplyMotion(MotionTarget target, float time, float intensity)
    {
        float phase = (time * target.Speed) + target.Delay;
        float wave = Mathf.Sin(phase * Mathf.PI * 2f);
        float softWave = Mathf.Sin((phase * 0.5f) * Mathf.PI * 2f);

        switch (target.Kind)
        {
            case MotionKind.CloudDrift:
                target.Rect.anchoredPosition = target.BasePosition + new Vector2(wave * target.Amplitude * intensity, softWave * 2f * intensity);
                break;

            case MotionKind.FlowerSway:
                target.Rect.localRotation = target.BaseRotation * Quaternion.Euler(0f, 0f, wave * target.Amplitude * intensity);
                break;

            case MotionKind.MusicFloat:
                target.Rect.anchoredPosition = target.BasePosition + new Vector2(softWave * 2.5f * intensity, wave * target.Amplitude * intensity);
                target.Rect.localRotation = target.BaseRotation * Quaternion.Euler(0f, 0f, wave * 5f * intensity);
                break;

            case MotionKind.ButtonBreath:
                target.Rect.localScale = target.BaseScale * (1f + ((wave + 1f) * 0.5f * target.Amplitude * intensity));
                break;

            case MotionKind.PanelFloat:
                target.Rect.anchoredPosition = target.BasePosition + new Vector2(0f, wave * target.Amplitude * intensity);
                target.Rect.localScale = target.BaseScale * (1f + ((softWave + 1f) * 0.006f * intensity));
                break;

            case MotionKind.GlowPulse:
                target.Rect.localScale = target.BaseScale * (1f + ((wave + 1f) * 0.5f * target.Amplitude * intensity));
                PulseGraphic(target, wave, warmGlowTint);
                break;

            default:
                target.Rect.anchoredPosition = target.BasePosition + new Vector2(0f, wave * target.Amplitude * intensity);
                PulseGraphic(target, wave, coolGlowTint);
                break;
        }
    }

    private void PulseGraphic(MotionTarget target, float wave, Color tint)
    {
        if (target.Graphic == null)
        {
            return;
        }

        float amount = (wave + 1f) * 0.08f;
        target.Graphic.color = Color.Lerp(target.BaseColor, tint, amount);
    }
}
