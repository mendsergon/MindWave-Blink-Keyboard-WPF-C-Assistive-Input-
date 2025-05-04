using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Runtime.InteropServices; // Allows calling native Windows functions
using System.Diagnostics; // Allows interaction with system processes amd diagnostics
using System.Net.Sockets; // Provides networking classes for TCP/IP connections
using Timer = System.Threading.Timer; // Prevents name conflincts with other Timer classes
using Button = System.Windows.Controls.Button; // Makes sure we are using WPF Button
using System.Windows.Threading; // Provides DispacherTimer
using Newtonsoft.Json; // Imports for JSON parsing and serialization



namespace Keyboard
{
    public partial class MainWindow : Window
    {
        private Timer clearTimer; // Timer
        private int currentSelection = 0; // Start at _10
        private bool isTimerRunning = false; // Track if the timer is running
        private int hitCount = 0; // Track how many signals have been received
        private Button previousSelectedButton; // Keep track of previously selected button
        private Dictionary<string, System.Windows.Media.Brush> originalButtonColors = new Dictionary<string, System.Windows.Media.Brush>(); // Store the original button color to reset later
        private enum SelectionPhase { Idle, Horizontal, Vertical } // Enum representing current phase of selection
        private SelectionPhase currentPhase = SelectionPhase.Idle; // Initialize current selection to idle
        private DispatcherTimer uiTimer; // Timer to update countdown UI
        private int remainingSeconds; // Count seconds remaining for UI
        private const int countdownSeconds = 5; // Constant countdown duration 

        // Import Windows API to bring window to foreground and print the total text
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);



        public MainWindow()
        {
            InitializeComponent(); // Initialize UI components
            InitializeTimers(); // Set up UI timer

            // Set up the constant timer
            clearTimer = new Timer(TimerTick, null, Timeout.Infinite, Timeout.Infinite); // Initialize the timer, but don't start it
            clearTimer.Change(Timeout.Infinite, Timeout.Infinite); // Wait for signal before starting

            // Start listening in a background thread for EEG signals
            Thread listenerThread = new Thread(StartListeningForHit);
            listenerThread.IsBackground = true; // Ensure that thread closes when app exits
            listenerThread.Start();
        }

