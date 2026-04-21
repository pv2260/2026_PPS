"""
HitMiss EEG Marker Receiver
============================
Receives UDP markers from Unity's EegMarkerEmitter and forwards them
to your EEG acquisition system.

Usage:
    python eeg_marker_receiver.py
    python eeg_marker_receiver.py --port 12345
    python eeg_marker_receiver.py --port 12345 --lsl    (with Lab Streaming Layer)

Protocol:
    Unity sends JSON over UDP, one packet per marker:
    {"engineTime":1.234,"eventCode":"trial_spawn","trialId":"B1_T01",
     "category":"Hit","expected":"Hit","received":"","extra":""}

Event codes emitted by Unity:
    session_start          - Session begins
    session_end            - Session ends
    phase_intro            - Intro phase (instructions shown)
    phase_block            - Block phase begins
    phase_rest             - Rest period between blocks
    phase_outro            - Outro phase (thank you screen)
    block_start            - A block of 80 trials starts
    block_end              - A block ends
    trial_spawn            - A ball appears (stimulus onset)
    response_hit           - Patient responded "Hit"
    response_miss          - Patient responded "Miss"
    trial_resolved_correct - Trial scored as correct
    trial_resolved_incorrect - Trial scored as incorrect
    trial_no_response      - Patient didn't respond in time
    trial_timeout          - Response window expired
"""

import socket
import json
import argparse
import csv
import os
from datetime import datetime


# ---------------------------------------------------------------------------
# 1. ADAPT THIS SECTION TO YOUR EEG SYSTEM
# ---------------------------------------------------------------------------

def send_to_eeg_system(marker: dict):
    """
    Called for every marker received from Unity.
    Replace this function body with your EEG system's API.

    Examples for common EEG systems:

    --- BrainVision Recorder (via serial/parallel port) ---
        import serial
        port = serial.Serial('COM3', 115200)
        code = EVENT_CODES.get(marker['eventCode'], 0)
        port.write(bytes([code]))

    --- BioSemi (via parallel port on Windows) ---
        import ctypes
        inpout = ctypes.WinDLL('inpoutx64.dll')
        code = EVENT_CODES.get(marker['eventCode'], 0)
        inpout.Out32(0x378, code)       # Send code
        time.sleep(0.005)               # 5 ms pulse
        inpout.Out32(0x378, 0)          # Reset

    --- Lab Streaming Layer (LSL) ---
        See the --lsl flag implementation below (push_sample).

    --- NeuroScan (via TCP) ---
        import socket
        ns_sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        ns_sock.connect(('192.168.1.100', 4000))
        code = EVENT_CODES.get(marker['eventCode'], 0)
        ns_sock.send(code.to_bytes(4, 'little'))

    --- g.tec / OpenBCI / other serial-based systems ---
        import serial
        port = serial.Serial('COM4', 115200)
        port.write(f"{marker['eventCode']}\\n".encode())
    """
    # Default: just print to console
    print(f"  -> EEG SYSTEM: would send code {EVENT_CODES.get(marker['eventCode'], 0)} "
          f"for '{marker['eventCode']}'")


# Numeric codes for your EEG system (customize these to match your protocol)
EVENT_CODES = {
    "session_start":             1,
    "session_end":               2,
    "phase_intro":              10,
    "phase_block":              11,
    "phase_rest":               12,
    "phase_outro":              13,
    "block_start":              20,
    "block_end":                21,
    "trial_spawn":              30,   # Stimulus onset - most important marker
    "response_hit":             40,
    "response_miss":            41,
    "trial_resolved_correct":   50,
    "trial_resolved_incorrect": 51,
    "trial_no_response":        52,
    "trial_timeout":            53,
}


# ---------------------------------------------------------------------------
# 2. UDP RECEIVER (no need to modify)
# ---------------------------------------------------------------------------

def create_receiver(port: int) -> socket.socket:
    """Create and bind a UDP socket to receive markers from Unity."""
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    sock.bind(("0.0.0.0", port))
    print(f"[Receiver] Listening for UDP markers on port {port}...")
    print(f"[Receiver] In Unity, enable 'Use Network Bridge' and set port to {port}")
    print()
    return sock


