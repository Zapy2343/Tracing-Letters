using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Adds a gloomy, kid-friendly idle motion pass only to designer-assigned UI objects.
/// </summary>
public class MainSceneGloomyAnimator : MonoBehaviour
{
    public enum MotionKind
    {
        SoftBob,
        ButtonBreath,
        CloudDrift,
        FlowerSway,
        MusicFloat,
        PanelFloat,
        GlowPulse
    }

    [System.Serializable]
    private class MotionTargetConfig
    {
        public RectTransform rect = null;
        public MotionKind kind = MotionKind.SoftBob;
        public float amplitude = 1f;
        public float speed = 1f;
        public float delay = 0f;
        public bool pulseGraphic = false;
        public Graphic graphicOverride = null;
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
        public bool PulseGraphic;
    }

    [Header("Motion")]
    [SerializeField] private bool animateOnStart = true;
    [SerializeField] private float globalIntensity = 1f;
    [SerializeField] private List<MotionTargetConfig> animatedTargets = new List<MotionTargetConfig>();

    [Header("Gloom Glow")]
    [SerializeField] private Color warmGlowTint = new Color(1f, 0.84f, 0.45f, 1f);
    [SerializeField] private Color coolGlowTint = new Color(0.58f, 0.78f, 1f, 1f);

    private readonly List<MotionTarget> targets = new List<MotionTarget>();

    private void Start()
    {
        if (!animateOnStart)
        {
            enabled = false;
            return;
        }

        RebuildTargets();
    }

    private void Update()
    {
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

    [ContextMenu("Rebuild Motion Targets")]
    public void RebuildTargets()
    {
        targets.Clear();

        for (int i = 0; i < animatedTargets.Count; i++)
        {
            MotionTargetConfig config = animatedTargets[i];
            if (config == null || config.rect == null)
            {
                continue;
            }

            targets.Add(CreateMotionTarget(config));
        }
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
                if (target.PulseGraphic)
                {
                    PulseGraphic(target, wave, warmGlowTint);
                }
                break;

            default:
                target.Rect.anchoredPosition = target.BasePosition + new Vector2(0f, wave * target.Amplitude * intensity);
                if (target.PulseGraphic)
                {
                    PulseGraphic(target, wave, coolGlowTint);
                }
                break;
        }
    }

    private MotionTarget CreateMotionTarget(MotionTargetConfig config)
    {
        Graphic graphic = config.graphicOverride != null
            ? config.graphicOverride
            : config.rect.GetComponent<Graphic>();

        MotionTarget target = new MotionTarget
        {
            Rect = config.rect,
            Graphic = graphic,
            BasePosition = config.rect.anchoredPosition,
            BaseScale = config.rect.localScale,
            BaseRotation = config.rect.localRotation,
            BaseColor = graphic != null ? graphic.color : Color.white,
            Kind = config.kind,
            Delay = config.delay,
            Amplitude = config.amplitude,
            Speed = config.speed,
            PulseGraphic = config.pulseGraphic
        };

        return target;
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
