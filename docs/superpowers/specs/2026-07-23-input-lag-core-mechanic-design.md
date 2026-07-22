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
| Command validity | **Checked at execution, never at enqueue** | One consistent rule; inputs are always accepted if the queue has room. |
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
| `Commands/IPlayerCommand.cs` | `string DisplayLabel { get; }` · `bool CanExecute(PlayerMotor)` · `void Execute(PlayerMotor)` |
| `Commands/JumpCommand.cs` | Label `↑`. `CanExecute` → `motor.IsGrounded`. `Execute` → `motor.Jump()`. |
| `Commands/DashCommand.cs` | Label `→`. `CanExecute` → `true`. `Execute` → `motor.Dash(motor.FacingDirection)`. |
| `Commands/CommandQueue.cs` | Owns the countdown. `QueuedCommand` struct (command + `ExecuteAt`, `Remaining => ExecuteAt - Time.time`). Serialized `delaySeconds` (per-level knob, 1–5) and `maxQueueSize = 3`. |
| `UI/CommandQueueUI.cs` | World-space canvas above player's head; one TMP text row per queued entry (`↑ 2.8`). |

### CommandQueue contract

- `bool Enqueue(IPlayerCommand cmd)` — if `Count >= 3`: fire `CommandRejected`,
  return false. Else store `{cmd, Time.time + delaySeconds}`, fire `CommandQueued`.
- `Update()` — while the front entry's `ExecuteAt <= Time.time`: remove it;
  if `CanExecute(motor)` → `Execute(motor)` + `CommandExecuted`, else `CommandDiscarded`.
- Exposes `IReadOnlyList<QueuedCommand> Entries` for the UI's per-frame countdown polling.
- Events (`CommandQueued`, `CommandExecuted`, `CommandDiscarded`, `CommandRejected`)
  carry the entry; UI uses them for structural changes only.
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

- World-space Canvas parented to the Player, positioned above the head.
- Vertical stack of up to 3 rows; each row is a TMP text: label + remaining seconds
  to one decimal (`↑ 2.8`). Timers update every frame.
- **Next-to-execute at the top**; on execution the remaining rows shift upward.
- No art: text glyphs `↑` / `→` stand in for icons. If the default TMP font atlas
  lacks those arrow glyphs, fall back to `JMP` / `DSH` — labels are data on the
  command, so this is a two-string change.
- Optional (only if trivial during implementation): brief red flash on
  discard/reject for readability. Not required today.

## Test scene

`Assets/Scenes/TestScene.unity`, graybox:

- Flat ground plus three platforms at increasing heights (white square sprites,
  BoxCollider2D, dedicated Ground layer).
- One gap sized so plain jump fails but **Jump→Dash clears it** — proves the combo
  and the planning loop.
- Player: white square, Rigidbody2D + BoxCollider2D, all scripts + queue UI attached.
- Static camera framing the whole play space (no follow script today).
- `delaySeconds = 2` as the testing default.

## Error handling / edge cases

| Case | Behavior |
|---|---|
| Queue full | Input ignored, `CommandRejected` fired. |
| Jump fires while airborne | Discarded, `CommandDiscarded` fired. |
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
7. UI countdowns tick in real time, rows shift up on execution, empty when idle.
8. Changing `delaySeconds` in the inspector changes all subsequent countdowns.

## Out of scope today

Levels, level-select/delay progression, art, sound, additional abilities, camera
follow, queue-full/discard feedback polish, menus.

## Future expansion notes

- New ability = new `IPlayerCommand` class + input binding + enqueue call.
- Per-level delay is just the `delaySeconds` field set per scene.
- If abilities grow past ~5 or need designer tuning/icons, graduate commands to
  ScriptableObjects — the interface boundary makes that a mechanical refactor.
