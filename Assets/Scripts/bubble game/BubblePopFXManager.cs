using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated FX Manager for Bubble POP scene using Object Pooling for particle effects.
/// Re-uses particle UI GameObjects when popping bubbles and completing levels with state resets.
/// </summary>
public class BubblePopFXManager : MonoBehaviour
{
    [Header("FX Sprites")]
    [SerializeField] private Sprite sparkSprite;
    [SerializeField] private Sprite glowSprite;
    [SerializeField] private Sprite starburstSprite;
    [SerializeField] private Sprite lightRaysSprite;
    [SerializeField] private Sprite sheenSprite;
    [SerializeField] private Sprite[] completionBalloonSprites;
    [SerializeField] private Sprite[] completionConfettiSprites;
    [SerializeField] private Sprite whiteSparkleSprite;

    [Header("Object Pool Settings")]
    [SerializeField] private int initialPoolSize = 60;
    [SerializeField] private bool allowPoolGrowth = true;

    [Header("Vibrant FX Palette")]
    [SerializeField]
    private Color[] vibrantColors = new Color[]
    {
        new Color(1.0f, 0.85f, 0.2f),  // Gold / Yellow
        new Color(0.2f, 0.9f, 1.0f),   // Cyan / Aqua
        new Color(1.0f, 0.3f, 0.85f),  // Vivid Pink / Magenta
        new Color(0.3f, 1.0f, 0.4f),   // Lime Green
        new Color(1.0f, 0.5f, 0.15f),  // Electric Orange
        new Color(0.7f, 0.4f, 1.0f)    // Bright Purple
    };

    private Canvas parentCanvas;
    private Canvas fxCanvas;
    private RectTransform fxContainer;
    private RectTransform poolContainer;
    private Material additiveMaterial;
    private Sprite generatedSoftCircleSprite;

    private readonly Queue<GameObject> particlePool = new Queue<GameObject>();
    private readonly HashSet<GameObject> activeParticles = new HashSet<GameObject>();

    private void Awake()
    {
        InitializeFXContainer();
        CreateAdditiveMaterial();
        AutoLoadSpritesIfEmpty();
        InitializeObjectPool();
    }

    private void InitializeFXContainer()
    {
        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
#if UNITY_2023_1_OR_NEWER
            parentCanvas = FindFirstObjectByType<Canvas>();
#else
            parentCanvas = FindObjectOfType<Canvas>();
#endif
        }

