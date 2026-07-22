using TMPro;
using UnityEngine;

/// <summary>
/// Renders queued commands above the player's head as world-space text rows:
/// "① ↑ 2.8". ① is always the next command to execute (top of the stack);
/// rows below shift up and renumber as commands fire. Pure observer of the
/// queue — the game runs fine without it.
/// </summary>
public class CommandQueueUI : MonoBehaviour
{
    [SerializeField] private CommandQueue queue;
    [SerializeField] private Vector2 headOffset = new Vector2(0f, 1.1f);
    [SerializeField] private float rowHeight = 0.55f;
    [SerializeField] private float fontSize = 4f;

    private static readonly string[] OrderGlyphs = { "①", "②", "③" };

    private TextMeshPro[] rows;

    private void Awake()
    {
        rows = new TextMeshPro[queue.MaxQueueSize];
        for (int i = 0; i < rows.Length; i++)
            rows[i] = BuildRow(i);
    }

    private TextMeshPro BuildRow(int index)
    {
        var go = new GameObject($"QueueRow{index}");
        go.transform.SetParent(transform, false);
        // Next-to-execute (index 0) sits at the TOP of the stack; later rows below.
        float y = headOffset.y + rowHeight * (rows.Length - 1 - index);
        go.transform.localPosition = new Vector3(headOffset.x, y, 0f);

        var text = go.AddComponent<TextMeshPro>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(6f, rowHeight);
        text.GetComponent<MeshRenderer>().sortingOrder = 10; // above graybox sprites
        go.SetActive(false);
        return text;
    }

    private void LateUpdate()
    {
        var entries = queue.Entries;
        for (int i = 0; i < rows.Length; i++)
        {
            bool used = i < entries.Count;
            rows[i].gameObject.SetActive(used);
            if (used)
            {
                string order = i < OrderGlyphs.Length ? OrderGlyphs[i] : $"{i + 1}.";
                rows[i].text = $"{order} {entries[i].Command.DisplayLabel} {entries[i].Remaining:0.0}";
            }
        }
    }
}
