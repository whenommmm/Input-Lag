# Input Lag Core Mechanic Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the delayed-execution command queue mechanic — immediate movement, queued Jump/Dash that fire after a level-wide countdown, live queue UI above the player, graybox test scene.

**Architecture:** One-directional data flow: `PlayerInputHandler → CommandQueue → IPlayerCommand.Execute(PlayerMotor)`, with `CommandQueueUI` as a pure observer of the queue. Commands are plain C# classes that check their own validity internally; the queue only waits → executes → removes → fires events. The test scene is built by an editor script so it is reproducible and reviewable.

**Tech Stack:** Unity 6000.3.2f1, 2D URP, Input System 1.17 (existing `Assets/InputSystem_Actions.inputactions`), TextMeshPro world text.

**Spec:** `docs/superpowers/specs/2026-07-23-input-lag-core-mechanic-design.md` — read it before starting. All gameplay decisions there are locked.

## Global Constraints

- Unity version: **6000.3.2f1**. Use `Rigidbody2D.linearVelocity` (the `velocity` property is deprecated in Unity 6).
- All runtime scripts live under `Assets/Assets/Scripts/` (this doubled path is the project's existing convention). Editor scripts under `Assets/Assets/Scripts/Editor/`.
- New Input System only — never `Input.GetKey`/legacy input.
- No namespaces (jam convention for this project), no new packages, no test framework. Per the approved spec, verification is: **compile-clean after every task + the manual play checklist in Task 7**. This deviates from TDD deliberately — the spec's verification section was user-approved.
- Queue rules (spec, verbatim): max 3 entries; full queue rejects input, never overwrites; validity is checked at execution *inside the command* — the queue never knows why a command succeeds or fails; airborne Jump at fire-time does nothing (wasted); Dash direction = facing at execution; air dash allowed; any command executing cancels an active dash first.
- `delaySeconds` default = 2 (the per-level knob).
- Unity writes a `.meta` file for every new file/folder after it refocuses and compiles. When committing, `git add` the created paths **and their `.meta` files if present**; if metas appear later, sweep them into the next commit.

### Compile check (used by every task)

If the Unity editor is **open**: switch to it, let it recompile, confirm the Console shows no errors (red entries).

If the editor is **closed**, run:

```bash
"/Applications/Unity/Hub/Editor/6000.3.2f1/Unity.app/Contents/MacOS/Unity" \
  -batchmode -quit -projectPath "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)" \
  -logFile - 2>&1 | grep -E "error CS|Compilation failed"; echo "grep exit: $?"
```

Expected: no `error CS` lines printed, `grep exit: 1` (grep found nothing). Any `error CS` line = compile failure; fix before committing. (Batchmode fails with "already open" if the editor is running — that's your cue to use the Console instead.)

---

### Task 1: PlayerMotor

**Files:**
- Create: `Assets/Assets/Scripts/Player/PlayerMotor.cs`

**Interfaces:**
- Consumes: nothing (foundation task).
- Produces (later tasks call exactly these):
  - `bool IsGrounded { get; }`
  - `bool IsDashing { get; }`
  - `int FacingDirection { get; }` — `+1` right / `-1` left, never 0, defaults `+1`
  - `void SetMoveInput(float direction)` — clamped to [-1, 1]
  - `void Jump()` — unconditional impulse; callers decide validity
  - `void Dash(int direction)` — horizontal burst; restarts if already dashing
  - Serialized fields (Task 7's scene builder sets these via `SerializedObject`): `groundLayer`

- [ ] **Step 1: Write PlayerMotor.cs**

```csharp
using UnityEngine;

/// <summary>
/// All player physics: walking, jumping, dashing, grounded state.
/// Knows nothing about input devices or the command queue.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float jumpVelocity = 14f;

    [Header("Dash")]
    [SerializeField] private float dashSpeed = 20f;
    [SerializeField] private float dashDuration = 0.15f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.9f, 0.1f);
    [SerializeField] private float groundCheckOffset = 0.5f;

    private Rigidbody2D body;
    private float moveInput;
    private float dashTimeLeft;
    private int dashDirection;
    private float defaultGravityScale;

    public bool IsGrounded { get; private set; }
    public bool IsDashing => dashTimeLeft > 0f;

    /// <summary>+1 facing right, -1 facing left. Never 0.</summary>
    public int FacingDirection { get; private set; } = 1;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        defaultGravityScale = body.gravityScale;
    }

    public void SetMoveInput(float direction)
    {
        moveInput = Mathf.Clamp(direction, -1f, 1f);
        if (moveInput > 0.01f) FacingDirection = 1;
        else if (moveInput < -0.01f) FacingDirection = -1;
    }

    /// <summary>Unconditional upward impulse. Validity lives in the caller (see JumpCommand).</summary>
    public void Jump()
    {
        CancelDash(); // spec: any command executing cancels an active dash first
        body.linearVelocity = new Vector2(body.linearVelocity.x, jumpVelocity);
    }

    /// <summary>Horizontal burst with gravity suspended. Re-dashing restarts the timer.</summary>
    public void Dash(int direction)
    {
        CancelDash();
        dashDirection = direction;
        dashTimeLeft = dashDuration;
        body.gravityScale = 0f;
    }

    private void CancelDash()
    {
        if (!IsDashing) return;
        dashTimeLeft = 0f;
        body.gravityScale = defaultGravityScale;
    }

    private void FixedUpdate()
    {
        Vector2 checkCenter = (Vector2)transform.position + Vector2.down * groundCheckOffset;
        IsGrounded = Physics2D.OverlapBox(checkCenter, groundCheckSize, 0f, groundLayer) != null;

        if (IsDashing)
        {
            // Move input is intentionally ignored while dashing (spec).
            body.linearVelocity = new Vector2(dashSpeed * dashDirection, 0f);
            dashTimeLeft -= Time.fixedDeltaTime;
            if (!IsDashing) body.gravityScale = defaultGravityScale;
        }
        else
        {
            body.linearVelocity = new Vector2(moveInput * moveSpeed, body.linearVelocity.y);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector2 checkCenter = (Vector2)transform.position + Vector2.down * groundCheckOffset;
        Gizmos.DrawWireCube(checkCenter, groundCheckSize);
    }
}
```

Tuning rationale (documented so nobody "fixes" it): with the scene's `gravityScale = 4` (set in Task 7), `jumpVelocity 14` gives ≈2.5 units of jump height and ≈0.72 s airtime; `moveSpeed 8` gives ≈5.7 units of horizontal jump reach; `dashSpeed 20 × dashDuration 0.15` adds 3 units. The Task 7 gap is 7 units: jump alone fails, jump→dash clears.

- [ ] **Step 2: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: PlayerMotor - movement, jump, dash, grounded check"
```

---

### Task 2: Command contract (CommandType, IPlayerCommand, JumpCommand, DashCommand)

**Files:**
- Create: `Assets/Assets/Scripts/Commands/CommandType.cs`
- Create: `Assets/Assets/Scripts/Commands/IPlayerCommand.cs`
- Create: `Assets/Assets/Scripts/Commands/JumpCommand.cs`
- Create: `Assets/Assets/Scripts/Commands/DashCommand.cs`

**Interfaces:**
- Consumes: `PlayerMotor.IsGrounded`, `PlayerMotor.Jump()`, `PlayerMotor.Dash(int)`, `PlayerMotor.FacingDirection` (Task 1).
- Produces:
  - `enum CommandType { Jump, Dash }`
  - `interface IPlayerCommand { CommandType Type { get; } string DisplayLabel { get; } void Execute(PlayerMotor motor); }`
  - `class JumpCommand : IPlayerCommand`, `class DashCommand : IPlayerCommand` — both stateless with parameterless constructors.

- [ ] **Step 1: Write CommandType.cs**

```csharp
/// <summary>
/// Identity for queued commands. Future systems key off this enum,
/// never off display strings.
/// </summary>
public enum CommandType
{
    Jump,
    Dash
}
```

- [ ] **Step 2: Write IPlayerCommand.cs**

```csharp
/// <summary>
/// A queueable player action. Validity is checked internally at execution
/// time: an invalid command simply does nothing (e.g. Jump while airborne
/// is wasted). The queue stays generic — it never knows why a command
/// succeeded or failed.
/// </summary>
public interface IPlayerCommand
{
    CommandType Type { get; }

    /// <summary>UI display only — never use for identity or logic.</summary>
    string DisplayLabel { get; }

    void Execute(PlayerMotor motor);
}
```

- [ ] **Step 3: Write JumpCommand.cs**

```csharp
public class JumpCommand : IPlayerCommand
{
    public CommandType Type => CommandType.Jump;
    public string DisplayLabel => "↑";

    public void Execute(PlayerMotor motor)
    {
        // Airborne at fire-time -> the action is wasted (locked design decision).
        if (motor.IsGrounded)
            motor.Jump();
    }
}
```

- [ ] **Step 4: Write DashCommand.cs**

```csharp
public class DashCommand : IPlayerCommand
{
    public CommandType Type => CommandType.Dash;
    public string DisplayLabel => "→";

    public void Execute(PlayerMotor motor)
    {
        // Direction resolves at execution time: the player queues the WHEN
        // and steers the WHERE with immediate movement (locked design decision).
        motor.Dash(motor.FacingDirection);
    }
}
```

- [ ] **Step 5: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 6: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: command contract - CommandType, IPlayerCommand, Jump/Dash commands"
```

---

### Task 3: CommandQueue

**Files:**
- Create: `Assets/Assets/Scripts/Commands/CommandQueue.cs` (also contains the `QueuedCommand` struct — they change together)

**Interfaces:**
- Consumes: `IPlayerCommand` (Task 2), `PlayerMotor` (Task 1, as the serialized `motor` field passed to `Execute`).
- Produces:
  - `bool Enqueue(IPlayerCommand command)` — false + `CommandRejected` event when full
  - `IReadOnlyList<QueuedCommand> Entries`
  - `int MaxQueueSize { get; }`
  - `event Action<QueuedCommand> CommandQueued`, `event Action<QueuedCommand> CommandExecuted`, `event Action<IPlayerCommand> CommandRejected`
  - `readonly struct QueuedCommand { IPlayerCommand Command { get; } float ExecuteAt { get; } float Remaining { get; } }`
  - Serialized fields (Task 7 sets via `SerializedObject`): `motor`

- [ ] **Step 1: Write CommandQueue.cs**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The countdown heart of the game. Accepts commands instantly, waits
/// delaySeconds, then executes them FIFO. Fully generic: its only job is
/// wait -> execute -> remove -> fire events. It never knows whether a
/// command's internal validity check passed.
/// </summary>
public class CommandQueue : MonoBehaviour
{
    [Tooltip("Level-wide execution delay in seconds. This is the per-level difficulty knob.")]
    [SerializeField] private float delaySeconds = 2f;
    [SerializeField] private int maxQueueSize = 3;
    [SerializeField] private PlayerMotor motor;

    private readonly List<QueuedCommand> entries = new List<QueuedCommand>();

    public IReadOnlyList<QueuedCommand> Entries => entries;
    public int MaxQueueSize => maxQueueSize;

    public event Action<QueuedCommand> CommandQueued;
    public event Action<QueuedCommand> CommandExecuted;
    public event Action<IPlayerCommand> CommandRejected;

    /// <summary>
    /// Returns false (and fires CommandRejected) when the queue is full.
    /// Existing entries are never overwritten.
    /// </summary>
    public bool Enqueue(IPlayerCommand command)
    {
        if (entries.Count >= maxQueueSize)
        {
            CommandRejected?.Invoke(command);
            return false;
        }

        var entry = new QueuedCommand(command, Time.time + delaySeconds);
        entries.Add(entry);
        CommandQueued?.Invoke(entry);
        return true;
    }

    private void Update()
    {
        // The delay is constant per level, so entries are always in ExecuteAt
        // order and FIFO falls out for free. Frame-granularity firing (~16 ms
        // late worst case) is acceptable per spec.
        while (entries.Count > 0 && entries[0].ExecuteAt <= Time.time)
        {
            QueuedCommand entry = entries[0];
            entries.RemoveAt(0);
            entry.Command.Execute(motor);
            CommandExecuted?.Invoke(entry);
        }
    }
}

/// <summary>A command paired with its execution timestamp.</summary>
public readonly struct QueuedCommand
{
    public IPlayerCommand Command { get; }
    public float ExecuteAt { get; }
    public float Remaining => Mathf.Max(0f, ExecuteAt - Time.time);

    public QueuedCommand(IPlayerCommand command, float executeAt)
    {
        Command = command;
        ExecuteAt = executeAt;
    }
}
```

- [ ] **Step 2: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: CommandQueue - countdown, FIFO execution, max 3, events"
```

---

### Task 4: Dash action in the input asset

**Files:**
- Modify: `Assets/InputSystem_Actions.inputactions` (JSON — three text edits in the `Player` map)

**Interfaces:**
- Consumes: nothing.
- Produces: a `Dash` Button action in the `Player` action map, bound to `<Keyboard>/leftShift` and `<Gamepad>/buttonWest`. Task 5 looks it up with `FindAction("Dash")`.

Context: the default asset already binds Left Shift to `Sprint` (binding id `f2e9ba44-…`). Our design doesn't use Sprint, so that binding is **repointed** to Dash rather than double-binding the key. The other Sprint bindings (left stick press, XR trigger) stay on Sprint and are harmless.

- [ ] **Step 1: Add the Dash action.** In the `Player` map's `"actions"` array, the `Sprint` action is the last entry. Replace this exact block:

```json
                {
                    "name": "Sprint",
                    "type": "Button",
                    "id": "641cd816-40e6-41b4-8c3d-04687c349290",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                }
            ],
```

with:

```json
                {
                    "name": "Sprint",
                    "type": "Button",
                    "id": "641cd816-40e6-41b4-8c3d-04687c349290",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                },
                {
                    "name": "Dash",
                    "type": "Button",
                    "id": "3f6a2b1c-9d4e-4f5a-8b7c-2e1d0a9f8c6b",
                    "expectedControlType": "Button",
                    "processors": "",
                    "interactions": "",
                    "initialStateCheck": false
                }
            ],
```

- [ ] **Step 2: Repoint Left Shift from Sprint to Dash.** In the binding whose `"id"` is `f2e9ba44-c423-42a7-ad56-f20975884794` (path `<Keyboard>/leftShift`), change the line

```json
                    "action": "Sprint",
```

to

```json
                    "action": "Dash",
```

(There are three `"action": "Sprint"` lines in the file — only the one inside the `f2e9ba44…` / `leftShift` block changes. Anchor the edit on the id line.)

- [ ] **Step 3: Add the gamepad binding.** The last binding in the `Player` map's `"bindings"` array is the Crouch `<Keyboard>/c` entry, followed by the array close. Replace this exact block:

```json
                {
                    "name": "",
                    "id": "36e52cba-0905-478e-a818-f4bfcb9f3b9a",
                    "path": "<Keyboard>/c",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard&Mouse",
                    "action": "Crouch",
                    "isComposite": false,
                    "isPartOfComposite": false
                }
            ]
        },
```

with:

```json
                {
                    "name": "",
                    "id": "36e52cba-0905-478e-a818-f4bfcb9f3b9a",
                    "path": "<Keyboard>/c",
                    "interactions": "",
                    "processors": "",
                    "groups": "Keyboard&Mouse",
                    "action": "Crouch",
                    "isComposite": false,
                    "isPartOfComposite": false
                },
                {
                    "name": "",
                    "id": "7c8d9e0f-1a2b-4c3d-8e5f-6a7b8c9d0e1f",
                    "path": "<Gamepad>/buttonWest",
                    "interactions": "",
                    "processors": "",
                    "groups": "Gamepad",
                    "action": "Dash",
                    "isComposite": false,
                    "isPartOfComposite": false
                }
            ]
        },
```

- [ ] **Step 4: Validate the JSON**

```bash
python3 -m json.tool "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)/Assets/InputSystem_Actions.inputactions" > /dev/null && echo VALID
grep -c '"name": "Dash"' "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)/Assets/InputSystem_Actions.inputactions"
```

Expected: `VALID`, then `1`. If the Unity editor is open, also confirm it reimports the asset without Console errors and the Player map shows Dash (Left Shift + Button West) in the Input Actions window.

- [ ] **Step 5: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/InputSystem_Actions.inputactions
git commit -m "feat: add Dash action (LShift repointed from Sprint, gamepad west)"
```

---

### Task 5: PlayerInputHandler

**Files:**
- Create: `Assets/Assets/Scripts/Player/PlayerInputHandler.cs`

**Interfaces:**
- Consumes: `PlayerMotor.SetMoveInput(float)` (Task 1); `CommandQueue.Enqueue(IPlayerCommand)` (Task 3); `JumpCommand`/`DashCommand` parameterless constructors (Task 2); `Player` map actions `Move`/`Jump`/`Dash` (Task 4).
- Produces: nothing consumed by later code tasks. Serialized field `actions` (`InputActionAsset`) is set by Task 7's scene builder.

- [ ] **Step 1: Write PlayerInputHandler.cs**

```csharp
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Translates device input into motor calls (movement — immediate) and
/// queued commands (specials — delayed). The only class that touches the
/// Input System.
/// </summary>
[RequireComponent(typeof(PlayerMotor), typeof(CommandQueue))]
public class PlayerInputHandler : MonoBehaviour
{
    [SerializeField] private InputActionAsset actions;

    // Commands are stateless, so one shared instance per type is enough.
    private static readonly JumpCommand jumpCommand = new JumpCommand();
    private static readonly DashCommand dashCommand = new DashCommand();

    private PlayerMotor motor;
    private CommandQueue queue;
    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction dashAction;

    private void Awake()
    {
        motor = GetComponent<PlayerMotor>();
        queue = GetComponent<CommandQueue>();
        playerMap = actions.FindActionMap("Player", throwIfNotFound: true);
        moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
        jumpAction = playerMap.FindAction("Jump", throwIfNotFound: true);
        dashAction = playerMap.FindAction("Dash", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        jumpAction.performed += OnJump;
        dashAction.performed += OnDash;
        playerMap.Enable();
    }

    private void OnDisable()
    {
        jumpAction.performed -= OnJump;
        dashAction.performed -= OnDash;
        playerMap.Disable();
    }

    private void Update()
    {
        // Movement is immediate by design — it never goes through the queue.
        motor.SetMoveInput(moveAction.ReadValue<Vector2>().x);
    }

    private void OnJump(InputAction.CallbackContext context) => queue.Enqueue(jumpCommand);
    private void OnDash(InputAction.CallbackContext context) => queue.Enqueue(dashCommand);
}
```

- [ ] **Step 2: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: PlayerInputHandler - immediate move, Jump/Dash enqueue"
```

---

### Task 6: CommandQueueUI

**Files:**
- Create: `Assets/Assets/Scripts/UI/CommandQueueUI.cs`

**Interfaces:**
- Consumes: `CommandQueue.Entries`, `CommandQueue.MaxQueueSize`, `QueuedCommand.Command.DisplayLabel`, `QueuedCommand.Remaining` (Task 3).
- Produces: nothing consumed by later code. Serialized field `queue` is set by Task 7's scene builder. Builds its own TextMeshPro row objects at runtime — no prefab, no Canvas; deleting the component never breaks gameplay.

Design note: the spec's "world-space canvas" is implemented as TextMeshPro world-text rows (the mesh-renderer TMP component, not a Canvas). Same world-space visual, fewer moving parts. The UI polls `Entries` every frame — countdown text has to update per-frame anyway, and with ≤3 rows polling structure too is simpler than event bookkeeping. The queue's events stay as the public API for future feedback (sound/VFX/reject flash).

- [ ] **Step 1: Write CommandQueueUI.cs**

```csharp
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
```

Glyph fallback (from spec): if `①`/`↑`/`→` render as empty boxes in play mode, the default TMP font atlas lacks them. Fix is strings-only: change `OrderGlyphs` to `{ "1.", "2.", "3." }` here and `DisplayLabel` to `"JMP"`/`"DSH"` in `JumpCommand.cs`/`DashCommand.cs`. Checked in Task 7 verification.

- [ ] **Step 2: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts
git commit -m "feat: CommandQueueUI - numbered world-space countdown rows"
```

---

### Task 7: Test scene builder + full manual verification

**Files:**
- Create: `Assets/Assets/Scripts/Editor/TestSceneBuilder.cs`
- Generated by running it: `Assets/Scenes/TestScene.unity`, `Assets/Assets/Sprites/Square.png`, a `Ground` layer in `ProjectSettings/TagManager.asset`

**Interfaces:**
- Consumes: every component built so far, wired via `SerializedObject` on their private serialized fields: `PlayerMotor.groundLayer`, `CommandQueue.motor`, `PlayerInputHandler.actions`, `CommandQueueUI.queue`.
- Produces: the playable graybox scene. Layout: left ground spans x[-12..2], right ground x[9..14] → **7-unit gap** (jump reach ≈5.7 fails; jump→dash ≈8.7 clears); three 3×0.5 platforms at (-9, 1.5), (-5.5, 3), (-2, 4.5), each within the 2.5-unit jump height of the previous; player spawns at (-11, 1); static ortho camera size 7 at (1, 4.5).

- [ ] **Step 1: Write TestSceneBuilder.cs**

```csharp
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the graybox test scene from code so it is reproducible and
/// reviewable. Run via Tools > Input Lag > Build Test Scene. Re-running
/// overwrites the scene. Editor-only (lives in an Editor folder).
/// </summary>
public static class TestSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/TestScene.unity";
    private const string SpritePath = "Assets/Assets/Sprites/Square.png";
    private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
    private const string GroundLayerName = "Ground";

    private static readonly Color GroundColor = new Color(0.35f, 0.35f, 0.35f);
    private static readonly Color PlatformColor = new Color(0.55f, 0.55f, 0.55f);

    [MenuItem("Tools/Input Lag/Build Test Scene")]
    public static void Build()
    {
        EnsureGroundLayer();
        Sprite square = EnsureSquareSprite();
        int groundLayer = LayerMask.NameToLayer(GroundLayerName);

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCamera();
        BuildGlobalLight();

        // Left ground x[-12..2], right ground x[9..14]: a 7-unit gap that only
        // jump->dash clears (jump reach ~5.7, jump+dash ~8.7).
        BuildBox("Ground Left", square, new Vector2(-5f, -0.5f), new Vector2(14f, 1f), groundLayer, GroundColor);
        BuildBox("Ground Right", square, new Vector2(11.5f, -0.5f), new Vector2(5f, 1f), groundLayer, GroundColor);

        // Each platform top is ~1.5-1.75 units above the previous surface,
        // inside the ~2.5-unit jump height.
        BuildBox("Platform 1", square, new Vector2(-9f, 1.5f), new Vector2(3f, 0.5f), groundLayer, PlatformColor);
        BuildBox("Platform 2", square, new Vector2(-5.5f, 3f), new Vector2(3f, 0.5f), groundLayer, PlatformColor);
        BuildBox("Platform 3", square, new Vector2(-2f, 4.5f), new Vector2(3f, 0.5f), groundLayer, PlatformColor);

        BuildPlayer(square, new Vector2(-11f, 1f), groundLayer);

        EditorSceneManager.SaveScene(scene, ScenePath);
        Debug.Log($"Test scene built and saved to {ScenePath}");
    }

    private static void EnsureGroundLayer()
    {
        var tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
            if (layers.GetArrayElementAtIndex(i).stringValue == GroundLayerName)
                return;

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = GroundLayerName;
                tagManager.ApplyModifiedProperties();
                return;
            }
        }
        throw new System.InvalidOperationException("No free layer slot for the Ground layer.");
    }

    private static Sprite EnsureSquareSprite()
    {
        if (AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath) == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
            var tex = new Texture2D(32, 32);
            var pixels = new Color32[32 * 32];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(pixels);
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(SpritePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spritePixelsPerUnit = 32; // 1 sprite == 1 world unit
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    private static void BuildCamera()
    {
        var go = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.12f, 0.15f);
        go.transform.position = new Vector3(1f, 4.5f, -10f);
        go.AddComponent<AudioListener>();
    }

    private static void BuildGlobalLight()
    {
        // URP 2D's default sprite material is lit; without a global light
        // everything renders black.
        var go = new GameObject("Global Light 2D");
        var light = go.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
    }

    private static void BuildBox(string name, Sprite sprite, Vector2 position,
        Vector2 size, int layer, Color color)
    {
        var go = new GameObject(name) { layer = layer, isStatic = true };
        go.transform.position = position;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        go.AddComponent<BoxCollider2D>(); // auto-sizes to the 1x1 sprite, scaled by transform
    }

    private static void BuildPlayer(Sprite sprite, Vector2 position, int groundLayer)
    {
        var go = new GameObject("Player");
        go.transform.position = position;

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 5;

        var body = go.AddComponent<Rigidbody2D>();
        body.gravityScale = 4f; // motor tuning (jumpVelocity 14) assumes this
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        go.AddComponent<BoxCollider2D>();

        var motor = go.AddComponent<PlayerMotor>();
        var motorSo = new SerializedObject(motor);
        motorSo.FindProperty("groundLayer").intValue = 1 << groundLayer;
        motorSo.ApplyModifiedPropertiesWithoutUndo();

        var queue = go.AddComponent<CommandQueue>();
        var queueSo = new SerializedObject(queue);
        queueSo.FindProperty("motor").objectReferenceValue = motor;
        queueSo.ApplyModifiedPropertiesWithoutUndo();

        var input = go.AddComponent<PlayerInputHandler>();
        var inputSo = new SerializedObject(input);
        inputSo.FindProperty("actions").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
        inputSo.ApplyModifiedPropertiesWithoutUndo();

        var ui = go.AddComponent<CommandQueueUI>();
        var uiSo = new SerializedObject(ui);
        uiSo.FindProperty("queue").objectReferenceValue = queue;
        uiSo.ApplyModifiedPropertiesWithoutUndo();
    }
}
```

- [ ] **Step 2: Compile check** — per Global Constraints. Expected: no errors.

- [ ] **Step 3: Build the scene.** In the Unity editor: **Tools > Input Lag > Build Test Scene**. Expected: Console logs "Test scene built and saved to Assets/Scenes/TestScene.unity"; Hierarchy shows Main Camera, Global Light 2D, Ground Left/Right, Platforms 1–3, Player. (If TextMeshPro complains about missing resources when you press Play in Step 4, run **Window > TextMeshPro > Import TMP Essential Resources** once, then re-test.)

- [ ] **Step 4: Run the manual verification checklist** (from the spec — all 8 must pass; `delaySeconds` is 2):

1. Walking (A/D) feels immediate at all times — including with a full queue and mid-jump.
2. Press Jump (Space): it fires exactly ~2 s later, on the ground. Queue several: they fire in press order, each 2 s after its own press.
3. With 3 actions queued, a 4th press is ignored — the queue never shows more than 3 rows and nothing is overwritten.
4. Walk off a ledge right after queueing a jump: at fire-time airborne → nothing happens (wasted, no impulse).
5. Stand at the left edge of the gap. Queue Jump then Dash quickly, walk toward the gap, jump fires, dash fires mid-air → clears the 7-unit gap. Verify jump alone falls in.
6. Queue a dash while facing right, then turn left before it fires → dash goes left (facing at execution).
7. UI shows numbered rows (`① ↑ 2.0` style), next-to-execute on top, countdowns tick in real time, rows shift up and renumber on execution, display empties when idle. **If glyphs show as empty boxes**, apply the strings-only fallback from Task 6 (`1.`/`JMP`/`DSH`) and re-test.
8. Stop play, set `delaySeconds` to 5 on the Player's CommandQueue, play again → all countdowns start at 5.0. Set it back to 2.

- [ ] **Step 5: Fix anything that fails** (tuning values are inspector-tweakable on the Player; if you change a serialized default, mirror it in the script so the scene builder stays reproducible, and re-run the builder).

- [ ] **Step 6: Commit**

```bash
cd "/Users/vanshsrivastava/Unity/GMTK(Input-Lag)"
git add Assets/Assets/Scripts Assets/Assets/Sprites Assets/Scenes/TestScene.unity ProjectSettings/TagManager.asset
git add -A Assets  # sweep in any .meta files Unity generated
git commit -m "feat: graybox test scene builder + built scene"
```

---

## Milestone summary requirement

Per the project brief ("work incrementally"), after Tasks 1–3 (core systems), Task 5 (input), and Task 7 (playable), post a short summary to the user: what was implemented, what to test. Task 7's checklist is the full acceptance gate for today's deliverables.