        if (parentCanvas != null)
        {
            GameObject container = new GameObject("BubblePOP_FX_Container", typeof(RectTransform), typeof(Canvas));
            container.transform.SetParent(parentCanvas.transform, false);

            fxContainer = container.GetComponent<RectTransform>();
            fxContainer.anchorMin = Vector2.zero;
            fxContainer.anchorMax = Vector2.one;
            fxContainer.sizeDelta = Vector2.zero;
            fxContainer.anchoredPosition = Vector2.zero;

            fxCanvas = container.GetComponent<Canvas>();
            fxCanvas.overrideSorting = true;
            fxCanvas.sortingOrder = parentCanvas.sortingOrder + 100;
            fxCanvas.pixelPerfect = parentCanvas.pixelPerfect;

            EnsureFXContainerOnTop();
        }
    }

    public void EnsureFXContainerOnTop()
    {
        if (fxContainer == null)
        {
            InitializeFXContainer();
        }

        if (fxContainer != null)
        {
            fxContainer.SetAsLastSibling();
        }
    }

    private void InitializeObjectPool()
    {
        if (fxContainer == null) return;

        GameObject poolObject = new GameObject("FX_Particle_Pool", typeof(RectTransform));
        poolObject.transform.SetParent(fxContainer, false);
        poolContainer = poolObject.GetComponent<RectTransform>();
        poolContainer.anchorMin = Vector2.zero;
        poolContainer.anchorMax = Vector2.one;
        poolContainer.sizeDelta = Vector2.zero;
        poolContainer.anchoredPosition = Vector2.zero;
        poolObject.SetActive(false);

        while (particlePool.Count < initialPoolSize)
        {
            GameObject particle = CreatePooledParticleInstance();
            ReturnParticleToPool(particle);
        }
    }

    private GameObject CreatePooledParticleInstance()
    {
        GameObject pObj = new GameObject("Pooled_FX_Particle", typeof(RectTransform), typeof(Image));
        pObj.transform.SetParent(poolContainer, false);

        Image img = pObj.GetComponent<Image>();
        img.raycastTarget = false;

        pObj.SetActive(false);
        return pObj;
    }

    private GameObject GetParticleFromPool()
    {
        while (particlePool.Count > 0)
        {
            GameObject particle = particlePool.Dequeue();
            if (particle != null)
            {
                ResetParticleState(particle);
                particle.transform.SetParent(fxContainer, false);
                particle.SetActive(true);
                activeParticles.Add(particle);
                return particle;
            }
        }

        if (allowPoolGrowth)
        {
            GameObject newParticle = CreatePooledParticleInstance();
            ResetParticleState(newParticle);
            newParticle.transform.SetParent(fxContainer, false);
            newParticle.SetActive(true);
            activeParticles.Add(newParticle);
            return newParticle;
        }

        return null;
    }

    private void ReturnParticleToPool(GameObject particle)
    {
        if (particle == null) return;

        activeParticles.Remove(particle);
        ResetParticleState(particle);
        particle.SetActive(false);

        if (poolContainer != null && gameObject.activeInHierarchy && poolContainer.gameObject.activeInHierarchy)
        {
            particle.transform.SetParent(poolContainer, false);
        }

        particlePool.Enqueue(particle);
    }

    private void ResetParticleState(GameObject particle)
    {
        if (particle == null) return;

        RectTransform rect = particle.GetComponent<RectTransform>();
        if (rect != null)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(50f, 50f);
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        Image img = particle.GetComponent<Image>();
        if (img != null)
        {
            img.sprite = GetGeneratedSoftCircleSprite();
            img.color = Color.white;
            img.material = null;
            img.raycastTarget = false;
        }
    }

    private void CreateAdditiveMaterial()
    {
        Shader targetShader = Shader.Find("UI/Additive");
        if (targetShader != null)
        {
            additiveMaterial = new Material(targetShader);
        }
    }

    private void AutoLoadSpritesIfEmpty()
    {
#if UNITY_EDITOR
        if (sparkSprite == null)
            sparkSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/FX sprites/ui_glow_spark.png");
        if (glowSprite == null)
            glowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/FX sprites/Glow.png");
        if (starburstSprite == null)
            starburstSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/FX sprites/Glow1.png");
        if (lightRaysSprite == null)
            lightRaysSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/FX sprites/GlowFxLightRays.png");
        if (sheenSprite == null)
            sheenSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/FX sprites/ui_sheen_sprite.png");

        if (whiteSparkleSprite == null)
        {
            string[] sparkleGuids = UnityEditor.AssetDatabase.FindAssets("Sparkles t:Sprite", new[] { "Assets/Sprites/CompletionFX" });
            if (sparkleGuids.Length > 0)
            {
                whiteSparkleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(sparkleGuids[0]));
            }
        }

        if (completionBalloonSprites == null || completionBalloonSprites.Length == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Sprites/CompletionFX" });
            List<Sprite> list = new List<Sprite>();
            foreach (string guid in guids)
            {
                Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.name.ToLower().Contains("balloon")) list.Add(s);
            }
            if (list.Count > 0) completionBalloonSprites = list.ToArray();
        }

        if (completionConfettiSprites == null || completionConfettiSprites.Length == 0)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Sprites/CompletionFX" });
            List<Sprite> list = new List<Sprite>();
            foreach (string guid in guids)
            {
                Sprite s = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                if (s != null && s.name.ToLower().Contains("confetti")) list.Add(s);
            }
            if (list.Count > 0) completionConfettiSprites = list.ToArray();
        }
