# HitMiss - Project Setup Checklist

Step-by-step guide to configure and run the Hit Or Miss project from scratch.

---

## 1. Prerequisites

- [ ] **Unity Hub** installed on your computer
  - Download it from the official Unity website
- [ ] **Unity 6000.3.2f1** installed through Unity Hub
  - When installing, make sure to include the required modules for your target platform
- [ ] **Visual Studio** or **Visual Studio Code** installed (C# code editor)
- [ ] **Git** installed (if you are going to clone the repository)

### Required Unity modules by platform:

- [ ] **Windows Build Support** (included by default)
- [ ] **Universal Windows Platform Build Support** (if you are building for HoloLens)
- [ ] **Android Build Support** (if you are building for Meta Quest)

---

## 2. Get the Project

- [ ] Clone or download the HitMiss project repository
- [ ] Verify that the folder contains these subfolders:
  - [ ] `Assets/`
  - [ ] `Packages/`
  - [ ] `ProjectSettings/`

---

## 3. Open the Project in Unity

- [ ] Open **Unity Hub**
- [ ] Click **"Open" / "Add project from disk"**
- [ ] Navigate to the `HitMiss` folder and select it
- [ ] Wait for Unity to import all assets (this may take several minutes the first time)
- [ ] Verify that there are no red errors in the **Console** window (`Window > General > Console`)

---

## 4. Verify Installed Packages

Open `Window > Package Manager` and confirm that these packages are installed:

- [ ] **XR Hands** (hand tracking)
- [ ] **XR Interaction Toolkit** (XR interaction)
- [ ] **Input System** (Unity's new input system)
- [ ] **TextMesh Pro** (UI text rendering)
- [ ] **XR Plugin Management** (XR plugin management)

> If any package is missing, search for it in the Package Manager and click "Install".

---

## 5. Open the Main Scene

- [ ] In the **Project** window, navigate to `Assets/Scenes/`
- [ ] Double-click **`HitMissScene.unity`**
- [ ] Verify that the scene loads without errors

---

## 6. Verify the Script Structure

Confirm that the following files exist in `Assets/Scripts/HitOrMiss/`:

### Core
- [ ] `Core/HitOrMissAppController.cs` - Main session controller
- [ ] `Core/Enums.cs` - Categories and states (`TrialCategory`, `TaskPhase`, etc.)
- [ ] `Core/TrialJudgement.cs` - Data structure for individual trial results
- [ ] `Core/SpokenCommandEvent.cs` - Command event

### Task
- [ ] `Task/TrajectoryTaskAsset.cs` - Experiment configuration (`ScriptableObject`)
- [ ] `Task/TrajectoryTaskManager.cs` - Real-time trial manager
- [ ] `Task/TrialGenerator.cs` - Generator for the 80 trials per block
- [ ] `Task/TrialDefinition.cs` - Trial parameter definition

### Input
- [ ] `Input/IResponseInputSource.cs` - Common interface for all input methods
- [ ] `Input/ControllerButtonInput.cs` - XR controller input
- [ ] `Input/HandPinchInput.cs` - Finger pinch input (hand tracking)
- [ ] `Input/KeyboardCommandInput.cs` - Keyboard input (`H` = Hit, `M` = Miss)
- [ ] `Input/HitMissInputActions.cs` - Unity-generated input actions
- [ ] `Input/InputMode.cs` - Input mode enum

### Visuals
- [ ] `Visuals/TrajectoryObjectController.cs` - Controls the flying sphere and its shadow

### Logging
- [ ] `Logging/TaskLogger.cs` - CSV and JSON export
- [ ] `Logging/EegMarkerEmitter.cs` - Markers for EEG synchronization

### UI
- [ ] `UI/ClinicianControlPanel.cs` - Clinician control panel
- [ ] `UI/ResponseIndicator.cs` - Patient response visual indicator

### Localization
- [ ] `Localization/LocalizedTermTable.cs` - Translated terms table
- [ ] `Localization/LocalizedUITextBinder.cs` - UI text translation binder
- [ ] `Localization/LanguageCommandProfile.cs` - Language profile
- [ ] `Localization/DefaultTermTableCreator.cs` - Default table creator

---

## 7. Verify Prefabs and Assets

In the **Project** window:

- [ ] `Assets/Prefab/HitOrMissComponents.prefab` - Main game prefab
- [ ] `Assets/Prefab/LoomingObjectPrefab.prefab` - Sphere (ball) prefab
- [ ] `Assets/Prefab/TrajectoryTask.asset` - Task configuration
- [ ] `Assets/ScriptableObjects/HitOrMissTermTable.asset` - Localization table

---

## 8. Configure the Experiment Parameters

Select `Assets/Prefab/TrajectoryTask.asset` in the Inspector and verify/adjust:

| Parameter | Default Value | Description |
|---|---|---|
| `TaskName` | "Hit Or Miss Task" | Name of the task (appears in log files) |
| `BlockCount` | 3 | Number of blocks per session |
| `TrialsPerCategory` | 20 | Trials per category (total per block = 4 x 20 = 80) |
| `IntroDuration` | 20 sec | Duration of the introduction screen |
| `RestDuration` | 30 sec | Duration of the rest period between blocks |
| `OutroDuration` | 10 sec | Duration of the final screen |
| `SpawnDistance` | 7 m | Distance at which the ball appears |
| `VanishDistance` | 1 m | Distance at which the ball disappears |
| `Speed` | 2.5 m/s | Ball speed |
| `BallDiameter` | 0.175 m | Ball diameter (17.5 cm) |

- [ ] Values reviewed and adjusted according to the study needs

---

## 9. Configure the Input Mode

In the scene object `HitOrMissAppController`, in the Inspector:

- [ ] Select the desired **Input Mode**:
  - **Controller** - For XR controllers (left trigger = Hit, right trigger = Miss)
  - **HandPinch** - For hand tracking (left pinch = Hit, right pinch = Miss)
  - **Keyboard** - For desktop testing (`H` key = Hit, `M` key = Miss)

### For Controller mode:
- [ ] Verify that `ControllerButtonInput` is assigned in the Inspector

### For HandPinch mode:
- [ ] Verify that `HandPinchInput` is assigned in the Inspector
- [ ] Pinch threshold: 4 cm (activation) / 6 cm (release)

### For Keyboard mode (desktop testing):
- [ ] Verify that `KeyboardCommandInput` is assigned in the Inspector

---

## 10. Configure XR (Extended Reality)

If you are using a VR/MR headset:

- [ ] Go to `Edit > Project Settings > XR Plug-in Management`
- [ ] Enable the plugin corresponding to your device:
  - [ ] **OpenXR** (for most modern devices)
  - [ ] **Windows Mixed Reality** (for HoloLens)
  - [ ] **Oculus/Meta** (for Quest)
- [ ] In `Project Settings > XR Plug-in Management > OpenXR`:
  - [ ] Add **Hand Tracking Subsystem** if you are going to use hand tracking
  - [ ] Add your device's **Interaction Profiles**

If you are only testing on desktop:
- [ ] Select `InputMode.Keyboard` in the AppController
- [ ] XR configuration is not required

---

## 11. Test in the Unity Editor

- [ ] Click the **Play** button at the top of the editor
- [ ] Verify that the **Clinician Panel** appears with:
  - [ ] Participant ID field
  - [ ] Language selector (English/French)
  - [ ] Start button ("Start")
- [ ] Enter a test ID (for example: `"TEST001"`)
- [ ] Click **Start**
- [ ] Verify the sequence:
  - [ ] The **Introduction** screen appears (20 seconds)
  - [ ] **Balls** begin to fly toward the camera
  - [ ] When pressing **H** (Hit) or **M** (Miss), an indicator appears briefly
  - [ ] The clinician panel shows real-time statistics
  - [ ] After 80 trials, the **Rest** screen appears
  - [ ] The process repeats for all 3 blocks
  - [ ] At the end, the **Outro** screen appears
- [ ] Click **Stop** in the Unity editor

---

## 12. Verify the Log Files

After running a test session:

- [ ] Navigate to the Unity persistent data folder:
  - **Windows:** `%USERPROFILE%\AppData\LocalLow\[CompanyName]\[ProductName]\Logs\`
- [ ] Verify that these files were created:
  - [ ] `{ParticipantId}_{SessionId}_{TaskName}.csv` - CSV data file
  - [ ] `{ParticipantId}_{SessionId}_{TaskName}.json` - JSON data file
- [ ] Open the CSV file and confirm that it contains these columns:
  ```
  TrialId, Block, Category, Expected, Received, Result, IsCorrect,
  StimulusOnset, ResponseTime, ReactionTimeMs, LateralOffsetM,
  ApproachAngleDeg, FailureReason
  ```
- [ ] Verify EEG markers in:
  - `%USERPROFILE%\AppData\LocalLow\[CompanyName]\[ProductName]\EEG_Markers\`

---

## 13. Build for Device

- [ ] Go to `File > Build Settings`
- [ ] Select the target platform:
  - [ ] **Windows** (for desktop or Windows MR)
  - [ ] **Universal Windows Platform** (for HoloLens)
  - [ ] **Android** (for Meta Quest)
- [ ] Click **"Switch Platform"** if needed
- [ ] Verify that `HitMissScene` is in the scene list (drag it in if it is not)
- [ ] Click **"Build"** or **"Build and Run"**
- [ ] Select an output folder (for example: `Builds/`)
- [ ] Wait for the build to finish without errors

---

## 14. Pre-Clinical Session Checklist

Before each session with a real patient:

- [ ] XR device charged and working
- [ ] Application installed and open
- [ ] Correct input mode selected
- [ ] Participant ID prepared
- [ ] Correct language selected (English or French)
- [ ] EEG equipment connected and synchronized (if applicable)
- [ ] Enough disk space available for log files
- [ ] Test session successfully run on the same day
- [ ] Patient informed about the procedure:
  - The balls will fly toward them
  - They must respond "Hit" or "Miss" according to their perception
  - They will not receive feedback about whether they were correct
  - There are 3 blocks with rest periods between them

---

## Session Flow Summary

```text
Clinician presses "Start"
        |
        v
  INTRODUCTION (20 sec) -- Instructions screen
        |
        v
  BLOCK 1 -- 80 balls with an interval of ~4 sec
        |     The patient responds Hit or Miss
        |     Accuracy and reaction times are recorded
        v
  REST (30 sec) -- Pause screen
        |
        v
  BLOCK 2 -- 80 more balls
        |
        v
  REST (30 sec)
        |
        v
  BLOCK 3 -- 80 final balls
        |
        v
  OUTRO (10 sec) -- Thank-you screen
        |
        v
  Data saved as CSV + JSON + EEG markers
```

---

## Common Troubleshooting

| Problem | Solution |
|---|---|
| Red errors when opening the project | Verify the Unity version (must be `6000.3.2f1`) |
| The balls do not appear | Verify that `LoomingObjectPrefab` is assigned in `TrajectoryTaskManager` |
| `H`/`M` keys do not respond | Verify that `InputMode` is set to `Keyboard` and `KeyboardCommandInput` is assigned |
| Hand pinch is not detected | Verify that the XR Hand plugin is enabled and the device supports hand tracking |
| Log files are not generated | Verify write permissions for the persistent data folder |
| The ball moves too fast/slow | Adjust `Speed` in `TrajectoryTask.asset` |
| Too few/many trials | Adjust `TrialsPerCategory` in `TrajectoryTask.asset` (total = value x 4) |

---