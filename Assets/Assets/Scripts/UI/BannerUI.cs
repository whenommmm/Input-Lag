using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Souls-style banner: a full-width tinted strip across screen center with
/// large text ("YOU DIED" / "LEVEL COMPLETE"), faded in and out via a
/// CanvasGroup. Builds its own overlay canvas at runtime — same pattern as
/// the queue UI, no prefabs, no scene wiring beyond adding the component.
/// </summary>
public class BannerUI : MonoBehaviour
{
    [SerializeField] private float fadeInSeconds = 0.3f;
    [SerializeField] private float holdSeconds = 0.8f;
    [SerializeField] private float fadeOutSeconds = 0.3f;
    [SerializeField] private float stripHeight = 240f; // reference pixels (1920x1080)
    [SerializeField] private float fontSize = 90f;

    private CanvasGroup group;
    private Image strip;
    private TextMeshProUGUI label;

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        var canvasGo = new GameObject("BannerCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // above everything else

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        group = canvasGo.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;

        var stripGo = new GameObject("Strip");
        stripGo.transform.SetParent(canvasGo.transform, false);
        strip = stripGo.AddComponent<Image>(); // no sprite = solid tintable rect
        RectTransform stripRect = strip.rectTransform;
        stripRect.anchorMin = new Vector2(0f, 0.5f); // full width, vertically centered
        stripRect.anchorMax = new Vector2(1f, 0.5f);
        stripRect.sizeDelta = new Vector2(0f, stripHeight);

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(stripGo.transform, false);
        label = labelGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = fontSize;
        label.color = Color.white;
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = Vector2.zero; // fill the strip
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
    }

    /// <summary>
    /// Fade in -> hold -> onFadeOutStart -> fade out -> onComplete.
    /// Ignored if a banner is already playing.
    /// </summary>
    public void Play(string text, Color stripColor, Action onFadeOutStart, Action onComplete)
    {
        if (IsPlaying) return;
        StartCoroutine(PlayRoutine(text, stripColor, onFadeOutStart, onComplete));
    }

    private IEnumerator PlayRoutine(string text, Color stripColor,
        Action onFadeOutStart, Action onComplete)
    {
        IsPlaying = true;
        label.text = text;
        strip.color = stripColor;

        for (float t = 0f; t < fadeInSeconds; t += Time.deltaTime)
        {
            group.alpha = t / fadeInSeconds;
            yield return null;
        }
        group.alpha = 1f;

        yield return new WaitForSeconds(holdSeconds);

        onFadeOutStart?.Invoke();
        for (float t = 0f; t < fadeOutSeconds; t += Time.deltaTime)
        {
            group.alpha = 1f - t / fadeOutSeconds;
            yield return null;
        }
        group.alpha = 0f;

        IsPlaying = false;
        onComplete?.Invoke();
    }
}
