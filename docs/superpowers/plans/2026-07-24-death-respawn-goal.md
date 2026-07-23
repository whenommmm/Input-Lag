# Death, Respawn & Goal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the play loop in the test scene: fall → Souls-style "YOU DIED" banner → respawn at last-touched checkpoint → reach the green goal → "LEVEL COMPLETE" → scene restarts.

**Architecture:** A `LevelManager` state machine (`Playing/Dying/Winning`) orchestrates everything; `Checkpoint`/`KillZone`/`LevelGoal` are dumb trigger notifiers; `BannerUI` runtime-builds a screen-space overlay (same pattern as the queue UI). Three tiny API additions to existing classes (`Teleport`, `Clear`, `SnapToTarget`). The scene builder wires it all.

**Tech Stack:** Unity 6000.3.2f1, 2D URP, uGUI + TMP (already imported), existing command-queue stack.

**Spec:** `docs/superpowers/specs/2026-07-24-death-respawn-goal-design.md` — binding; read before starting.

## Global Constraints

- Unity 6000.3.2f1; `Rigidbody2D.linearVelocity` (never the deprecated `velocity`).
- Runtime scripts under `Assets/Assets/Scripts/` (project convention, doubled path); new level scripts go in `Assets/Assets/Scripts/Level/`; no namespaces; no new packages; no test framework (user-approved — verification is the manual checklist in Task 4).
- Locked values (spec, verbatim): banner timings fade-in **0.3 s** / hold **0.8 s** / fade-out **0.3 s** (all serialized); queue **cleared** on death AND on win; control cut = disable the `PlayerInputHandler` component + `motor.SetMoveInput(0f)`; respawn = last checkpoint touched, initial checkpoint at player spawn; trigger events outside the `Playing` state are ignored; teleport sets BOTH `body.position` and `transform.position` (interpolation smear); goal restarts the scene when `nextSceneName` is empty.
- Scene geometry (locked): KillZone center (0, −7) size (60 × 2); Checkpoint A (−11, 1) trigger (1 × 3); Checkpoint B (0.5, 1.5) trigger (1 × 3); Goal at (13, 0.75), visible green block scaled (1 × 1.5) with auto-sized trigger collider.
- Player detection in triggers: `other.GetComponent<PlayerMotor>() != null` — no tags/layers.
- The Unity editor is typically OPEN during this work: the batchmode compile check fails with "already open in another instance" — that is the cue to verify via the editor Console instead. Never claim compilation succeeded without one of the two.
- Unity generates `.meta` files for new files/folders when it refocuses — include any that `git status` shows under `Assets/` in your commit; sweep stragglers into the next commit.

### Compile check (used by every task)

Editor open → switch to Unity, let it recompile, Console must show no errors.
Editor closed → run:

```bash
"/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit -projectPath "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)" \
  -logFile - 2>&1 | grep -E "error CS|Compilation failed"; echo "grep exit: $?"
```

Expected: no `error CS` lines, `grep exit: 1`.

---

### Task 1: API additions — Teleport, Clear, SnapToTarget

**Files:**
- Modify: `Assets/Assets/Scripts/Player/PlayerMotor.cs` (add one method after `Dash`)
- Modify: `Assets/Assets/Scripts/Commands/CommandQueue.cs` (add one method after `Enqueue`)
- Modify: `Assets/Assets/Scripts/Camera/CameraFollow.cs` (add one method after `LateUpdate`)

