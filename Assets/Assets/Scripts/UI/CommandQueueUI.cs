using System.Globalization;
using TMPro;
using UnityEngine;

/// <summary>
/// Renders queued commands above the player's head as draining-bar rows:
/// ability icon + a bar that empties as the countdown runs (green -> yellow
/// -> red, flashing white when imminent). The top row is always next to
/// execute and is the only one with a digit readout; rows slide up as
/// commands fire. Order is conveyed by position, so no numbering glyphs are
/// needed. Pure observer of the queue — the game runs fine without this.
/// </summary>
public class CommandQueueUI : MonoBehaviour
{
    [SerializeField] private CommandQueue queue;
    [SerializeField] private Vector2 headOffset = new Vector2(0f, 1.15f);
    [SerializeField] private float rowHeight = 0.42f;
    [SerializeField] private float barWidth = 1.5f;
    [SerializeField] private float barHeight = 0.16f;
    [SerializeField] private float iconFontSize = 3f;
    [SerializeField] private float digitFontSize = 2.2f;

    private static readonly Color BarBackColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color FullColor = new Color(0.35f, 0.85f, 0.35f);
    private static readonly Color MidColor = new Color(0.95f, 0.85f, 0.25f);
    private static readonly Color EmptyColor = new Color(0.95f, 0.30f, 0.25f);

    private Row[] rows;
    private Sprite whiteSprite;

    private class Row
    {
        public GameObject Root;
        public TextMeshPro Icon;
        public TextMeshPro Digits;
        public Transform Fill;
        public SpriteRenderer FillRenderer;
    }

    private void Awake()
    {
        // 1x1-unit white sprite generated in code so the UI stays self-contained.
        Texture2D tex = Texture2D.whiteTexture;
        whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f), tex.width);

        rows = new Row[queue.MaxQueueSize];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = BuildRow(i);
    }

    private Row BuildRow(int index)
    {
        var root = new GameObject($"QueueRow{index}");
        root.transform.SetParent(transform, false);
        // Next-to-execute (index 0) sits at the TOP of the stack; later rows below.
        float y = headOffset.y + rowHeight * (rows.Length - 1 - index);
        root.transform.localPosition = new Vector3(headOffset.x, y, 0f);

        var row = new Row { Root = root };

        row.Icon = BuildText(root.transform, "Icon",
            new Vector2(-(barWidth * 0.5f + 0.3f), 0f), iconFontSize);

        var back = new GameObject("BarBack").AddComponent<SpriteRenderer>();
        back.transform.SetParent(root.transform, false);
        back.transform.localScale = new Vector3(barWidth, barHeight, 1f);
        back.sprite = whiteSprite;
        back.color = BarBackColor;
        back.sortingOrder = 10; // above graybox sprites

        var fill = new GameObject("BarFill").AddComponent<SpriteRenderer>();
        fill.transform.SetParent(root.transform, false);
        fill.sprite = whiteSprite;
        fill.sortingOrder = 11;
        row.Fill = fill.transform;
        row.FillRenderer = fill;

        // Created on every row, but enabled per-frame only on the next-to-execute row.
        row.Digits = BuildText(root.transform, "Digits",
            new Vector2(barWidth * 0.5f + 0.45f, 0f), digitFontSize);

        root.SetActive(false);
        return row;
    }

    private TextMeshPro BuildText(Transform parent, string name, Vector2 localPos, float size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var text = go.AddComponent<TextMeshPro>();
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(1.2f, rowHeight);
        text.GetComponent<MeshRenderer>().sortingOrder = 12;
        return text;
    }

    private void LateUpdate()
    {
        var entries = queue.Entries;
        float delay = Mathf.Max(queue.DelaySeconds, 0.0001f);

        for (int i = 0; i < rows.Length; i++)
        {
            bool used = i < entries.Count;
            rows[i].Root.SetActive(used);
            if (!used) continue;

            float remaining = entries[i].Remaining;
            float frac = Mathf.Clamp01(remaining / delay);

            rows[i].Icon.text = entries[i].Command.DisplayLabel;

            // Bar drains toward its left edge as time runs out.
            float fillWidth = barWidth * frac;
            rows[i].Fill.localScale = new Vector3(fillWidth, barHeight, 1f);
            rows[i].Fill.localPosition = new Vector3(-barWidth * 0.5f + fillWidth * 0.5f, 0f, 0f);

            Color color = frac >= 0.5f
                ? Color.Lerp(MidColor, FullColor, (frac - 0.5f) * 2f)
                : Color.Lerp(EmptyColor, MidColor, frac * 2f);

            bool isNext = i == 0;
            // The imminent action flashes toward white in its last half second.
            if (isNext && remaining < 0.5f)
                color = Color.Lerp(color, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.time * 16f));
            rows[i].FillRenderer.color = color;

            rows[i].Digits.gameObject.SetActive(isNext);
            if (isNext)
                rows[i].Digits.text = remaining.ToString("0.00", CultureInfo.InvariantCulture);
        }
    }
}
