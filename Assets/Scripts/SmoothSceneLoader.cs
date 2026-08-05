using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SmoothSceneLoader : MonoBehaviour
{
    private static SmoothSceneLoader instance;
    private CanvasGroup fader;
    private RectTransform[] floatingShapes;
    private Vector2[] floatingShapeStartPositions;
    private Vector2[] floatingShapeDrift;
    private Image[] floatingShapeImages;
    private bool isLoading;

    public static void LoadScene(string sceneName, float fadeDuration = 0.45f)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return;
        }

        EnsureInstance();
        instance.StartCoroutine(instance.LoadSceneRoutine(sceneName, fadeDuration));
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject root = new GameObject("Smooth Scene Loader");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<SmoothSceneLoader>();
        instance.BuildFader(root.transform);
    }

    private void BuildFader(Transform root)
    {
        GameObject canvasObject = new GameObject("Scene Fade Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasObject.transform.SetParent(root, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        fader = canvasObject.GetComponent<CanvasGroup>();
        fader.alpha = 0f;
        fader.blocksRaycasts = false;
        fader.interactable = false;

        GameObject panelObject = new GameObject("Pastel Fade Panel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(canvasObject.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        Image panel = panelObject.GetComponent<Image>();
        panel.color = new Color(0.83f, 0.79f, 1f, 1f);
        panel.raycastTarget = false;

        BuildFloatingShapes(canvasObject.transform);
    }

    private void BuildFloatingShapes(Transform parent)
    {
        Color[] colors =
        {
            new Color(1f, 0.84f, 0.35f, 1f),
            new Color(0.48f, 0.86f, 1f, 1f),
            new Color(1f, 0.56f, 0.72f, 1f),
            new Color(0.58f, 0.89f, 0.58f, 1f),
            new Color(1f, 1f, 1f, 0.95f)
        };

        Vector2[] positions =
        {
            new Vector2(-520f, 230f),
            new Vector2(-260f, -120f),
            new Vector2(0f, 180f),
            new Vector2(290f, -150f),
            new Vector2(540f, 210f)
        };

        floatingShapes = new RectTransform[positions.Length];
        floatingShapeStartPositions = new Vector2[positions.Length];
        floatingShapeDrift = new Vector2[positions.Length];
        floatingShapeImages = new Image[positions.Length];

        Sprite bubbleSprite = CreateCircleSprite();
        Sprite starSprite = CreateStarSprite();

        for (int i = 0; i < positions.Length; i++)
        {
            GameObject shapeObject = new GameObject(i % 2 == 0 ? "Floating Star" : "Floating Bubble", typeof(RectTransform), typeof(Image));
            shapeObject.transform.SetParent(parent, false);

            RectTransform rectTransform = shapeObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = positions[i];
            rectTransform.sizeDelta = Vector2.one * (i % 2 == 0 ? 130f : 160f);

            Image image = shapeObject.GetComponent<Image>();
            image.sprite = i % 2 == 0 ? starSprite : bubbleSprite;
            image.color = colors[i];
            image.preserveAspect = true;
            image.raycastTarget = false;

            floatingShapes[i] = rectTransform;
            floatingShapeStartPositions[i] = positions[i];
            floatingShapeDrift[i] = new Vector2(i % 2 == 0 ? 22f : -22f, i % 2 == 0 ? 18f : 26f);
            floatingShapeImages[i] = image;
        }
    }

    private Sprite CreateCircleSprite()
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Generated Transition Bubble";

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float fill = Mathf.InverseLerp(radius, radius * 0.65f, distance);
                float outline = Mathf.Clamp01(1f - Mathf.Abs(distance - radius) / 3.5f);
                float highlight = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), center + new Vector2(-18f, 18f)) / 12f);

                Color color = new Color(1f, 1f, 1f, 0.26f * fill);
                color = Color.Lerp(color, new Color(1f, 1f, 1f, 0.78f), highlight * 0.75f);
                color.a = Mathf.Max(color.a, outline * 0.72f);

                if (distance > radius)
                {
                    color = Color.clear;
                }

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private Sprite CreateStarSprite()
    {
        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Generated Transition Star";

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float outerRadius = size * 0.42f;
        float innerRadius = size * 0.2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y) - center;
                float angle = Mathf.Atan2(point.y, point.x);
                float wave = (Mathf.Cos(angle * 5f) + 1f) * 0.5f;
                float targetRadius = Mathf.Lerp(innerRadius, outerRadius, wave);
                float distance = point.magnitude;
                float alpha = Mathf.SmoothStep(1f, 0f, Mathf.InverseLerp(targetRadius - 2f, targetRadius + 3f, distance));

                Color color = new Color(1f, 1f, 1f, alpha);
                if (alpha <= 0.01f)
                {
                    color = Color.clear;
                }

                texture.SetPixel(x, y, color);
            }
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    private IEnumerator LoadSceneRoutine(string sceneName, float fadeDuration)
    {
        if (isLoading)
        {
            yield break;
        }

        isLoading = true;
        yield return Fade(0f, 1f, fadeDuration);

        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName);
        while (operation != null && !operation.isDone)
        {
            yield return null;
        }

        yield return null;
        yield return Fade(1f, 0f, fadeDuration);
        isLoading = false;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        fader.blocksRaycasts = true;
        fader.interactable = true;

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / safeDuration);
            float eased = t * t * (3f - 2f * t);
            fader.alpha = Mathf.LerpUnclamped(from, to, eased);
            AnimateFloatingShapes(eased);
            yield return null;
        }

        fader.alpha = to;
        AnimateFloatingShapes(to);
        fader.blocksRaycasts = to > 0.01f;
        fader.interactable = to > 0.01f;
    }

    private void AnimateFloatingShapes(float progress)
    {
        if (floatingShapes == null)
        {
            return;
        }

        for (int i = 0; i < floatingShapes.Length; i++)
        {
            RectTransform shape = floatingShapes[i];
            if (shape == null)
            {
                continue;
            }

            float wave = Mathf.Sin((Time.unscaledTime * 3.2f) + i * 0.9f);
            float pop = Mathf.Sin(progress * Mathf.PI);
            shape.anchoredPosition = floatingShapeStartPositions[i] + floatingShapeDrift[i] * wave * pop;
            shape.localScale = Vector3.one * Mathf.Lerp(0.7f, 1.08f + 0.08f * wave, pop);
            shape.localRotation = Quaternion.Euler(0f, 0f, wave * 12f * pop);

            if (floatingShapeImages != null && floatingShapeImages[i] != null)
            {
                Color color = floatingShapeImages[i].color;
                color.a = Mathf.Lerp(0f, i % 2 == 0 ? 0.95f : 0.72f, pop);
                floatingShapeImages[i].color = color;
            }
        }
    }
}
