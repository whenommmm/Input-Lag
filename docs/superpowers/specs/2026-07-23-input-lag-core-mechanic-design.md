# Input Lag — Core Mechanic Design

**Date:** 2026-07-23
**Status:** Approved
**Scope:** Core mechanic only — no levels, art, sound, or polish.

## Overview

Input Lag is a 2D puzzle-platformer for the GMTK Game Jam (theme: *Count Down*).
Movement (left/right) is always immediate. Special actions (Jump, Dash) are
accepted instantly but placed into an execution queue and fire automatically
after a level-wide countdown delay. The player programs their future movement.

This is an **execution delay system**, not a cooldown system.

## Environment

- Unity 6000.3.2f1, 2D URP template
- New Input System 1.17 (existing `Assets/InputSystem_Actions.inputactions`)
- Scripts live in `Assets/Assets/Scripts/`

## Locked design decisions

| Decision | Choice | Rationale |
|---|---|---|
| Airborne Jump at fire-time | **Discarded** (wasted) | Mis-planning has real consequences — the prediction challenge the theme demands. |
| Dash direction | **Facing at execution** | Walking is immediate, so the player queues the *when* and steers the *where* up to the last moment. |
| Air dash | **Allowed** (gravity suspended during dash) | Unlocks the marquee Jump→Dash gap-crossing combo. |
| Command validity | **Checked at execution, inside the command itself** | One consistent rule; inputs are always accepted if the queue has room. The queue stays fully generic — it never knows why a command succeeds or fails. |
| Queue full (3 entries) | **Reject new input** (event fired, never overwrite) | Per brief. |
| Architecture | **Plain C# command classes** (no ScriptableObjects) | Right weight for a jam; a new ability = one class + one input binding. |

## Architecture

Data flow is one-directional; the UI is a pure observer:

```
PlayerInputHandler ──Move──────────────────▶ PlayerMotor   (immediate)
        │
        └─Jump/Dash press──▶ CommandQueue ──▶ IPlayerCommand.Execute(motor)
                                  │
                                  └──events + polling──▶ CommandQueueUI
```

### Components

All paths relative to `Assets/Assets/Scripts/`.

| File | Responsibility |
|---|---|
| `Player/PlayerMotor.cs` | All physics: Rigidbody2D velocity movement, grounded check (OverlapBox vs ground layer), `Jump()`, `Dash(int direction)`, `IsGrounded`, `FacingDirection`. Knows nothing about queues or input. |
| `Player/PlayerInputHandler.cs` | Reads Input System actions. Move → motor every frame; Jump/Dash `performed` → `queue.Enqueue(command)`. |
| `Commands/CommandType.cs` | `enum CommandType { Jump, Dash }`. Future systems identify commands by `Type` — never by label strings. |
| `Commands/IPlayerCommand.cs` | `CommandType Type { get; }` · `string DisplayLabel { get; }` (UI only) · `void Execute(PlayerMotor)` |
| `Commands/JumpCommand.cs` | Type `Jump`, label `↑`. `Execute` → `motor.Jump()` if grounded, otherwise does nothing (validity is internal to the command). |
| `Commands/DashCommand.cs` | Type `Dash`, label `→`. `Execute` → `motor.Dash(motor.FacingDirection)`. |
| `Commands/CommandQueue.cs` | Owns the countdown. `QueuedCommand` struct (command + `ExecuteAt`, `Remaining => ExecuteAt - Time.time`). Serialized `delaySeconds` (per-level knob, 1–5) and `maxQueueSize = 3`. |
| `UI/CommandQueueUI.cs` | World-space draining-bar rows above player's head (icon + bar per queued entry; digits on the next-to-execute row only — see UI section). |
| `Camera/CameraFollow.cs` | Smooth-damp camera follow of the player (added by request 2026-07-23; purely visual). |

### CommandQueue contract

- `bool Enqueue(IPlayerCommand cmd)` — if `Count >= 3`: fire `CommandRejected`,
  return false. Else store `{cmd, Time.time + delaySeconds}`, fire `CommandQueued`.
- `Update()` — while the front entry's `ExecuteAt <= Time.time`: remove it, call
  `Execute(motor)`, fire `CommandExecuted`. The queue's only responsibilities are:
  wait for countdown → execute → remove → fire events. It never knows or cares
  whether a command's internal validity check passed.
- Exposes `IReadOnlyList<QueuedCommand> Entries` for the UI's per-frame countdown polling.
- Events (`CommandQueued`, `CommandExecuted`, `CommandRejected`) carry the entry;
  UI uses them for structural changes only.
- Uses `Time.time` (scaled) so pausing pauses countdowns. Frame-granularity firing
  (~16 ms late worst case) is acceptable.
- Because the delay is constant per level, execution spacing always mirrors input
  spacing — FIFO order is guaranteed and simultaneous fires are impossible unless
  pressed the same frame.

