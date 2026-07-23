# Input Lag — Death, Respawn & Goal Design

**Date:** 2026-07-24
**Status:** Approved
**Depends on:** `2026-07-23-input-lag-core-mechanic-design.md` (core mechanic, shipped)
**Scope:** Complete the play loop in the existing test scene: die → YOU DIED →
respawn at checkpoint → reach goal → LEVEL COMPLETE. Level 1 is the NEXT cycle,
not this one — the test scene is the sandbox until the loop feels right.

## Locked design decisions

| Decision | Choice | Rationale |
|---|---|---|
| Death trigger | Kill-zone trigger below the map (falling is the only death today) | Matches current graybox hazards. |
| Death presentation | Souls-style banner: full-width red strip across screen center, "YOU DIED" text, fade in → hold → fade out (~0.3 / 0.8 / 0.3 s, all serialized) | User choice; total ~1.4 s keeps the retry loop jam-fast. |
| Queue on death | **Cleared** | A death wipes your plan — thematic, and prevents queued actions firing mid-death-screen. |
| Control during death/win | Input handler component disabled (its existing OnDisable already unsubscribes + disables the action map), move input zeroed | No new input plumbing needed. |
| Respawn point | **Last checkpoint touched** (empty trigger objects; editor gizmo only, invisible in game) | User choice. Initial checkpoint sits at player spawn. Re-touching the active checkpoint is a no-op. |
| Respawn state reset | Position + velocity zeroed, dash cancelled (gravity restored), queue cleared, camera snapped to target | Full reset, no scene reload — play-mode tuning survives deaths. |
| Goal | Visible green block past the jump→dash gap; same banner in green: "LEVEL COMPLETE", then scene restarts (a `nextSceneName` field switches this to next-level loading later) | Completes the loop now, one-line upgrade when Level 2 exists. |
| Rejected alternative | Scene-reload respawn | Kills death-screen timing, loses play-mode tweaks each death, checkpoints would need cross-reload persistence. |

## Death sequence (frame by frame)

1. Player enters KillZone → `LevelManager.PlayerDied()`.
2. Instantly: state → Dying, input handler disabled, `motor.SetMoveInput(0)`,
   `queue.Clear()`.
3. Banner fades in (red strip + "YOU DIED"), holds.
4. At fade-out start: `motor.Teleport(checkpoint.position)`,
   `cameraFollow.SnapToTarget()`.
5. Banner fades out → input re-enabled, state → Playing.

Win sequence is identical in shape: state → Winning, control cut (queue also
cleared), green "LEVEL COMPLETE" banner, then scene restart (or `nextSceneName`).

## Components

New folder `Assets/Assets/Scripts/Level/`:

| File | Responsibility |
|---|---|
| `Level/LevelManager.cs` | Orchestrates the loop. State machine `Playing / Dying / Winning` — trigger notifications outside Playing are ignored (dying while dying, goal during death, etc. are all no-ops by construction). Holds the active checkpoint (serialized initial one), runs the death/win coroutines, owns refs to motor, queue, input handler, camera follow, banner. |
| `Level/Checkpoint.cs` | Trigger; on player enter → `levelManager.CheckpointReached(this)`. Draws an editor-only gizmo (wire cube). |
| `Level/KillZone.cs` | Trigger; on player enter → `levelManager.PlayerDied()`. |
| `Level/LevelGoal.cs` | Trigger; on player enter → `levelManager.GoalReached()`. |
| `UI/BannerUI.cs` | Runtime-builds a screen-space overlay canvas (like CommandQueueUI builds its rows): full-width tinted strip centered vertically + TMP text. `Play(text, color, onFadeOutStart, onComplete)` coroutine with serialized fade-in/hold/fade-out. Fade via CanvasGroup alpha. |

Trigger detection: all three triggers identify the player by
`other.GetComponent<PlayerMotor>() != null` — no tags or layers needed.

### API additions to existing classes

- `PlayerMotor.Teleport(Vector2 position)` — cancels dash (restores gravity),
  zeroes velocity, sets rigidbody + transform position.
- `CommandQueue.Clear()` — empties the list. No events fired; the polling UI
  empties on the next frame automatically.
- `CameraFollow.SnapToTarget()` — jumps straight to target + offset, zeroes
  SmoothDamp velocity.

## Scene builder changes (TestSceneBuilder)

- **KillZone**: invisible trigger box, center (0, −7), size (60 × 2) — well
  below the camera's view so falls read as falls.
- **Checkpoint A (initial)**: at player spawn (−11, 1), trigger size (1 × 3).
- **Checkpoint B**: on the left ground just before the gap, (0.5, 1.5),
  trigger size (1 × 3).
- **Goal**: green block (1 × 1.5) at (13, 0.75) on the right ground, visible
  sprite + trigger collider.
- **LevelManager** object with BannerUI component; all references wired via
  SerializedObject as usual; initial checkpoint = Checkpoint A.

## Edge cases

| Case | Behavior |
|---|---|
| Kill-zone touched while already Dying | Ignored (state machine). |
| Goal touched during death (or vice versa) | Ignored (state machine). |
| Dying mid-dash | `Teleport` cancels the dash and restores gravity. |
| Queued action due during the death screen | Impossible — queue cleared at death start; queue keeps ticking but is empty. |
| Checkpoint re-touched | No-op if already active. |
| Rigidbody interpolation smearing the teleport | Set both `body.position` and `transform.position` in `Teleport`. |
| Input pressed during banner | Dead — handler component disabled; movement input zeroed at death start. |

## Verification (manual, in play mode)

1. Fall into the gap → control cuts instantly, red "YOU DIED" strip fades in
   center-screen, holds, fades out; player is back at spawn with camera snapped,
   control restored. Total ≈ 1.4 s.
2. Queue Jump→Dash then walk off the edge → banner plays, **no queued action
   fires**, queue UI is empty after respawn.
3. Walk through Checkpoint B (before the gap), then die in the gap → respawn at
   Checkpoint B, not at spawn.
4. Die while mid-dash → respawn is clean (no dash momentum, gravity normal).
5. Reach the green block → green "LEVEL COMPLETE" banner, scene restarts,
   everything works again after restart.
6. Mash Jump/Dash/movement during the death banner → nothing happens, nothing
   queued when control returns.
7. Checkpoints are invisible in Game view, visible as gizmos in Scene view.

## Out of scope

Level 1 (next cycle), death/win sounds, particles or player death animation,
checkpoint activation feedback, lives/death counter, skippable banner.
