# EEG Marker Receiver

This file explains how to use [`eeg_marker_receiver.py`](/d:/2025_PPS/PythonBridge/eeg_marker_receiver.py).

## Purpose

`eeg_marker_receiver.py` listens for UDP marker messages sent by the Unity HitMiss application and then:

- prints each marker to the console
- writes a CSV backup log on the Python side
- optionally publishes numeric event codes through Lab Streaming Layer (LSL)
- provides a single function, `send_to_eeg_system()`, where you can connect the markers to a real EEG acquisition system

It is intended to run on the same machine as Unity or on another machine reachable over the network.

## What Unity Sends

Unity sends one UDP packet per event as JSON. A typical message looks like this:

```json
{
  "engineTime": 1.234,
  "eventCode": "trial_spawn",
  "trialId": "B1_T01",
  "category": "Hit",
  "expected": "Hit",
  "received": "",
  "extra": ""
}
```

Important event codes include:

- `session_start`
- `session_end`
- `block_start`
- `block_end`
- `trial_spawn`
- `response_hit`
- `response_miss`
- `trial_resolved_correct`
- `trial_resolved_incorrect`
- `trial_no_response`
- `trial_timeout`

## Basic Setup

1. Make sure Python is installed.
2. Open a terminal in the project root or in `PythonBridge`.
3. Run the receiver before starting the Unity task.
4. In Unity, enable the EEG/network bridge and set the UDP port to match the script.

Default UDP port:

```text
12345
```

## Run Commands

Run with default settings:

```powershell
python PythonBridge\eeg_marker_receiver.py
```

Run on a custom port:

```powershell
python PythonBridge\eeg_marker_receiver.py --port 12345
```

Run with LSL enabled:

```powershell
python PythonBridge\eeg_marker_receiver.py --lsl
```

Run with a custom CSV log folder:

```powershell
python PythonBridge\eeg_marker_receiver.py --log-dir PythonBridge\logs
```

You can combine options:

```powershell
python PythonBridge\eeg_marker_receiver.py --port 12345 --lsl --log-dir PythonBridge\logs
```

## Output

When running, the script:

- prints a live table of incoming markers
- creates a CSV file named like `eeg_markers_python_YYYYMMDD_HHMMSS.csv`
- stops automatically when it receives `session_end`

The CSV contains these columns:

- `PythonTimestamp`
- `EngineTime`
- `EventCode`
- `TrialId`
- `Category`
- `Expected`
- `Received`
- `Extra`

## LSL Mode

If you use the `--lsl` flag, install `pylsl` first:

```powershell
pip install pylsl
```

The script creates an LSL outlet with:

- stream name: `HitMissMarkers`
- stream type: `Markers`
- one channel
- `int32` values

The integer sent to LSL is taken from the `EVENT_CODES` dictionary in the script.

## Connecting to a Real EEG System

The script does not directly control hardware yet. The integration point is:

[`send_to_eeg_system()`](/d:/2025_PPS/PythonBridge/eeg_marker_receiver.py:45)

Right now, that function only prints the numeric code that would be sent. To use a real EEG device, replace the body of that function with the API or transport required by your acquisition software.

The script already includes example patterns in comments for:

- BrainVision Recorder
- BioSemi
- LSL
- NeuroScan
- serial-based systems such as g.tec or OpenBCI

## Event Code Mapping

Numeric trigger values are defined in:

[`EVENT_CODES`](/d:/2025_PPS/PythonBridge/eeg_marker_receiver.py:85)

Adjust those values so they match the trigger codes expected by your EEG recording pipeline.

## Typical Workflow

1. Start the Python receiver.
2. Confirm it is listening on the expected UDP port.
3. Start the Unity HitMiss task.
4. Verify markers appear in the receiver console.
5. Confirm the CSV is being written.
6. If using LSL or hardware triggers, verify the downstream system receives the same events.

## Notes

- `trial_spawn` is usually the most important stimulus-onset marker for EEG analysis.
- `.meta` files are not relevant to the Python script itself, but Unity-side assets that emit markers should still keep their `.meta` files in version control.
- If no markers appear, first check that Unity is configured to use the network bridge and that both sides use the same port.
