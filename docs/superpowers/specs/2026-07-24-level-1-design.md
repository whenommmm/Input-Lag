# Input Lag — Level 1 "Think Ahead" Design

**Date:** 2026-07-24
**Status:** Awaiting user approval
**Depends on:** core mechanic spec (2026-07-23), death/respawn spec (2026-07-24)
**Purpose:** The FIRST level. Not a challenge — a teacher. The player should
finish thinking: "I don't react anymore. I plan ahead."

## Rules (from requirements, locked)

- Delay: **2 seconds** (`delaySeconds = 2`).
- Only geometry teaches: ground, static platforms, moving platforms, gaps,
  walls, player. NO enemies/spikes/lasers/crushers/doors/switches/
  disappearing platforms/collectibles/timers/moving hazards/death traps.
- ~8 sequential rooms, ONE new idea each, difficulty rises gradually,
  failure must explain itself, never precision platforming, never softlock,
  safe ground after failure, generous space, one obvious solution per puzzle.

## Movement model (all numbers derive from this — retune motor ⇒ re-derive)

| Constant | Value |
|---|---|
| Walk speed | 8 u/s (immediate) |
| Jump height | 2.5 u |
| Jump reach at full walk | ≤ 5.7 u |
| Dash | +3.0 u horizontal, gravity suspended 0.15 s |
| Jump→Dash reach | ≤ 8.7 u (dash near apex) |
| Commitment distance | 16 u (2 s × 8 u/s) |

**Derived design rules:**
- Step rise standard **2.0** (0.5 headroom under jump height — no precision).
- "Easy" gaps ≤ 3.5 (≤ 60 % of plain reach). Combo gaps = **7.0** (plain jump
  fails by ~1.3 — legible failure; combo clears by ~1.7 — roomy success).
- **Every moving platform travels one leg of its path in exactly 2.0 s** — the
  delay itself becomes the prediction rule: "press when the platform is at the
  far end; it arrives as your action fires."
- **No deaths in Level 1.** Every gap drops into a recovery pit (floor 3 u
  below) with 1.5-u escape steps on its left wall — failure costs seconds and
  teaches; it never punishes. Kill-zone at y = −12 as unreachable safety net.
- Rooms with short runways deliberately afford the stand→queue→go strategy
  (the calm beginner plan); full-speed pre-queueing (16 u early) is the expert
  path for later levels.

## World layout

One continuous left→right strip. "Floor at y = N" = walkable top surface.
Player 1×1, spawns at (1, 1) on floor y = 0. Tall boundary walls at x = −1 and
x = 162.5. Camera: ortho 5.5, bounds x[−1..163], y[−4..20]. Checkpoints
(invisible 1×3 triggers) at every room entry: x = 1, 16, 26, 40, 54, 68, 85,
107, and mid-exam x = 143.

### Room 1 — The Late Jump (x 0→22) · teaches: the delay exists
- Flat floor x 0→14 at y=0 (13 u calm runway). Solid block x 14→22, top y=2.0.
- Solution: walk right, blocked by chest-high wall, press Jump → nothing →
  2 s later it fires while holding right → rise 2.5, slide onto ledge.
- Failure impossible (worst case: jump in place, press again).
- Prepares: a press is a promise → Room 2 spends it on a location.

### Room 2 — The Planned Gap (x 14→34) · teaches: planning the delayed jump
- Floor y=2, x 14→22. **Gap x 22→25 (3.0)**. Floor y=2, x 25→34.
- Recovery pit: floor y=−1, x 20→26; escape steps tops y=0.5 and y=2.0.
- Solutions (both intended): stand at edge, queue, hold right on fire (drift
  reach 5.7 ≫ 3); or press a few steps early and keep walking (±1 s tolerant).
- Prepares: chosen-in-the-past jump locations → Room 3 chains two.

### Room 3 — Stairs of Intent (x 25→45) · teaches: rhythm, optional multi-queue
- From floor y=2: step x 34→39 top y=4.0; step x 39→45 top y=6.0 (2.0 rises,
  5–6 u deep). Nothing to fall into.
- Solution: queue jump, walk into riser, rise; repeat. Players may discover
  queueing the 2nd jump while the 1st counts down (optional).
- Prepares: jumps firing on schedule → Room 4 adds an object with a schedule.

### Room 4 — The Ferry (x 39→60) · teaches: predicting a moving platform
- Floor y=6 ends x=45. **Gap x 45→53 (8.0)**. Floor y=6 resumes x 53→60.
- **Moving platform**: 3.5 × 0.5, top flush y=6.0, center ping-pong
  x=47.5 ↔ 50.5, speed 1.5 u/s → **2.0 s per leg**. Near extreme edge 0.75 u
  from near lip; far extreme edge 0.75 u from far lip.
- Recovery pit y=3, x 44→54; escape steps tops 4.5 / 6.0.
- Solution: watch one cycle (4 s); press Jump when platform is at FAR side →
  it arrives as the jump fires; hop on; ride; queue jump as it heads back out;
  hop off. Flush height + 3.5-u target + slow speed = prediction is the only
  graded skill.
- Prepares: predicting the world's future → Room 7 combines with the combo.