def receive_loop(sock: socket.socket, csv_writer, lsl_outlet=None):
    """Main loop: receive UDP packets, parse JSON, forward to EEG system."""
    print("[Receiver] Waiting for markers from Unity...\n")
    print(f"{'Time':>12}  {'Event Code':<30}  {'Trial ID':<10}  {'Category':<10}  {'Extra'}")
    print("-" * 85)

    try:
        while True:
            data, addr = sock.recvfrom(4096)
            try:
                marker = json.loads(data.decode("utf-8"))
            except json.JSONDecodeError:
                print(f"[WARNING] Invalid JSON from {addr}: {data[:100]}")
                continue

            # Display in console
            print(f"{marker.get('engineTime', 0):>12.4f}  "
                  f"{marker.get('eventCode', '?'):<30}  "
                  f"{marker.get('trialId', ''):<10}  "
                  f"{marker.get('category', ''):<10}  "
                  f"{marker.get('extra', '')}")

            # Write to local Python-side CSV (backup)
            if csv_writer:
                csv_writer.writerow([
                    datetime.now().isoformat(),
                    marker.get("engineTime", ""),
                    marker.get("eventCode", ""),
                    marker.get("trialId", ""),
                    marker.get("category", ""),
                    marker.get("expected", ""),
                    marker.get("received", ""),
                    marker.get("extra", ""),
                ])

            # Forward to LSL if enabled
            if lsl_outlet is not None:
                code = EVENT_CODES.get(marker.get("eventCode", ""), 0)
                lsl_outlet.push_sample([code])

            # Forward to your EEG system
            send_to_eeg_system(marker)

            # Stop listening when session ends
            if marker.get("eventCode") == "session_end":
                print("\n[Receiver] Session ended. Stopping.")
                break

    except KeyboardInterrupt:
        print("\n[Receiver] Interrupted by user.")


# ---------------------------------------------------------------------------
# 3. LAB STREAMING LAYER (optional)
# ---------------------------------------------------------------------------

def create_lsl_outlet():
    """Create an LSL outlet for marker streaming. Requires: pip install pylsl"""
    try:
        from pylsl import StreamInfo, StreamOutlet
        info = StreamInfo(
            name="HitMissMarkers",
            type="Markers",
            channel_count=1,
            nominal_srate=0,          # Irregular rate (event-based)
            channel_format="int32",
            source_id="hitmiss_unity_bridge"
        )
        outlet = StreamOutlet(info)
        print("[LSL] Outlet created: 'HitMissMarkers' (Markers, 1 channel, int32)")
        print("[LSL] Other LSL apps can now receive markers from this stream")
        return outlet
    except ImportError:
        print("[LSL] ERROR: pylsl not installed. Run: pip install pylsl")
        print("[LSL] Continuing without LSL support.")
        return None


# ---------------------------------------------------------------------------
# 4. ENTRY POINT
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(
        description="Receive EEG markers from HitMiss Unity app via UDP"
    )
    parser.add_argument(
        "--port", type=int, default=12345,
        help="UDP port to listen on (must match Unity's BridgePort, default: 12345)"
    )
    parser.add_argument(
        "--lsl", action="store_true",
        help="Also stream markers via Lab Streaming Layer (requires: pip install pylsl)"
    )
    parser.add_argument(
        "--log-dir", type=str, default=".",
        help="Directory to save the Python-side CSV backup (default: current dir)"
    )
    args = parser.parse_args()

    # CSV backup on the Python side
    os.makedirs(args.log_dir, exist_ok=True)
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    csv_path = os.path.join(args.log_dir, f"eeg_markers_python_{timestamp}.csv")
    csv_file = open(csv_path, "w", newline="", encoding="utf-8")
    csv_writer = csv.writer(csv_file)
    csv_writer.writerow([
        "PythonTimestamp", "EngineTime", "EventCode", "TrialId",
        "Category", "Expected", "Received", "Extra"
    ])
    print(f"[Receiver] Python-side CSV: {csv_path}")

    # LSL outlet (optional)
    lsl_outlet = create_lsl_outlet() if args.lsl else None

    # Start listening
    sock = create_receiver(args.port)
    try:
        receive_loop(sock, csv_writer, lsl_outlet)
    finally:
        csv_file.close()
        sock.close()
        print(f"[Receiver] CSV saved to {csv_path}")


if __name__ == "__main__":
    main()
