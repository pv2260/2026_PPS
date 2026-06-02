import socket
import serial
import time

# ── Configuration ──────────────────────────────────────────────────────────────
ARDUINO_PORT = "COM7"
BAUDRATE = 9600

TCP_HOST = "127.0.0.1"
TCP_PORT = 5005

# ── Serial setup ───────────────────────────────────────────────────────────────
ser = serial.Serial(ARDUINO_PORT, BAUDRATE)
time.sleep(2)  # allow Arduino reset

print(f"[Server] Arduino on   : {ARDUINO_PORT}")
print(f"[Server] TCP listening: {TCP_HOST}:{TCP_PORT}")
print("[Server] Waiting for Unity connection...\n")

# ── TCP server ──────────────────────────────────────────────────────────────────
with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
    server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
    server.bind((TCP_HOST, TCP_PORT))
    server.listen(1)

    conn, addr = server.accept()
    print(f"[Server] Unity connected: {addr}\n")

    buffer = ""

    with conn:
        while True:
            data = conn.recv(64)
            if not data:
                print("[Server] Unity disconnected.")
                break

            buffer += data.decode("utf-8")

            while "\n" in buffer:
                line, buffer = buffer.split("\n", 1)
                line = line.strip()

                if not line:
                    continue

                try:
                    value = int(line)
                except ValueError:
                    print(f"[Server] Invalid value: {line!r}")
                    continue

                if not 0 <= value <= 255:
                    print(f"[Server] Out of range: {value}")
                    continue

                print(f"[Arduino] Sending byte: {value}")
                ser.write(bytes([value]))


# import socket
# from byte_triggers import ParallelPortTrigger

# # ── Configuration ──────────────────────────────────────────────────────────────
# # Serial port used by the Arduino or parallel-port converter.
# # Change this if your device appears on another COM port.
# ARDUINO_PORT = "COM3"

# # Local IP address for the Python server.
# # "127.0.0.1" means only this computer can connect.
# TCP_HOST = "127.0.0.1"

# # TCP port that Unity will connect to.
# # This must match the port defined in your Unity C# script.
# TCP_PORT = 5005

# # ── Setup ──────────────────────────────────────────────────────────────────────
# # Create the trigger object that sends values to the Arduino / converter.
# # "arduino" tells byte_triggers to use the Arduino backend.
# trigger = ParallelPortTrigger(ARDUINO_PORT)

# # Print startup information so you can verify the configuration in the console.
# print(f"[Server] Arduino on      : {ARDUINO_PORT}")
# print(f"[Server] Listening on    : {TCP_HOST}:{TCP_PORT}")
# print("[Server] Waiting for Unity to connect … (Ctrl+C to quit)\n")

# # Create a TCP socket that will act as the server.
# # The 'with' block ensures the socket is automatically closed when done.
# with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as server:
#     # Allow the socket address to be reused quickly after closing.
#     # This helps if you restart the script often during debugging.
#     server.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)

#     # Bind the server to the chosen IP address and port.
#     # This tells Python where to listen for incoming Unity connections.
#     server.bind((TCP_HOST, TCP_PORT))

#     # Start listening for incoming client connections.
#     # The number '1' means only one pending connection is queued.
#     server.listen(1)

#     # Wait until Unity connects.
#     # 'accept()' blocks here until a client connects.
#     conn, addr = server.accept()
#     print(f"[Server] Unity connected from {addr}\n")
#     # Use another 'with' block so the client connection closes cleanly.
#     with conn:
#         # Buffer stores incoming text until a full line is received.
#         # This is needed because TCP can split messages across packets.
#         buffer = ""

#         while True:
#             # Read up to 64 bytes from the Unity connection.
#             # If Unity disconnects, 'data' will be empty.
#             data = conn.recv(64)
#             if not data:
#                 print("[Server] Unity disconnected.")
#                 break

#             # Decode bytes into a string and append to the buffer.
#             # We keep leftover partial messages in 'buffer'.
#             buffer += data.decode("utf-8")

#             # Process all complete lines currently in the buffer.
#             # Each line should contain one integer trigger value.
#             while "\n" in buffer:
#                 # Split off the first complete line.
#                 line, buffer = buffer.split("\n", 1)

#                 # Remove spaces, tabs, and carriage returns.
#                 line = line.strip()

#                 # Skip empty lines.
#                 if not line:
#                     continue
#                 # Convert the text line into an integer.
#                 # Example: "20" -> 20
#                 try:
#                     value = int(line)
#                 except ValueError:
#                     print(f"[Server] Non-integer, skipping: {line!r}")
#                     continue

#                 # Make sure the trigger is in the valid byte range.
#                 # byte_triggers expects values from 0 to 255.
#                 if not 0 <= value <= 255:
#                     print(f"[Server] Out of range (0-255), skipping: {value}")
#                     continue

#                 # Send the trigger value to the Arduino / converter.
#                 # This is the actual hardware trigger output.
#                 print(f"[Arduino] Send value: {value}")
#                 trigger.signal(value)