        // Method to start TCP connection and listen for blink data
        private void StartListeningForHit()
        {
            string host = "127.0.0.1"; // Localhost
            int port = 13854; // Default ThinkGear Connector port

            try
            {
                // Log connection attempt to UI
                Dispatcher.Invoke(() =>
                {
                    LogOutput.Text += $"Attempting to connect to ThinkGear Connector on {host}:{port}...\n";
                });

                // Establish TCP connection to ThinkGear Connector
                using (TcpClient client = new TcpClient(host, port))
                using (NetworkStream stream = client.GetStream())
                {
                    // Log success
                    Dispatcher.Invoke(() =>
                    {
                        LogOutput.Text += "Connected to ThinkGear Connector.\n";
                    });

                    // Send JSON config to disable raw EEG output and use JSON format
                    string configMessage = "{\"enableRawOutput\":false,\"format\":\"Json\"}\n";
                    byte[] configBytes = Encoding.UTF8.GetBytes(configMessage);
                    stream.Write(configBytes, 0, configBytes.Length);

                    // Buffer incoming data
                    byte[] buffer = new byte[1024];

                    // Continuously listen for incoming data
                    while (true)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            // Decode JSON string from bytes
                            string jsonString = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                            try
                            {
                                // Deserialize JSON into BlinkData object
                                BlinkData blinkData = JsonConvert.DeserializeObject<BlinkData>(jsonString);

                                // If blink strength detected and non-zero
                                if (blinkData != null && blinkData.BlinkStrength > 0)
                                {
                                    // Treat blinks with blink strength >= 0 as "HIT"
                                    string message = blinkData.BlinkStrength >= 70 ? "HIT" : "MISS";

                                    // On "HIT" call "HandleHitDetected" method
                                    if (message == "HIT")
                                    {
                                        HandleHitDetected();
                                    }
                                }
                            }
                            catch (Newtonsoft.Json.JsonException)
                            {
                                // Ignore malformed packets cause the program will crash :(
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log connection error
                Dispatcher.Invoke(() =>
                {
                    LogOutput.Text += $"Connection error: {ex.Message}\n";
                });
            }
            finally
            {
                // Log disconnection
                Dispatcher.Invoke(() =>
                {
                    LogOutput.Text += "Disconnected from ThinkGear Connector.\n";
                });
            }
        }

        // Method TO handle the logic when a "HIT" is detected
        private void HandleHitDetected()
        {
            Dispatcher.Invoke(() =>
            {
                LogOutput.ScrollToEnd(); // Scroll log to bottom

                // If Idle, switch to Horizontal selection phase 
                if (currentPhase == SelectionPhase.Idle)
                {
                    currentPhase = SelectionPhase.Horizontal;
                    LogOutput.Text += "Entered horizontal selection phase.\n";
                }

                // Restart timer after each "HIT"
                clearTimer.Change(5000, Timeout.Infinite);
                isTimerRunning = true;
                hitCount++;

                // Restart the UI timer after each "HIT"
                remainingSeconds = countdownSeconds;
                TimerText.Text = $"{remainingSeconds}";
                uiTimer.Start();

                // Move selection based on the current phase
                if (currentPhase == SelectionPhase.Horizontal)
                {
                    MoveSelection();
                }
                else if (currentPhase == SelectionPhase.Vertical)
                {
                    MoveSelectionVertically();
                }
            });
        }

        // Method to move horizontally
        private void MoveSelection()
        {
            // From _10 we move to _11
            if (currentSelection == 0)
            {
                currentSelection = 1; 
            }
            // From _18 we move to _11
            else if (currentSelection == 8) 
            {
                currentSelection = 1; 
            }
            // From _28 we move to _21
            else if (currentSelection == 18) 
            {
                currentSelection = 11; 
            }
            // From _38 we move to _31
            else if (currentSelection == 28) 
            {
                currentSelection = 21; 
            }
            // From _48 we move to _41
            else if (currentSelection == 38) 
            {
                currentSelection = 31; 
            }
            // From _58 we move to _51
            else if (currentSelection == 48) 
            {
                currentSelection = 41; 
            }
            // Everywhere else we move to the next button
            else
            {
                currentSelection++;
            }


            // Update the button color 
            UpdateButtonColor();

            // Display the current selection in the UI
            Dispatcher.Invoke(() =>
            {
                LogOutput.Text += $"Current selection: {currentSelection + 10}\n"; // Display actual button number (_10, _11, etc.)
            });
        }

        // Method to move vertically
        private void MoveSelectionVertically()
        {
            // From _51 we move to _11
            if (currentSelection == 41) 
            {
                currentSelection = 1; 
            }
            // From _52 we move to _12
            else if (currentSelection == 42) 
            {
                currentSelection = 2; 
            }
            // From _53 we move to _13
            else if (currentSelection == 43) 
            {
                currentSelection = 3; 
            }
            // From _54 we move to _14
            else if (currentSelection == 44) 
            {
                currentSelection = 4; 
            }
            // From _55 we move to _15
            else if (currentSelection == 45) 
            {
                currentSelection = 5; 
            }
            // From _56 we move to _16
            else if (currentSelection == 46) 
            {
                currentSelection = 6; 
            }
            // From _57 we move to _17
            else if (currentSelection == 47) 
            {
                currentSelection = 7; 
            }
            // From _58 we move to _18
            else if (currentSelection == 48) 
            {
                currentSelection = 8; 
            }
            // Everywhere else we move to the button below, in our case we need to move 10 buttons across
            else
            {
                currentSelection = currentSelection + 10;
            }

            // Update the button color 
            UpdateButtonColor();

            // Display the current selection in the UI
            Dispatcher.Invoke(() =>
            {
                LogOutput.Text += $"Current selection: {currentSelection + 10}\n"; // Display actual button number (_10, _11, etc.)
            });
        }

        // Method to press button
        private void PressButton()
        {
            Dispatcher.Invoke(() =>
            {
                // Construct the button name 
                string buttonName = $"_{currentSelection + 10}";

                // Log the press attempt to the UI
                LogOutput.Text += $"Button {buttonName} pressed!\n";

                // Find the button by name
                Button buttonToPress = this.FindName(buttonName) as Button;

                if (buttonToPress != null)
                {
                    // Simulate the button click by raising the event programmatically
                    buttonToPress.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else
                {
                    // If the button is not found, log an error to the UI
                    LogOutput.Text += $"Button {buttonName} not found.\n";
                }

                // After the button press, reset the selection and state
                currentSelection = 0;
                hitCount = 0;
                currentPhase = SelectionPhase.Idle;
                isTimerRunning = false;

                // Reset button color 
                UpdateButtonColor();
            });
        }

        // Method to hightlight currently selected button and reset the previous
        private void UpdateButtonColor()
        {
            // Reset color for the previously selected button, if any
            if (previousSelectedButton != null)
            {
                // Check if we have stored the original color for the previous button
                if (originalButtonColors.ContainsKey(previousSelectedButton.Name))
                {
                    previousSelectedButton.Background = originalButtonColors[previousSelectedButton.Name];
                }
                else
                {
                    // If no original color is stored, use a default fallback color 
                    previousSelectedButton.Background = System.Windows.Media.Brushes.Gray;
                }
            }

            // Find the button corresponding to the current selection 
            string buttonName = $"_{currentSelection + 10}"; // Add 10 for the display format 
            Button button = this.FindName(buttonName) as Button;

            if (button != null)
            {
                // If the button color hasn't been stored yet, store its original color
                if (!originalButtonColors.ContainsKey(buttonName))
                {
                    originalButtonColors[buttonName] = button.Background;
                }

                // Change button color to red to indicate selection
                button.Background = System.Windows.Media.Brushes.Red;
                previousSelectedButton = button;

                
            }
        }

        // Method for timer handling the switching of phases and the press of the button
        private void TimerTick(object state)
        {
            Dispatcher.Invoke(() =>
            {
                if (currentPhase == SelectionPhase.Horizontal)
                {
                    // If the timer expires while in Horizontal mode, switch to Vertical
                    currentPhase = SelectionPhase.Vertical;
                    LogOutput.ScrollToEnd();
                    LogOutput.Text += "Switched to vertical selection mode.\n"; // Print to UI

                    clearTimer.Change(5000, Timeout.Infinite); // Restart timer
                    isTimerRunning = true;
                    hitCount = 0;

                    // Restart UI timer
                    remainingSeconds = countdownSeconds;
                    TimerText.Text = $"{remainingSeconds}"; // Print to UI
                    uiTimer.Start();
                }
                else if (currentPhase == SelectionPhase.Vertical)
                {
                    // If the timer expires while in Vertical mode, press the button
                    PressButton();

                    // Reset everything
                    currentPhase = SelectionPhase.Idle;
                    isTimerRunning = false;
                    hitCount = 0;
                    uiTimer.Stop();
                    TimerText.Text = "";
                }
            });
        }

        // Method to initialize the UI timer
        private void InitializeTimers()
        {
            uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromSeconds(1);
            uiTimer.Tick += UiTimer_Tick;
        }

        // Method to handle the UI timer's countdown ticks
        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (remainingSeconds > 0)
            {
                remainingSeconds--;
                TimerText.Text = $"{remainingSeconds}";
            }
            else
            {
                uiTimer.Stop();
                TimerText.Text = "";
            }
        }

        

        // Buttons
        private void _11_Click(object sender, RoutedEventArgs e) // Q
        {
            TextTotal.Text = TextTotal.Text + "Q";
        }

        private void _12_Click(object sender, RoutedEventArgs e) // W
        {
            TextTotal.Text = TextTotal.Text + "W";
        }

        private void _13_Click(object sender, RoutedEventArgs e) // E
        {
            TextTotal.Text = TextTotal.Text + "E";
        }

        private void _14_Click(object sender, RoutedEventArgs e) // R
        {
            TextTotal.Text = TextTotal.Text + "R";
        }

        private void _15_Click(object sender, RoutedEventArgs e) // T
        {
            TextTotal.Text = TextTotal.Text + "T";
        }

        private void _16_Click(object sender, RoutedEventArgs e) // Y
        {
            TextTotal.Text = TextTotal.Text + "Y";
        }

        private void _17_Click(object sender, RoutedEventArgs e) // U
        {
            TextTotal.Text = TextTotal.Text + "U";
        }

        private void _18_Click(object sender, RoutedEventArgs e) // I
        {
            TextTotal.Text = TextTotal.Text + "I";
        }

        private void _21_Click(object sender, RoutedEventArgs e) // O
        {
            TextTotal.Text = TextTotal.Text + "O";
        }

        private void _22_Click(object sender, RoutedEventArgs e) // P
        {
            TextTotal.Text = TextTotal.Text + "P";
        }

        private void _23_Click(object sender, RoutedEventArgs e) // A
        {
            TextTotal.Text = TextTotal.Text + "A";
        }

        private void _24_Click(object sender, RoutedEventArgs e) // S
        {
            TextTotal.Text = TextTotal.Text + "S";
        }

        private void _25_Click(object sender, RoutedEventArgs e) // D
        {
            TextTotal.Text = TextTotal.Text + "D";
        }

        private void _26_Click(object sender, RoutedEventArgs e) // F
        {
            TextTotal.Text = TextTotal.Text + "F";
        }

        private void _27_Click_1(object sender, RoutedEventArgs e) // G
        {
            TextTotal.Text = TextTotal.Text + "G";
        }

        private void _28_Click(object sender, RoutedEventArgs e) // H
        {
            TextTotal.Text = TextTotal.Text + "H";
        }

        private void _31_Click(object sender, RoutedEventArgs e) // J
        {
            TextTotal.Text = TextTotal.Text + "J";
        }

        private void _32_Click(object sender, RoutedEventArgs e) // K
        {
            TextTotal.Text = TextTotal.Text + "K";
        }

        private void _33_Click(object sender, RoutedEventArgs e) // L
        {
            TextTotal.Text = TextTotal.Text + "L";
        }

        private void _34_Click(object sender, RoutedEventArgs e) // Z
        {
            TextTotal.Text = TextTotal.Text + "Z";
        }

        private void _35_Click(object sender, RoutedEventArgs e) // X
        {
            TextTotal.Text = TextTotal.Text + "X";
        }

        private void _36_Click(object sender, RoutedEventArgs e) // C
        {
            TextTotal.Text = TextTotal.Text + "C";
        }

        private void _37_Click(object sender, RoutedEventArgs e) // V
        {
            TextTotal.Text = TextTotal.Text + "V";
        }

        private void _38_Click(object sender, RoutedEventArgs e) // B
        {
            TextTotal.Text = TextTotal.Text + "B";
        }

        private void _41_Click(object sender, RoutedEventArgs e) // N
        {
            TextTotal.Text = TextTotal.Text + "N";
        }

        private void _42_Click(object sender, RoutedEventArgs e) // M
        {
            TextTotal.Text = TextTotal.Text + "M";
        }

        private void _43_Click(object sender, RoutedEventArgs e) // 1
        {
            TextTotal.Text = TextTotal.Text + "1";
        }

        private void _44_Click(object sender, RoutedEventArgs e) // 2
        {
            TextTotal.Text = TextTotal.Text + "2";
        }

        private void _45_Click(object sender, RoutedEventArgs e) // 3
        {
            TextTotal.Text = TextTotal.Text + "3";
        }

        private void _46_Click(object sender, RoutedEventArgs e) // 4
        {
            TextTotal.Text = TextTotal.Text + "4";
        }

        private void _47_Click(object sender, RoutedEventArgs e) // 5
        {
            TextTotal.Text = TextTotal.Text + "5";
        }

        private void _48_Click(object sender, RoutedEventArgs e) // 6
        {
            TextTotal.Text = TextTotal.Text + "6";
        }

        private void _51_Click(object sender, RoutedEventArgs e) // 7
        {
            TextTotal.Text = TextTotal.Text + "7";
        }

        private void _52_Click(object sender, RoutedEventArgs e) // 8
        {
            TextTotal.Text = TextTotal.Text + "8";
        }

        private void _53_Click(object sender, RoutedEventArgs e) // 9
        {
            TextTotal.Text = TextTotal.Text + "9";
        }

        private void _54_Click(object sender, RoutedEventArgs e) // 0
        {
            TextTotal.Text = TextTotal.Text + "0";
        }

        private void _55_Click(object sender, RoutedEventArgs e) // DELETE
        {
            if (!string.IsNullOrEmpty(TextTotal.Text))
            {
                TextTotal.Text = TextTotal.Text.Substring(0, TextTotal.Text.Length - 1);
            }
        }

        private void _56_Click(object sender, RoutedEventArgs e) // SPACE
        {
            TextTotal.Text = TextTotal.Text + " ";
        }

        private void _57_Click(object sender, RoutedEventArgs e) // Send message to selected window
        {
            string targetWindowTitle = "Notepad"; // Define window title we are searching for
            string textToSend = TextTotal.Text; // Retrieve text from TextTotal textbox

            // Find the target window
            foreach (Process proc in Process.GetProcesses())
            {
                string windowTitle = proc.MainWindowTitle;

                // Remove leading "*" if it exists, "*" appears when a program is unsaved on windows
                if (windowTitle.StartsWith("*"))
                {
                    windowTitle = windowTitle.Substring(1).Trim();
                }

                // Check if the window title contains the base title
                if (windowTitle.Contains(targetWindowTitle) && proc.MainWindowHandle != IntPtr.Zero)
                {
                    // Bring the window to the foreground
                    SetForegroundWindow(proc.MainWindowHandle);

                    // Wait for the window to be fully focused
                    System.Threading.Thread.Sleep(500);

                    // Send the text to the window after focus is confirmed
                    SendKeys.SendWait(textToSend);  

                    // Press Enter, to send the text automatically 
                    SendKeys.SendWait("{ENTER}"); 

                    break; // Stop once the window is found and text is sent
                }
            }

            // Clear the TextTotal TextBox after sending the text
            TextTotal.Clear();  
        }

        private void _58_Click(object sender, RoutedEventArgs e) // Exit application
        {
            // Display confirmation box asking if the user wants to exit
            var result = System.Windows.MessageBox.Show("Are you sure you want to exit?", "Exit", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                // Shutdown the application
                System.Windows.Application.Current.Shutdown();
            }
        }


        // UI textboxes
        private void TextTotal_TextChanged(object sender, TextChangedEventArgs e) // Text window
        {

        }

        private void TimerText_TextChanged(object sender, TextChangedEventArgs e) // UI timer
        {

        }

        private void LogOutput_TextChanged(object sender, TextChangedEventArgs e) // Log
        {

        }
    }


}