### PlayerMotor feel rules

- Horizontal movement: direct velocity control, always responsive — including while
  actions are queued and during jumps.
- Jump: set vertical velocity to `jumpVelocity`.
- Dash: for `dashDuration` (~0.15 s) — gravity scale 0, vertical velocity 0,
  horizontal velocity `dashSpeed * direction`; move input ignored until it ends.
- **Conflict rule: any command executing cancels an active dash first** (queued jump
  firing mid-dash ends the dash, then jumps). Repeated dash restarts the dash.
- Serialized tuning: `moveSpeed`, `jumpVelocity`, `dashSpeed`, `dashDuration`,
  ground-check size/layer. Rigidbody2D: freeze rotation, interpolation on.

## Input bindings

Extend the existing `InputSystem_Actions` asset (Player map):

- **Move** — existing (A/D, arrows, left stick); only the x-axis is used.
- **Jump** — existing (Space, gamepad south).
- **Dash** — new action: Left Shift, gamepad west.

## UI

Redesigned 2026-07-24 after playtest feedback ("three ticking timers are hard to
read"): countdowns are now **draining bars**, read preattentively instead of as
digits.

- World-space rows parented to the Player above the head (runtime-built TMP text
  + SpriteRenderer bars; no Canvas, no prefabs).
- Each queued action = its icon glyph (`↑`/`→`) + a horizontal bar that empties
  as the countdown runs; the action fires when the bar is empty. Bar color runs
  green → yellow → red with time remaining.
- **Next-to-execute at the top**; on execution the remaining rows shift upward.
  Order is conveyed purely by position — no numbering glyphs (the circled digits
  weren't in the default font atlas anyway).
- Only the next-to-execute row shows a digit readout (`2.83`, two decimals,
  InvariantCulture so the separator is always a dot); later rows are bar-only
  since with a constant delay their exact timers carry no extra information.
- The next-to-execute bar flashes toward white during its final 0.5 s.
- Known fragility (accepted 2026-07-24): the `↑`/`→` icon glyphs are not in the
  static LiberationSans SDF atlas — they render via the dynamic fallback font
  asset. Swapping the default font or clearing the fallback would degrade them
  to `□`. If fonts ever change, bake U+2191/U+2192 into the atlas.
- Optional (only if trivial during implementation): brief red flash on reject for
  readability. Not required today. (The queue no longer surfaces discards — if
  discard feedback is ever wanted, it comes from the motor/command layer.)

## Test scene

`Assets/Scenes/TestScene.unity`, graybox:

- Flat ground plus three platforms at increasing heights (white square sprites,
  BoxCollider2D, dedicated Ground layer).
- One gap sized so plain jump fails but **Jump→Dash clears it** — proves the combo
  and the planning loop.
- Player: white square, Rigidbody2D + BoxCollider2D, all scripts + queue UI attached.
- Camera smooth-follows the player (`CameraFollow`, offset (0, 2, -10), ~0.15 s
  smooth time), wired by the scene builder.
- `delaySeconds = 2` as the testing default.

## Error handling / edge cases

| Case | Behavior |
|---|---|
| Queue full | Input ignored, `CommandRejected` fired. |
| Jump fires while airborne | `JumpCommand.Execute` does nothing — the action is wasted. The queue treats it like any other execution (`CommandExecuted` fires). |
| Dash fires during active dash | Dash restarts (timer resets). |
| Jump fires during active dash | Dash cancels, jump executes. |
| Move input during dash | Ignored until dash ends. |
| Jump pressed while airborne | Accepted into queue (validity is checked at fire-time only). |

## Verification (jam-pragmatic: manual checklist, no test scaffolding)

1. Walking feels immediate at all times, including with a full queue and mid-jump.
2. Queued actions fire in press order, `delaySeconds` after their press.
3. Fourth input while queue is full is ignored; queue never exceeds 3.
4. Airborne jump at fire-time is discarded silently (no impulse).
5. Jump→Dash queued together clears the test gap.
6. Dash direction follows facing at the moment of execution (turn after pressing).
7. UI bars drain in real time (green → yellow → red, white flash in the last
   0.5 s on the top row); top row shows digits; rows shift up on execution and
   the display empties when idle.
8. Changing `delaySeconds` in the inspector changes all subsequent countdowns.

## Out of scope today

Levels, level-select/delay progression, art, sound, additional abilities,
queue-full/discard feedback polish, menus.

## Future expansion notes

- New ability = new `CommandType` enum entry + new `IPlayerCommand` class +
  input binding + enqueue call.
- Per-level delay is just the `delaySeconds` field set per scene.
- If abilities grow past ~5 or need designer tuning/icons, graduate commands to
  ScriptableObjects — the interface boundary makes that a mechanical refactor.
