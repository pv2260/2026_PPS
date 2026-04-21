# HitMiss - Developer Guide for Beginners

A step-by-step walkthrough of how the Hit Or Miss clinical assessment app works, written for amateur programmers who want to understand the codebase.

---

## Table of Contents

1. [What Is This App?](#1-what-is-this-app)
2. [Tech Stack](#2-tech-stack)
3. [Project Structure](#3-project-structure)
4. [Step 1 - The App Flow (State Machine)](#step-1---the-app-flow-state-machine)
5. [Step 2 - Configuration (No Code Needed)](#step-2---configuration-no-code-needed)
6. [Step 3 - Generating Trials (The Questions)](#step-3---generating-trials-the-questions)
7. [Step 4 - The Flying Ball (Visuals)](#step-4---the-flying-ball-visuals)
8. [Step 5 - Capturing Input (Patient Response)](#step-5---capturing-input-patient-response)
9. [Step 6 - Running Trials and Scoring](#step-6---running-trials-and-scoring)
10. [Step 7 - Logging Everything](#step-7---logging-everything)
11. [Step 8 - The Clinician Dashboard](#step-8---the-clinician-dashboard)
12. [Step 9 - Localization (Multi-Language)](#step-9---localization-multi-language)
13. [Programming Patterns Used](#programming-patterns-used)
14. [Data Flow Summary](#data-flow-summary)
15. [Glossary](#glossary)

---

## 1. What Is This App?

HitMiss is a **clinical assessment tool** built in Unity for XR (Virtual/Mixed Reality) headsets. It is designed for medical research, likely studying neurological conditions like Parkinson's disease.

**How it works:** A ball flies toward the patient along a curved path. The patient must judge whether the ball will **hit their body** or **miss** it, and respond by pressing a button, pinching their fingers, or pressing a key. Their accuracy and reaction times are recorded for analysis.

**Key design choice:** The app intentionally gives **no feedback** about whether the patient was right or wrong. This is standard in clinical research to avoid influencing future responses.

---

## 2. Tech Stack

| Technology | Purpose |
|---|---|
| **Unity 6000.3.2f1** | Game engine (renders 3D graphics, manages the app lifecycle) |
| **C#** | Programming language for all scripts |
| **Unity XR Hands** | Hand tracking on VR/MR headsets |
| **Unity Input System** | Handles controller button presses |
| **TextMesh Pro (TMPro)** | Renders UI text on screen |
| **ScriptableObjects** | Stores configuration data outside of code |

---

## 3. Project Structure

```
Assets/
  Scripts/HitOrMiss/           <-- All the C# code lives here
    Core/                      <-- Main app controller, enums, data structures
      HitOrMissAppController.cs   (the "boss" that runs everything)
      Enums.cs                    (shared categories and states)
      TrialJudgement.cs           (data structure for each scored trial)
    Task/                      <-- Trial generation and scheduling
      TrialGenerator.cs           (creates the 80 trials per block)
      TrajectoryTaskManager.cs    (runs trials in real-time, scores responses)
      TrajectoryTaskAsset.cs      (configuration container)
      TrialDefinition.cs          (data structure for one trial's parameters)
    Input/                     <-- How the patient responds
      IResponseInputSource.cs     (interface - the "contract" all inputs follow)
      ControllerButtonInput.cs    (XR controller triggers)
      HandPinchInput.cs           (finger pinch detection)
      KeyboardCommandInput.cs     (H/M keys for testing)
    Visuals/                   <-- What the patient sees
      TrajectoryObjectController.cs  (the flying ball + shadow)
    Logging/                   <-- Data recording
      TaskLogger.cs               (CSV + JSON file output)
      EegMarkerEmitter.cs         (brain scan synchronization markers)
    UI/                        <-- Clinician-facing interface
      ClinicianControlPanel.cs    (start/stop, live stats)
      ResponseIndicator.cs        (brief "HIT"/"MISS" flash)
    Localization/              <-- English/French support
      LocalizedTermTable.cs       (key-value text lookup)
      LocalizedUITextBinder.cs    (connects UI text to translations)
  Prefab/
    TrajectoryTask.asset       <-- Configuration file (edit in Unity Inspector)
    LoomingObjectPrefab.prefab <-- The ball that flies at the patient
  Scenes/
    HitMissScene.unity         <-- The main scene you open in Unity
```

---

## Step 1 - The App Flow (State Machine)

**File:** `Assets/Scripts/HitOrMiss/Core/HitOrMissAppController.cs`

The entire session follows a fixed sequence of **phases**:

```
IDLE  -->  INTRO (20s)  -->  BLOCK 1 (80 trials)  -->  REST (30s)
                              BLOCK 2 (80 trials)  -->  REST (30s)
                              BLOCK 3 (80 trials)  -->  OUTRO (10s)  -->  IDLE
```

**How it works in code:**

The app uses a **coroutine** (`RunSession`) - think of it as a recipe that Unity follows step by step, pausing where you tell it to wait:

```csharp
IEnumerator RunSession()
{
    // Phase 1: Show intro screen, wait 20 seconds
    m_CurrentPhase = TaskPhase.Intro;
    yield return new WaitForSeconds(20f);

    // Phase 2: Run 3 blocks
    for (int b = 0; b < 3; b++)
    {
        m_CurrentPhase = TaskPhase.Block;
        m_TaskManager.StartBlock(b);       // Launch 80 trials

        while (m_TaskManager.IsRunning)    // Wait for all trials to finish
            yield return null;

        if (b < 2)                         // Rest between blocks (not after last)
        {
            m_CurrentPhase = TaskPhase.Rest;
            yield return new WaitForSeconds(30f);
        }
    }

    // Phase 3: Show outro, wait 10 seconds
    m_CurrentPhase = TaskPhase.Outro;
    yield return new WaitForSeconds(10f);

    EndSession();  // Save data, clean up
}
```

**Key concept for beginners:** `yield return` is Unity's way of saying "pause here and come back next frame" or "pause for X seconds." The method remembers where it left off.

**What happens at startup (`Awake`):**
- Links the task configuration to the task manager
- Connects the EEG marker system
- Hides all UI panels (intro, rest, outro)

**What happens when the clinician presses Start (`StartSession`):**
- Selects the correct input method (controller, hand tracking, or keyboard)
- Opens the CSV log file
- Starts the EEG marker session
- Wires up the response indicator
- Hides setup controls and begins the coroutine

---

## Step 2 - Configuration (No Code Needed)

**File:** `Assets/Prefab/TrajectoryTask.asset`

All tunable parameters live in a **ScriptableObject** - a Unity feature that stores data in a file you can edit in the Inspector without touching code:

| Parameter | Default | What it controls |
|---|---|---|
| `TaskName` | "Hit Or Miss Task" | Label for log files |
| `BlockCount` | 3 | Number of blocks per session |
| `TrialsPerCategory` | 20 | Trials per category per block (total = 4 x 20 = 80) |
| `IntroDuration` | 20 seconds | How long the intro screen shows |
| `RestDuration` | 30 seconds | Break time between blocks |
| `OutroDuration` | 10 seconds | How long the outro screen shows |
| `SpawnDistance` | 7 meters | How far away the ball starts |
| `VanishDistance` | 1 meter | How close the ball gets before disappearing |
| `Speed` | 2.5 m/s | How fast the ball travels |
| `BallDiameter` | 0.175 m (17.5 cm) | Size of the ball |

**Why this matters:** Researchers can adjust the difficulty (faster balls, closer spawns, more trials) without a programmer.

---

## Step 3 - Generating Trials (The Questions)

**File:** `Assets/Scripts/HitOrMiss/Task/TrialGenerator.cs`

Each block needs 80 trials. The generator creates them in **4 categories** of 20 trials each:

| Category | Offset from body | Correct answer | What the patient sees |
|---|---|---|---|
| **Hit** | 0 cm (on body) | "Hit" | Ball aimed directly at them |
| **NearHit** | 0-10 cm | "Hit" | Ball barely grazes past them |
| **NearMiss** | 10-25 cm | "Miss" | Ball passes somewhat close |
| **Miss** | 30-45 cm | "Miss" | Ball clearly flies past |

**Each trial gets unique variation:**

1. **Approach angle** (-30 to +30 degrees): The ball doesn't always come from straight ahead. It spreads evenly across angles with a small random jitter of +/-3 degrees.

2. **Curve direction** (left or right): Alternates so balls curve both ways.

3. **Curvature magnitude** (0.30-0.50 m): How much the path bends. Varies randomly within range.

4. **Lateral offset**: How far from the body the ball ends up. Random within the category's range, randomly assigned to left or right side.

**The shuffle step** is important: after generating all 80 trials, the code shuffles them so the **same category never appears twice in a row**. This prevents the patient from predicting patterns.

```csharp
// Simplified version of the shuffle logic:
// 1. Fisher-Yates shuffle (random ordering)
// 2. Fix any consecutive same-category pairs by swapping
```

**Timing:** Trials are spaced ~4 seconds apart with a small random jitter (+/-0.3s) so the patient can't predict the rhythm.

---

## Step 4 - The Flying Ball (Visuals)

**File:** `Assets/Scripts/HitOrMiss/Visuals/TrajectoryObjectController.cs`

Each ball follows a **quadratic Bezier curve** - a smooth curved line defined by 3 points:

```
    Start Point (P0)                    End Point (P2)
    7 meters away                       1 meter from patient
         \                             /
          \                           /
           \        Control          /
            \       Point (CP)      /
             \        *            /
              \      /            /
               \    /            /
                \  /            /
                 \/            /
                  \           /
                   ----------
                  (curved path)
```

**The math (simplified):**

```csharp
// Bezier formula: position at time t (0 to 1)
Vector3 pos = (1-t)*(1-t) * startPos      // Start influence (fades out)
            + 2*(1-t)*t   * controlPoint   // Curve influence (peaks at middle)
            + t*t         * endPos;        // End influence (fades in)
```

- At `t=0`: ball is at the start position (7m away)
- At `t=0.5`: ball is near the control point (maximum curve)
- At `t=1`: ball is at the end position (1m away, with lateral offset)

**The ground shadow:**
A dark disc follows underneath the ball on the ground. Its size changes based on the ball's height - closer to the ground means a larger, sharper shadow. This creates a natural depth perception cue.

```csharp
float scaleFactor = 1 / (height * 0.5 + 0.5);  // Closer to ground = bigger shadow
```

**Ball duration:** The time the ball is visible = `(spawnDistance - vanishDistance) / speed` = `(7 - 1) / 2.5` = **2.4 seconds**.

---

## Step 5 - Capturing Input (Patient Response)

**Files:** `Assets/Scripts/HitOrMiss/Input/` folder

The app supports **3 ways** for the patient to respond. All of them implement the same **interface** (`IResponseInputSource`), which is a contract that says: "I promise to fire a `ResponseReceived` event when the patient responds."

### Option A: XR Controller (`ControllerButtonInput.cs`)
- **Left trigger** = "Hit"
- **Right trigger** = "Miss"
- Uses Unity's Input Action system to detect button presses

### Option B: Hand Tracking (`HandPinchInput.cs`)
- **Left hand pinch** (thumb + index finger) = "Hit"
- **Right hand pinch** = "Miss"
- Uses XR Hand Subsystem to read finger joint positions

**Hysteresis** prevents accidental double-triggers:
```
Pinch triggers at:  < 4 cm apart  (thumb tip to index tip)
Pinch releases at:  > 6 cm apart
```
The gap between 4cm and 6cm means you must clearly open your fingers before you can pinch again.

### Option C: Keyboard (`KeyboardCommandInput.cs`)
- **H key** = "Hit"
- **M key** = "Miss"
- Used during development/testing on a desktop

**Why use an interface?** The scoring system (`TrajectoryTaskManager`) doesn't care HOW the patient responded. It just listens for the `ResponseReceived` event. This makes it trivial to add new input methods in the future (e.g., voice commands, foot pedals) without changing any scoring logic.

---

## Step 6 - Running Trials and Scoring

**File:** `Assets/Scripts/HitOrMiss/Task/TrajectoryTaskManager.cs`

This is the **runtime brain** of the app. It does 4 things every frame:

### 6a. Spawn trials on schedule
Every ~4 seconds, the next trial's ball is created and launched:
```csharp
if (elapsed >= nextSpawnTime)
{
    SpawnTrial(m_BlockTrials[m_NextTrialIndex]);
    m_NextTrialIndex++;
}
```

### 6b. Match responses to trials
When the patient responds, the system finds the **most recently spawned, unresolved trial** that is still within its response window:

```
Response window = ball flight time + 1.5 seconds grace period
                = 2.4s + 1.5s = 3.9 seconds from spawn
```

```csharp
// Simplified matching logic:
foreach (var trial in m_ActiveTrials)
{
    if (trial.Resolved) continue;           // Already answered
    if (trialElapsed > deadline) continue;  // Too late

    if (trial.SpawnTime > bestSpawnTime)    // Pick most recent
        bestTrial = trial;
}
```

### 6c. Score the response
```csharp
bool correct = response.command == bestTrial.Definition.expectedResponse;
```

Each trial gets one of these results:
| Result | Meaning |
|---|---|
| `Correct` | Patient answered correctly |
| `Incorrect` | Patient answered wrong |
| `NoResponse` | Patient didn't respond within the window |
| `TooEarly` | Response came before the trial started |
| `TooLate` | Response came after the window closed |

### 6d. Timeout unresolved trials
If the grace period expires with no response, the trial is marked `NoResponse`:
```csharp
if (!trial.Resolved && trialElapsed >= deadline)
{
    ResolveTrial(trial, SemanticCommand.None, TrialResult.NoResponse, "timeout");
}
```

### 6e. Block completion
When all trials are spawned AND all active trials are resolved, the block ends:
```csharp
if (m_NextTrialIndex >= m_BlockTrials.Length && m_ActiveTrials.Count == 0)
{
    m_Running = false;
    BlockEnded?.Invoke(m_CurrentBlock);
}
```

---

## Step 7 - Logging Everything

### 7a. CSV + JSON Logs (`TaskLogger.cs`)

**Location:** `Application.persistentDataPath/Logs/`
**Filename:** `{ParticipantId}_{SessionId}_{TaskName}.csv`

Each trial produces one row in the CSV:

```
TrialId, Block, Category, Expected, Received, Result, IsCorrect,
StimulusOnset, ResponseTime, ReactionTimeMs, LateralOffsetM,
ApproachAngleDeg, FailureReason
```

**Example row:**
```
B1_T05, 0, NearHit, Hit, Hit, Correct, True, 24.5012, 26.2103, 1709.1, 0.0734, 12.3, ""
```

At the end of the session, a JSON file is also written with the same data in a structured format, wrapped in a `SessionLog` object with participant ID, session ID, and timestamp.

### 7b. EEG Markers (`EegMarkerEmitter.cs`)

For synchronizing with brain-scanning equipment, the app emits timestamped event codes:

| Event Code | When it fires |
|---|---|
| `session_start` / `session_end` | Beginning/end of entire session |
| `phase_intro` / `phase_block` / `phase_rest` / `phase_outro` | Each phase transition |
| `block_start` / `block_end` | Each block boundary |
| `trial_spawn` | A ball appears |
| `response_hit` / `response_miss` | Patient presses hit or miss |
| `trial_resolved_correct` / `trial_resolved_incorrect` | Trial scored |
| `trial_no_response` / `trial_timeout` | Patient didn't respond |

These markers let researchers align brain activity data with exact moments in the task.

---

## Step 8 - The Clinician Dashboard

**File:** `Assets/Scripts/HitOrMiss/UI/ClinicianControlPanel.cs`

The researcher operating the test sees a control panel with two modes:

### Setup Mode (before the test starts):
- **Participant ID field** - type in the patient's code (e.g., "P001")
- **Language toggle** - switch between English and French
- **Start button** - begins the session

### Task Mode (during the test):
- **Stop button** - emergency stop (resolves all remaining trials as NoResponse)
- **Live monitoring:**
  - Current phase (Idle / Intro / Block / Rest / Outro)
  - Block progress (e.g., "Block 2 of 3, Trial 45 of 80")
  - Overall accuracy percentage
  - Per-category accuracy (Hit: 85%, NearHit: 70%, NearMiss: 75%, Miss: 90%)
  - Last trial details (ID, category, response, correctness, reaction time)

### Response Indicator (`ResponseIndicator.cs`)
A brief visual flash (0.5s display + 0.3s fade) shows "HIT" (blue) or "MISS" (orange) when the patient responds. It does **NOT** show whether they were correct - this is intentional for the research protocol.

---

## Step 9 - Localization (Multi-Language)

**Files:** `Assets/Scripts/HitOrMiss/Localization/` folder

The app supports English and French using a simple key-value system:

```csharp
// In a ScriptableObject (LocalizedTermTable):
// Key: "block_complete"
// English: "Block completed - take a small break"
// French: "Bloc termine - prenez une petite pause"

string text = m_TermTable.Get("block_complete", m_Language);
```

UI text elements have `LocalizedUITextBinder` components that automatically update when the language changes.

---

## Programming Patterns Used

These are the key software design concepts the project demonstrates:

### 1. State Machine
The app has clear **phases** (Idle, Intro, Block, Rest, Outro) and only one can be active at a time. This makes the flow predictable and easy to debug.

### 2. Event-Driven Architecture
Components communicate through **C# events** instead of calling each other directly:
```csharp
// The task manager FIRES an event:
TrialJudged?.Invoke(judgement);

// The logger LISTENS for it:
m_TaskManager.TrialJudged += m_TaskLogger.LogTrial;
```
**Why this matters:** The task manager doesn't know (or care) that a logger exists. You could add 10 more listeners without changing any task manager code.

### 3. Interface (Contract Pattern)
All input methods implement `IResponseInputSource`:
```csharp
public interface IResponseInputSource
{
    event Action<ResponseEvent> ResponseReceived;
    void Enable();
    void Disable();
}
```
This means the task manager works with ANY input source - controllers, hands, keyboard, or anything you add in the future.

### 4. ScriptableObjects (Data-Driven Design)
Configuration and localization data live in Unity assets, not in code. Researchers can tweak parameters in the Unity Inspector without needing a programmer.

### 5. Coroutines (Async Flow)
Unity coroutines handle the session timeline - waiting for intro, running blocks, resting between blocks - without blocking the game loop.

### 6. Bezier Curves (Parametric Paths)
Instead of straight lines, balls follow smooth curves. The quadratic Bezier formula gives natural-looking trajectories with just 3 control points.

---

## Data Flow Summary

```
┌──────────────────────────────────────────────────────────────────┐
│                    HitOrMissAppController                        │
│                 (orchestrates the session)                        │
│                                                                  │
│  StartSession() ──> RunSession() coroutine                       │
│     │                   │                                        │
│     ▼                   ▼                                        │
│  ┌─────────────┐   ┌──────────────────────┐                     │
│  │ InputSource  │   │ TrajectoryTaskManager │                    │
│  │ (controller, │──>│ (spawns balls,        │                    │
│  │  hands, or   │   │  scores responses)    │                    │
│  │  keyboard)   │   └──────────┬───────────┘                    │
│  └─────────────┘              │                                  │
│                               │ TrialJudged event                │
│                    ┌──────────┼──────────┐                       │
│                    ▼          ▼          ▼                        │
│              ┌──────────┐ ┌───────┐ ┌──────────────┐            │
│              │TaskLogger│ │  EEG  │ │  Clinician   │            │
│              │(CSV/JSON)│ │Markers│ │   Panel      │            │
│              └──────────┘ └───────┘ └──────────────┘            │
└──────────────────────────────────────────────────────────────────┘

Per trial:
  TrialGenerator ──> TrialDefinition ──> TrajectoryObjectController (ball)
                                    ──> TrajectoryTaskManager (scoring)
                                    ──> TrialJudgement (result)
                                    ──> TaskLogger (saved to disk)
```

---

## Glossary

| Term | Meaning |
|---|---|
| **Block** | A set of 80 trials. There are 3 blocks per session. |
| **Trial** | One ball flying at the patient. The patient must respond "Hit" or "Miss." |
| **Category** | How close the ball passes to the body: Hit, NearHit, NearMiss, or Miss. |
| **Lateral offset** | How far (in meters) the ball ends up from the patient's body center. |
| **Bezier curve** | A mathematical formula for drawing smooth curves using control points. |
| **Coroutine** | A Unity feature that lets code pause and resume across frames (like a bookmark in a recipe). |
| **ScriptableObject** | A Unity data container that lives as a file - editable in the Inspector without code changes. |
| **Interface** | A C# contract that says "any class implementing me must have these methods/events." |
| **Hysteresis** | Using different thresholds for "on" and "off" to prevent flickering (e.g., pinch at 4cm, release at 6cm). |
| **EEG** | Electroencephalography - measuring brain electrical activity. Markers sync task events with brain data. |
| **ITI** | Inter-Trial Interval - the pause between consecutive trials (~4 seconds). |
| **Grace period** | Extra time (1.5s) after the ball vanishes during which the patient can still respond. |
| **Fisher-Yates shuffle** | An algorithm for randomly reordering a list where every permutation is equally likely. |
| **XR** | Extended Reality - umbrella term for VR (Virtual Reality), AR (Augmented Reality), and MR (Mixed Reality). |

---

Unity 6000.3.2f1
