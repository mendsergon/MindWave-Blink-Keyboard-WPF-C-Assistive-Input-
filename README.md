# Disclaimer
This software is for research and educational purposes only.
It is not affiliated with, endorsed by, or certified by NeuroSky, Inc.
Users must obtain NeuroSky software and the ThinkGear Connector directly from NeuroSky.
No proprietary NeuroSky software or SDK components are included in this repository.

# Program Description
This program is a Windows desktop application designed to provide an assistive virtual keyboard interface for individuals with Locked-in Syndrome.
It enables users to compose messages using only intentional eye blinks, which are detected via a NeuroSky MindWave Mobile 2 EEG headset.
The application interprets blinks as input commands to select and "press" virtual keyboard buttons through a two-step scanning process (horizontal and vertical selection).

# System Components & Workflow
1) Hardware & Communication Setup: The application connects to NeuroSky's ThinkGear Connector via a TCP/IP socket (127.0.0.1 : 13854).
                                   Upon connection, the program sends a JSON configuration message to ensure that: - Raw EEG output is disabled.
                                                                                                                   - The data format is JSON for easier parsing.
                                   The system continuously listens for incoming JSON-formatted blink data.

2) Blink Detection: Incoming TCP data packets are deserialized into a BlinkData object using Newtonsoft.Json.
                    If a blink strength is detected and is ≥ 70, it is classified as a "HIT" (intentional blink). Weaker signals are ignored.
                    When a HIT is detected, the application invokes HandleHitDetected() to process it as an input event.

3) Selection Phases: The core of the interface uses a two-phase scanning method to allow users to select a button.
                     Phases: -Idle: The system waits passively for the user's first blink to start interaction.
                             -Horizontal: After the first blink, each subsequent blink moves the selection horizontally across the first row of the keyboard. If the last button is reached and the user blinks, the selection
                              wraps to the start of the row. A countdown timer starts after each blink (resets after each blink). If no blink is detected within 5 seconds, the system switches to the vertical phase.
                             -Vertical: The user now cycles vertically through the selected column. Now, each blink moves the selection downward. If the bottom button is reached and the user blinks, the selection wraps
                              back to the top. The timer works the same way here, except that if it expires, the currently selected button is pressed.   
                             -Press & Reset: The selected button is programmatically "pressed". Afterwards, the system resets to the idle phase, ready for a new cycle.

5) Timers: -A Threading Timer (clearTimer) is used to manage the switching between phases and button presses. 
           -A Dispatcher Timer (uiTimer) provides a visual countdown in the UI (TimerText) to show the user how much time remains to blink before the automatic phase switch.

6) Button Color Update: The currently highlighted button is visually marked red, and previously selected buttons revert to their original color.

7) Thread Safety & UI Updates: All updates to the UI are wrapped in Dispatcher.Invoke() calls to ensure safe access from the background listener thread.

# Summary of Interaction Flow
1) User blinks once -> enters Horizontal selection.
2) Each blink cycles through buttons horizontally (wraps row end).
3) If no blink within 5 seconds -> switches to Vertical selection.
4) Each blink cycles through buttons vertically through the selected column (wraps at column end).
5) If no blink within 5 seconds -> selected button is pressed.
6) System resets to Idle phase.

# Key Features
Hands-free virtual keyboard navigation using EEG blink detection.
Two-phase row/column scanning method.
Visual countdown timer for user feedback.
Safe multithreaded UI updates.
Automatic fallback handling of malformed or missing EEG packets.

# Considerations
The default timer is set to 5 seconds. It can be adjusted in the code to better suit individual users' blink speed or preference.
Currently, no explicit error handling for unexpected disconnection mid-session from the ThinkGear Connector (the app logs an error but does not auto-reconnect).
By default, the program sends the composed text to the open Notepad application. This output can be modified in the code to direct the text to a different application.                  