#endif
    }

    /// <summary>
    /// Spawns a pop FX particle burst at localPosition using pooled particle GameObjects.
    /// </summary>
    public void PlayPopFX(Vector2 localPosition)
    {
        EnsureFXContainerOnTop();

        // 1. Soft Starburst Flash
        Sprite starSprite = starburstSprite != null ? starburstSprite : (glowSprite != null ? glowSprite : GetGeneratedSoftCircleSprite());
        SpawnStarburst(localPosition, 0.35f, 130f, starSprite);

        // 2. Pooled Spark Burst
        Sprite particleSprite = sparkSprite != null ? sparkSprite : GetGeneratedSoftCircleSprite();
        int count = Random.Range(14, 22);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = Random.Range(140f, 340f);
            float size = Random.Range(20f, 48f);
            Color color = GetRandomVibrantColor();

            SpawnBurstParticle(localPosition, dir, speed, size, color, Random.Range(0.4f, 0.65f), particleSprite);
        }
    }

    /// <summary>
    /// Plays an explosive impact blast celebration when a letter/level is completed.
    /// Features a shockwave ring, explosive radial confetti spread, balloon burst, and star sparkles.
    /// </summary>
    public void PlayCompletionCelebration(Vector2 centerPosition)
    {
        EnsureFXContainerOnTop();

        // 1. Impact Shockwave Ring Expansion
        SpawnShockwaveRing(centerPosition, 0.65f, 750f);

        // 2. Explosive Radial Confetti Blast (stars & strips)
        SpawnReferenceConfetti(2.8f, centerPosition);

        // 3. Radial Balloon Burst from Impact Center
        SpawnReferenceBalloons(2.8f, centerPosition);

        // 4. Dense Radial Sparkle & Star Explosion
        Sprite particleSprite = whiteSparkleSprite != null ? whiteSparkleSprite : (sparkSprite != null ? sparkSprite : GetGeneratedSoftCircleSprite());
        for (int i = 0; i < 36; i++)
        {
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float speed = Random.Range(320f, 850f);
            float size = Random.Range(28f, 65f);
            Color color = GetRandomVibrantColor();

            SpawnBurstParticle(centerPosition, dir, speed, size, color, Random.Range(0.6f, 1.25f), particleSprite);
        }

        // 5. Starburst Core Flash
        Sprite starSprite = starburstSprite != null ? starburstSprite : (glowSprite != null ? glowSprite : GetGeneratedSoftCircleSprite());
        SpawnStarburst(centerPosition, 0.85f, 320f, starSprite);
    }

    private void SpawnShockwaveRing(Vector2 center, float duration, float maxDiameter)
    {
        GameObject pObj = GetParticleFromPool();
        if (pObj == null) return;

        Sprite sprite = glowSprite != null ? glowSprite : GetGeneratedSoftCircleSprite();

        RectTransform rect = pObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(30f, 30f);
        rect.anchoredPosition = center;
        rect.localScale = Vector3.one;

        Image img = pObj.GetComponent<Image>();
        img.sprite = sprite;
        img.material = additiveMaterial;
        img.color = new Color(1f, 0.95f, 0.5f, 0.9f);

        StartCoroutine(AnimateShockwaveRing(pObj, rect, img, duration, maxDiameter));
    }

    private IEnumerator AnimateShockwaveRing(GameObject pObj, RectTransform rect, Image img, float duration, float maxDiameter)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float currentSize = Mathf.Lerp(30f, maxDiameter, eased);

            rect.sizeDelta = new Vector2(currentSize, currentSize);

            Color c = img.color;
            c.a = Mathf.Lerp(0.9f, 0f, t);
            img.color = c;

            yield return null;
        }

        ReturnParticleToPool(pObj);
    }

    private void SpawnBurstParticle(Vector2 center, Vector2 dir, float speed, float size, Color color, float duration, Sprite particleSprite)
    {
        GameObject pObj = GetParticleFromPool();
        if (pObj == null) return;

        RectTransform rect = pObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = center;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Image img = pObj.GetComponent<Image>();
        img.sprite = particleSprite != null ? particleSprite : GetGeneratedSoftCircleSprite();
        img.material = additiveMaterial;
        img.color = color;

        StartCoroutine(AnimateBurstParticle(pObj, rect, img, dir, speed, duration));
    }

    private IEnumerator AnimateBurstParticle(GameObject pObj, RectTransform rect, Image img, Vector2 dir, float speed, float duration)
    {
        float elapsed = 0f;
        Vector2 startPos = rect.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            rect.anchoredPosition = startPos + dir * (speed * t * (1f - 0.4f * t));
            rect.localScale = Vector3.one * (1f - t * 0.7f);

            Color c = img.color;
            c.a = 1f - t;
            img.color = c;

            yield return null;
        }

        ReturnParticleToPool(pObj);
    }

    private void SpawnStarburst(Vector2 center, float duration, float size = 140f, Sprite customSprite = null)
    {
        GameObject pObj = GetParticleFromPool();
        if (pObj == null) return;

        Sprite sprite = customSprite != null ? customSprite : (starburstSprite != null ? starburstSprite : GetGeneratedSoftCircleSprite());

        RectTransform rect = pObj.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = center;
        rect.localScale = Vector3.zero;

        Image img = pObj.GetComponent<Image>();
        img.sprite = sprite;
        img.material = additiveMaterial;
        img.color = new Color(1f, 0.95f, 0.4f, 0.85f);

        StartCoroutine(AnimateStarburst(pObj, rect, img, duration));
    }

    private IEnumerator AnimateStarburst(GameObject pObj, RectTransform rect, Image img, float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float scale = Mathf.Sin(t * Mathf.PI) * 1.2f;

            rect.localScale = new Vector3(scale, scale, 1f);
            rect.localRotation = Quaternion.Euler(0f, 0f, t * 180f);

            Color c = img.color;
            c.a = 1f - t;
            img.color = c;

            yield return null;
        }

        ReturnParticleToPool(pObj);
    }

    private void SpawnReferenceConfetti(float duration, Vector2 center)
    {
        if (completionConfettiSprites == null || completionConfettiSprites.Length == 0) return;

        int count = Random.Range(50, 70);
        for (int i = 0; i < count; i++)
        {
            Sprite sprite = completionConfettiSprites[Random.Range(0, completionConfettiSprites.Length)];
            if (sprite == null) continue;

            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 velocity = dir * Random.Range(350f, 850f) + Vector2.up * Random.Range(80f, 260f);

            GameObject pObj = GetParticleFromPool();
            if (pObj == null) continue;

            RectTransform rect = pObj.GetComponent<RectTransform>();
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            float size = Random.Range(30f, 56f);
            rect.sizeDelta = new Vector2(size * aspect, size);
            rect.anchoredPosition = center + Random.insideUnitCircle * 40f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            Image img = pObj.GetComponent<Image>();
            img.sprite = sprite;
            img.material = null;
            img.color = Color.white;

            float delay = Random.Range(0f, 0.15f);
            StartCoroutine(AnimateConfettiPiece(pObj, rect, img, velocity, delay, duration));
        }
    }

    private IEnumerator AnimateConfettiPiece(GameObject pObj, RectTransform rect, Image img, Vector2 velocity, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        Vector2 pos = rect.anchoredPosition;
        Vector2 gravity = new Vector2(0f, -340f);
        float spin = Random.Range(-450f, 450f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            velocity += gravity * Time.deltaTime;
            pos += velocity * Time.deltaTime;
            rect.anchoredPosition = pos;
            rect.Rotate(0f, 0f, spin * Time.deltaTime);

            Color c = img.color;
            c.a = t < 0.78f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.78f) / 0.22f);
            img.color = c;

            yield return null;
        }

        ReturnParticleToPool(pObj);
    }

    private void SpawnReferenceBalloons(float duration, Vector2 center)
    {
        if (completionBalloonSprites == null || completionBalloonSprites.Length == 0) return;

        int count = Random.Range(20, 28);
        for (int i = 0; i < count; i++)
        {
            Sprite sprite = completionBalloonSprites[i % completionBalloonSprites.Length];
            if (sprite == null) continue;

            GameObject pObj = GetParticleFromPool();
            if (pObj == null) continue;

            RectTransform rect = pObj.GetComponent<RectTransform>();
            float height = Random.Range(150f, 240f);
            float aspect = sprite.rect.width / Mathf.Max(1f, sprite.rect.height);
            rect.sizeDelta = new Vector2(height * aspect, height);

            rect.anchoredPosition = center + Random.insideUnitCircle * 20f;
            rect.localScale = Vector3.zero;

            float angle = (i * (360f / count) + Random.Range(-12f, 12f)) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float blastSpeed = Random.Range(480f, 980f);
            Vector2 initialVelocity = dir * blastSpeed + Vector2.up * Random.Range(120f, 360f);

            Image img = pObj.GetComponent<Image>();
            img.sprite = sprite;
            img.material = null;
            img.color = Color.white;

            float delay = Random.Range(0f, 0.14f);
            StartCoroutine(AnimateBalloonSpread(pObj, rect, img, initialVelocity, delay, duration));
        }
    }

    private IEnumerator AnimateBalloonSpread(GameObject pObj, RectTransform rect, Image img, Vector2 velocity, float delay, float duration)
    {
        if (delay > 0f)
        {
            yield return new WaitForSeconds(delay);
        }

        float elapsed = 0f;
        Vector2 pos = rect.anchoredPosition;
        float sway = Random.Range(35f, 70f);
        float swayPhase = Random.Range(0f, Mathf.PI * 2f);
        float buoyancy = Random.Range(340f, 540f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            velocity.x = Mathf.Lerp(velocity.x, Mathf.Sin(Time.time * 2.4f + swayPhase) * sway, Time.deltaTime * 3.8f);
            velocity.y = Mathf.Lerp(velocity.y, buoyancy, Time.deltaTime * 3.2f);

            pos += velocity * Time.deltaTime;
            rect.anchoredPosition = pos;

            if (t < 0.15f)
            {
                float subT = t / 0.15f;
                float scale = Mathf.Lerp(0f, 1.35f, Mathf.Sin(subT * Mathf.PI * 0.5f));
                rect.localScale = Vector3.one * scale;
            }
            else if (t < 0.32f)
            {
                float subT = (t - 0.15f) / 0.17f;
                float scale = Mathf.Lerp(1.35f, 1.0f, Mathf.Sin(subT * Mathf.PI * 0.5f));
                rect.localScale = Vector3.one * scale;
            }
            else
            {
                rect.localScale = Vector3.one;
            }

            Color c = img.color;
            c.a = t < 0.82f ? 1f : Mathf.Lerp(1f, 0f, (t - 0.82f) / 0.18f);
            img.color = c;

            yield return null;
        }

        ReturnParticleToPool(pObj);
    }

    private Color GetRandomVibrantColor()
    {
        if (vibrantColors != null && vibrantColors.Length > 0)
        {
            return vibrantColors[Random.Range(0, vibrantColors.Length)];
        }
        return Color.yellow;
    }

    private Sprite GetGeneratedSoftCircleSprite()
    {
        if (generatedSoftCircleSprite == null)
        {
            const int texSize = 64;
            Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
            tex.name = "Generated Soft Particle Sprite";
            Vector2 center = new Vector2((texSize - 1) * 0.5f, (texSize - 1) * 0.5f);
            float radius = texSize * 0.48f;

            for (int y = 0; y < texSize; y++)
            {
                for (int x = 0; x < texSize; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(1f - dist / radius);
                    alpha = Mathf.Pow(alpha, 2.2f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            generatedSoftCircleSprite = Sprite.Create(tex, new Rect(0f, 0f, texSize, texSize), new Vector2(0.5f, 0.5f), texSize);
        }

        return generatedSoftCircleSprite;
    }
}