**Interfaces:**
- Consumes: existing private members — `PlayerMotor.body` (Rigidbody2D), `PlayerMotor.CancelDash()`, `CommandQueue.entries` (List), `CameraFollow.target/offset/velocity`.
- Produces (Task 3's LevelManager calls exactly these):
  - `void PlayerMotor.Teleport(Vector2 position)`
  - `void CommandQueue.Clear()`
  - `void CameraFollow.SnapToTarget()`

- [ ] **Step 1: Add `Teleport` to PlayerMotor.** In `Assets/Assets/Scripts/Player/PlayerMotor.cs`, directly after the closing brace of `public void Dash(int direction) { ... }`, insert:

```csharp
    /// <summary>
    /// Respawn-style reset: cancels any dash (restoring gravity), zeroes
    /// velocity, and moves the body. Sets transform.position too so
    /// rigidbody interpolation can't smear the jump across frames.
    /// </summary>
    public void Teleport(Vector2 position)
    {
        CancelDash();
        body.linearVelocity = Vector2.zero;
        body.position = position;
        transform.position = position;
    }
```

- [ ] **Step 2: Add `Clear` to CommandQueue.** In `Assets/Assets/Scripts/Commands/CommandQueue.cs`, directly after the closing brace of `public bool Enqueue(IPlayerCommand command) { ... }`, insert:

```csharp
    /// <summary>
    /// Empties the queue (e.g. on death — a death wipes your plan). Fires no
    /// events; the polling UI empties on the next frame automatically.
    /// </summary>
    public void Clear()
    {
        entries.Clear();
    }
```

- [ ] **Step 3: Add `SnapToTarget` to CameraFollow.** In `Assets/Assets/Scripts/Camera/CameraFollow.cs`, directly after the closing brace of `private void LateUpdate() { ... }`, insert:

```csharp
    /// <summary>Jump straight to the follow position (e.g. after a respawn teleport).</summary>
    public void SnapToTarget()
    {
        if (target == null) return;
        velocity = Vector3.zero;
        transform.position = target.position + offset;
    }
```

- [ ] **Step 4: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 5: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: Teleport/Clear/SnapToTarget APIs for respawn"
```

---

### Task 2: BannerUI

**Files:**
- Create: `Assets/Assets/Scripts/UI/BannerUI.cs`

**Interfaces:**
- Consumes: uGUI (`Canvas`, `CanvasScaler`, `CanvasGroup`, `Image`) and TMP (`TextMeshProUGUI`) — both already in the project.
- Produces (Task 3 calls exactly this):
  - `void Play(string text, Color stripColor, Action onFadeOutStart, Action onComplete)` — fade in → hold → invoke `onFadeOutStart` → fade out → invoke `onComplete`. Ignores calls while already playing.
  - `bool IsPlaying { get; }`

- [ ] **Step 1: Write BannerUI.cs**

```csharp
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
```

- [ ] **Step 2: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: BannerUI - runtime-built Souls-style banner overlay"
```

---

### Task 3: Level scripts — LevelManager, Checkpoint, KillZone, LevelGoal

**Files:**
- Create: `Assets/Assets/Scripts/Level/LevelManager.cs`
- Create: `Assets/Assets/Scripts/Level/Checkpoint.cs`
- Create: `Assets/Assets/Scripts/Level/KillZone.cs`
- Create: `Assets/Assets/Scripts/Level/LevelGoal.cs`

**Interfaces:**
- Consumes: `PlayerMotor.Teleport(Vector2)` / `SetMoveInput(float)`, `CommandQueue.Clear()`, `CameraFollow.SnapToTarget()` (Task 1), `BannerUI.Play(string, Color, Action, Action)` (Task 2), `PlayerInputHandler` (component enable/disable — its existing OnEnable/OnDisable handle subscription and action-map state).
- Produces (Task 4's builder wires these serialized fields via SerializedObject):
  - `LevelManager` fields: `motor`, `queue`, `inputHandler`, `cameraFollow`, `banner`, `initialCheckpoint`, `nextSceneName` (string, default "")
  - `LevelManager` public methods: `PlayerDied()`, `CheckpointReached(Checkpoint)`, `GoalReached()`
  - `Checkpoint`/`KillZone`/`LevelGoal` field: `levelManager`

- [ ] **Step 1: Write LevelManager.cs**

```csharp
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Orchestrates the play loop: death -> YOU DIED banner -> respawn at the
/// active checkpoint; goal -> LEVEL COMPLETE banner -> next scene (or scene
/// restart). The state machine makes overlapping trigger events no-ops by
/// construction: only Playing accepts kill/checkpoint/goal notifications.
/// </summary>
public class LevelManager : MonoBehaviour
{
    private enum State { Playing, Dying, Winning }

    [SerializeField] private PlayerMotor motor;
    [SerializeField] private CommandQueue queue;
    [SerializeField] private PlayerInputHandler inputHandler;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private BannerUI banner;
    [SerializeField] private Checkpoint initialCheckpoint;
    [Tooltip("Scene to load on level complete. Empty = restart this scene.")]
    [SerializeField] private string nextSceneName = "";

    private static readonly Color DeathStripColor = new Color(0.55f, 0.05f, 0.05f, 0.8f);
    private static readonly Color WinStripColor = new Color(0.05f, 0.45f, 0.12f, 0.8f);

    private State state = State.Playing;
    private Checkpoint activeCheckpoint;

    private void Awake()
    {
        activeCheckpoint = initialCheckpoint;
    }

    public void PlayerDied()
    {
        if (state != State.Playing) return;
        state = State.Dying;
        CutControl();
        banner.Play("YOU DIED", DeathStripColor, RespawnAtCheckpoint, RestoreControl);
    }

    public void CheckpointReached(Checkpoint checkpoint)
    {
        if (state != State.Playing) return;
        activeCheckpoint = checkpoint; // last one touched wins
    }

    public void GoalReached()
    {
        if (state != State.Playing) return;
        state = State.Winning;
        CutControl();
        banner.Play("LEVEL COMPLETE", WinStripColor, null, LoadNextScene);
    }

    private void CutControl()
    {
        // Disabling the handler unsubscribes and disables the action map
        // (its OnDisable already does both). A death wipes your plan.
        inputHandler.enabled = false;
        motor.SetMoveInput(0f);
        queue.Clear();
    }

    private void RestoreControl()
    {
        inputHandler.enabled = true;
        state = State.Playing;
    }

    private void RespawnAtCheckpoint()
    {
        motor.Teleport(activeCheckpoint.transform.position);
        cameraFollow.SnapToTarget();
    }

    private void LoadNextScene()
    {
        string scene = string.IsNullOrEmpty(nextSceneName)
            ? SceneManager.GetActiveScene().name
            : nextSceneName;
        SceneManager.LoadScene(scene);
    }
}
```

- [ ] **Step 2: Write Checkpoint.cs**

```csharp
using UnityEngine;

/// <summary>
/// Invisible respawn point. Walking through it makes it the active respawn
/// (last one touched wins). Visible only as an editor gizmo — players never
/// see it.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMotor>() == null) return;
        levelManager.CheckpointReached(this);
    }

    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider2D>();
        if (box == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(transform.position + (Vector3)box.offset, box.size);
    }
}
```

- [ ] **Step 3: Write KillZone.cs**

```csharp
using UnityEngine;

/// <summary>Trigger below the map: falling in kills the player.</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class KillZone : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMotor>() == null) return;
        levelManager.PlayerDied();
    }
}
```

- [ ] **Step 4: Write LevelGoal.cs**

```csharp
using UnityEngine;

/// <summary>Level-complete trigger (the visible green block).</summary>
[RequireComponent(typeof(BoxCollider2D))]
public class LevelGoal : MonoBehaviour
{
    [SerializeField] private LevelManager levelManager;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerMotor>() == null) return;
        levelManager.GoalReached();
    }
}
```

- [ ] **Step 5: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 6: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: LevelManager state machine + checkpoint/kill/goal triggers"
```

---

### Task 4: Scene builder additions + full manual verification

**Files:**
- Modify: `Assets/Assets/Scripts/Editor/TestSceneBuilder.cs`
- Generated by re-running the builder: updated `Assets/Scenes/TestScene.unity` (committed by the controller after the human runs it)

**Interfaces:**
- Consumes: every Task 1-3 type and their serialized field names exactly as defined above; existing builder members `Build()`, `WireCameraFollow(GameObject)`, `LoadSquareSprite()`, `BuildPlayer(...)`.
- Produces: the finished scene. Geometry per Global Constraints.

- [ ] **Step 1: Make `WireCameraFollow` return the follow component.** In `Assets/Assets/Scripts/Editor/TestSceneBuilder.cs`, change the method signature and add a return (body otherwise unchanged):

Replace:
```csharp
    private static void WireCameraFollow(GameObject player)
    {
        var camera = GameObject.FindWithTag("MainCamera");
        var follow = camera.AddComponent<CameraFollow>();
```
with:
```csharp
    private static CameraFollow WireCameraFollow(GameObject player)
    {
        var camera = GameObject.FindWithTag("MainCamera");
        var follow = camera.AddComponent<CameraFollow>();
```
and at the end of that method, after the `camera.transform.position = ...` statement, add:
```csharp
        return follow;
```

- [ ] **Step 2: Call the level-systems builder from `Build()`.** Replace:

```csharp
        GameObject player = BuildPlayer(square, new Vector2(-11f, 1f), groundLayer);
        WireCameraFollow(player);
```
with:
```csharp
        GameObject player = BuildPlayer(square, new Vector2(-11f, 1f), groundLayer);
        CameraFollow follow = WireCameraFollow(player);
        BuildLevelSystems(player, follow, square);
```

- [ ] **Step 3: Add the new builder methods.** Insert directly after the closing brace of `WireCameraFollow`:

```csharp
    private static void BuildLevelSystems(GameObject player, CameraFollow follow, Sprite square)
    {
        var managerGo = new GameObject("LevelManager");
        var banner = managerGo.AddComponent<BannerUI>();
        var manager = managerGo.AddComponent<LevelManager>();

        Checkpoint checkpointA = BuildCheckpoint("Checkpoint A (spawn)",
            new Vector2(-11f, 1f), manager);
        BuildCheckpoint("Checkpoint B (pre-gap)", new Vector2(0.5f, 1.5f), manager);

        var killGo = BuildTrigger("Kill Zone", new Vector2(0f, -7f), new Vector2(60f, 2f));
        Wire(killGo.AddComponent<KillZone>(), "levelManager", manager);

        // Goal: visible green block past the jump->dash gap. Collider auto-sizes
        // to the 1x1 sprite, scaled by the transform.
        var goalGo = new GameObject("Goal");
        goalGo.transform.position = new Vector2(13f, 0.75f);
        goalGo.transform.localScale = new Vector3(1f, 1.5f, 1f);
        var goalRenderer = goalGo.AddComponent<SpriteRenderer>();
        goalRenderer.sprite = square;
        goalRenderer.color = new Color(0.25f, 0.85f, 0.35f);
        goalGo.AddComponent<BoxCollider2D>().isTrigger = true;
        Wire(goalGo.AddComponent<LevelGoal>(), "levelManager", manager);

        var managerSo = new SerializedObject(manager);
        managerSo.FindProperty("motor").objectReferenceValue =
            player.GetComponent<PlayerMotor>();
        managerSo.FindProperty("queue").objectReferenceValue =
            player.GetComponent<CommandQueue>();
        managerSo.FindProperty("inputHandler").objectReferenceValue =
            player.GetComponent<PlayerInputHandler>();
        managerSo.FindProperty("cameraFollow").objectReferenceValue = follow;
        managerSo.FindProperty("banner").objectReferenceValue = banner;
        managerSo.FindProperty("initialCheckpoint").objectReferenceValue = checkpointA;
        managerSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Checkpoint BuildCheckpoint(string name, Vector2 position, LevelManager manager)
    {
        var go = BuildTrigger(name, position, new Vector2(1f, 3f));
        var checkpoint = go.AddComponent<Checkpoint>();
        Wire(checkpoint, "levelManager", manager);
        return checkpoint;
    }

    private static GameObject BuildTrigger(string name, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        var box = go.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = size;
        return go;
    }

    private static void Wire(Component component, string field, Object value)
    {
        var so = new SerializedObject(component);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
```

Note: `Checkpoint`/`KillZone`/`LevelGoal` carry `[RequireComponent(typeof(BoxCollider2D))]`; `BuildTrigger` adds the collider first, so `AddComponent` won't add a duplicate.

- [ ] **Step 4: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 5: Commit the script**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: scene builder wires kill zone, checkpoints, goal, LevelManager"
```

- [ ] **Step 6 (human, in editor): Rebuild the scene.** Tools > Input Lag > Build Test Scene. Expected: Hierarchy gains LevelManager, Checkpoint A (spawn), Checkpoint B (pre-gap), Kill Zone, Goal (green block on the right ground).

- [ ] **Step 7 (human, in editor): Run the spec's manual verification checklist.**

1. Fall into the gap → control cuts instantly, red "YOU DIED" strip fades in center-screen, holds, fades out; player back at spawn, camera snapped, control restored. Total ≈ 1.4 s.
2. Queue Jump→Dash then walk off the edge → banner plays, **no queued action fires**, queue UI empty after respawn.
3. Walk through Checkpoint B (just before the gap), then die in the gap → respawn at Checkpoint B, not at spawn.
4. Die mid-dash → respawn is clean (no dash momentum, gravity normal).
5. Touch the green block → green "LEVEL COMPLETE" banner → scene restarts → everything still works after restart.
6. Mash Jump/Dash/movement during the death banner → nothing happens, nothing queued when control returns.
7. Checkpoints invisible in Game view; cyan wire cubes in Scene view.

- [ ] **Step 8 (controller): Commit the rebuilt scene + any new .meta files**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add -A Assets
git commit -m "feat: test scene with kill zone, checkpoints, and goal"
```