### Room 5 — The Long Gap (x 53→75) · teaches: Jump→Dash
- Floor y=6, x 53→60. **Gap x 60→67 (7.0)**. Floor y=6, x 67→79 (shared with
  Room 6's run-up).
- Recovery pit y=3, x 59→68; escape steps.
- Solution: plain jump visibly falls ~1.3 short into the pit (self-diagnosing
  failure). Then: press Jump then Dash (~0.2 s apart, tolerance ~0.5 s) while
  walking — jump fires at edge, dash extends mid-air with gravity cut.
- Prepares: combo as one mental unit → Room 6 aims it upward.

### Room 6 — The Tower (x 67→91) · teaches: dash preserves height
- Floor y=6, x 67→79 (12 u run-up). **Gap x 79→84 (5.0)** against tower; high
  ledge x 84→91, **top y=8.2 (rise 2.2)**.
- Math: plain running jump is at height ≈1.1 when it reaches x=84 (needs 2.2)
  → always bonks, drops to pit. Jump→Dash: apex at +2.8/+2.5, dash carries the
  remaining distance AT height 2.5 → lands ~0.8 deep above the 2.2 rise.
- Recovery pit y=3, x 78→85; escape steps tops 4.5 / 6.0.
- Prepares: full spatial command of the combo → Room 7 moving target.

### Room 7 — Ferry + Combo (x 84→112) · teaches: predict + combo in one plan
- Floor y=8.2, x 84→91. **Gap x 91→106 (15.0)**. Floor y=8.2, x 106→116
  (shared with Room 8's entry runway).
- Ferry: 3.5 wide, top y=8.2, center x=95 ↔ 98, speed 1.5 → **2.0 s legs**.
  Near extreme edge 2.25 u from near lip (easy predicted hop on). Far extreme
  edge at x=99.75 → **6.25 u to far lip**: beyond plain jump (5.7), well
  within combo (8.7) — the off-jump REQUIRES Jump→Dash.
- Recovery pit y=5.2, x 90→107; steps tops 6.7 / 8.2. Checkpoint x=85.
- Solution: predicted hop on (Room 4 rule); ride out; as ferry passes middle
  heading far, press Jump then Dash → both fire at the far extreme → cross.
- Prepares: the exam's hardest question, rehearsed in isolation.

### Room 8 — Final Exam (x 106→162) · teaches: nothing new, recaps everything
- **a) Elevation** (Room 1/3): floor y=8.2 x 106→116; step x 116→124 top
  y=10.2 (rise 2.0).
- **b) Gap** (Room 2): x 124→127.5 (**3.5**); floor x 127.5→134 y=10.2.
  Pit y=7.2 + steps.
- **c) Ferry** (Room 4): gap x 134→142 (8.0); platform 3.5 wide top y=10.2,
  center 136.75 ↔ 139.25, speed 1.25 → **2.0 s legs**, reaching within 1 u of
  each lip. Pit y=7.2.
- **d) Combo gap** (Room 5): floor x 142→148 (checkpoint x=143); gap
  x 148→155 (**7.0**); pit y=7.2.
- **e) Goal**: floor x 155→162; green goal block at x=158 → LEVEL COMPLETE.
  `nextSceneName` = "" (restart) until Level 2 exists.

## New systems required (the only engineering beyond geometry)

1. **`MovingPlatform`** (`Scripts/Level/`): kinematic Rigidbody2D ping-ponging
   between two serialized points at a serialized speed (MovePosition in
   FixedUpdate). Distinct color (light blue) for readability.
2. **Platform carry in `PlayerMotor`**: the motor writes `linearVelocity`
   every physics step, which would let a platform slide out from under the
   player. Standard fix: while grounded, add the ground rigidbody's velocity
   to the walk velocity (track the collider found by the ground check).
3. **`Level1SceneBuilder`** (editor): builds `Assets/Scenes/Level1.unity` from
   the numbers in this spec (commented), reusing the test-scene builder's
   patterns (Ground layer, TMP essentials, wiring, build-settings entry).
   Static geometry gray, moving platforms light blue, goal green.

Deferred hardening from the last review lands with this cycle's LevelManager
touch if convenient (activeCheckpoint null guard, CanStreamedLevelBeLoaded).

## Verification (manual, per room)

1. R1: jump fires late but lands you on the ledge with zero skill required.
2. R2: both strategies (stand-queue / walk-early) clear the 3-u gap; pit
   escape works with two delayed jumps.
3. R3: two-step rhythm works; queueing both jumps early also works.
4. R4: "press when far" rule works reliably; platform carries the standing
   player with no sliding; both hops forgiving.
5. R5: plain jump fails visibly ~1.3 short; Jump→Dash with sloppy (±0.5 s)
   spacing clears.
6. R6: plain jump always bonks below the ledge; combo lands comfortably.
7. R7: hop-on prediction + ride + combo-off all work; failing any beat drops
   to pit with easy escape.
8. R8: all four beats flow back-to-back; goal fires LEVEL COMPLETE.
9. Camera never shows void; checkpoints respawn correctly per room; no
   possible death anywhere (kill zone unreachable).

## Out of scope

Level 2+, title screen, sound, art beyond colored graybox, moving platform
easing (linear is fine), any new hazard types.
