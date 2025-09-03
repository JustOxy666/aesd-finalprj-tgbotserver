using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

public enum gnssposget_app_states
{
    STATE_INIT = 0,
    STATE_START_REQUESTED = 1,
    STATE_WORKING = 2,
    STATE_ABORT_REQUESTED = 3,
    STATE_FINISHED = 4,
    STATE_DONE = 5,
    STATE_ERROR = 6,
    STATE_UNEXPECTED_ERROR = 7
}

public class gnssposget_app
{
    public gnssposget_app_states state;
    socketСonnect socket;
    tgbot_server tgbot;
    bool isRunning = false;
    Collection<string> report = new Collection<string>();
    private bool statusRequested;
    private bool abortRequested;
    private CancellationTokenSource listenerCts;
    public gnssposget_app(socketСonnect socket)
    {
        state = gnssposget_app_states.STATE_INIT;
        this.socket = socket;
        this.tgbot = null;
        this.statusRequested = false;
        this.abortRequested = false;
        report.Clear();
    }

    public void run()
    {
        isRunning = true;
        while (isRunning)
        {
            string receivedMessage = String.Empty;

            // Get message from receive queue
            if (socket.messageQueue.Count > 0)
            {
                receivedMessage = socket.messageQueue.Dequeue();
            }

            if (socket.messageQueue.Contains("ERROR"))
            {
                state = gnssposget_app_states.STATE_UNEXPECTED_ERROR;
            }

            switch(state)
            {
                case gnssposget_app_states.STATE_INIT:
                    // Waiting for user to issue a start command
                    break;
                case gnssposget_app_states.STATE_START_REQUESTED:
                    stateStartRequested(receivedMessage);
                    // Wait for aesd-gnssposget-server to be ready
                    break;
                case gnssposget_app_states.STATE_WORKING:
                    stateWorking(receivedMessage);
                    break;
                case gnssposget_app_states.STATE_ABORT_REQUESTED:
                    Console.WriteLine("Application state = STATE_ABORT_REQUESTED");
                    if (receivedMessage == "ABORTED")
                    {
                        receivedMessage = String.Empty;
                        tgbot.sendMessage("GNSS measurement application aborted successfully.");
                        state = gnssposget_app_states.STATE_DONE;
                    }

                    // Clean up resources and prepare for shutdown
                    state = gnssposget_app_states.STATE_DONE;
                    break;
                case gnssposget_app_states.STATE_FINISHED:
                    Console.WriteLine("Application state = STATE_FINISHED");
                    // Finalize and exit the application
                    state = gnssposget_app_states.STATE_INIT; // Reset state to INIT for next run
                    break;
                case gnssposget_app_states.STATE_DONE:
                    socket.stopReceiving(this.listenerCts);
                    sendFinalReport(); // Send the final report to tgbot
                    state = gnssposget_app_states.STATE_FINISHED; // Move to finished state
                    break;
                case gnssposget_app_states.STATE_ERROR:
                    socket.stopReceiving(this.listenerCts);
                    tgbot.sendMessage("Unexpected response from server: " +
                                          $"{receivedMessage}");
                    Console.WriteLine("Unexpected response from server: " +
                                          $"{receivedMessage}");
                    isRunning = false; // Stop the application
                    break;
                case gnssposget_app_states.STATE_UNEXPECTED_ERROR:
                    socket.stopReceiving(this.listenerCts);
                    Console.WriteLine("Application state = STATE_ERROR");
                    // Something went wrong...
                    tgbot.sendMessage($"An error occurred in the application. " +
                            $"Error message: {receivedMessage.Split('^')[1]}");
                    isRunning = false; // Stop the application
                    break;
                default:
                    socket.stopReceiving(this.listenerCts);
                    Console.WriteLine($"Unknown application state: {state}");
                    tgbot.sendMessage($"An internal error occurred in the application." +
                                      $"Error state: {state}");
                    isRunning = false; // Stop the application
                    break;
            }

            Thread.Sleep(500); // Small delay to prevent busy-waiting
        }

        socket.close(); // Close the socket connection when done
    }

    public void stateInit(tgbot_server tgbot)
    {
        this.tgbot = tgbot; // Store the tgbot instance for later use
        this.listenerCts = socket.startReceiving();
        socket.send("STATE_INIT");
        state = gnssposget_app_states.STATE_START_REQUESTED;
    }

    private void stateStartRequested(string receivedMessage)
    {
        if ((!String.IsNullOrEmpty(receivedMessage)) &&
            (receivedMessage.Contains("STATE_START_REQUESTED")))
        {
            string[] resp = receivedMessage.Split('^');
            // Expected format: STATE_START_REQUESTED^NEW_STATUS^MESSAGE
            if (resp.Length == 3)
            {
                if (resp[1] == "WORKING")
                {
                    tgbot.sendMessage("GNSS position obtained. Time to fly!");
                    Console.WriteLine("Application state -> STATE_WORKING. " +
                                      $"Message: {resp[2]}");

                    // Start waiting for report
                    state = gnssposget_app_states.STATE_WORKING;
                }
                else if (resp[1] == "NO_SIGNAL")
                {
                    tgbot.sendMessage($"Could not get GNSS position after timeout of " +
                                      $"{resp[2].TrimEnd('\n')} seconds");
                    Console.WriteLine("Application state = STATE_DONE. " +
                                      $"Message: {resp[2]}");
                    state = gnssposget_app_states.STATE_DONE;
                }
                else
                {
                    state = gnssposget_app_states.STATE_ERROR;
                }
            }
            else
            {
                state = gnssposget_app_states.STATE_ERROR;
            }
        }
    }

    private void stateWorking(string receivedMessage)
    {
        if ((receivedMessage != String.Empty) &&
            (receivedMessage.Contains("STATE_WORKING")))
        {
            string[] resp = receivedMessage.Split('^');
            // Expected format: STATE_WORKING^NEW_STATUS^MESSAGE
            // e.g. "STATE_WORKING^RUNNING_STATUS^1#3.53" -- 0-30kmph = 3.53s
            // e.g. "STATE_WORKING^RUNNING_STATUS^2#6.21" -- 0-60kmph = 6.21s
            // e.g. "STATE_WORKING^RUNNING_TIMEOUT^2#30"  -- timed out waiting for 2(0-100) after 30 seconds
            // e.g. "STATE_WORKING^RUNNING_ERROR^Signal lost" -- Error: signal lost
            // e.g. "STATE_WORKING^RUNNING_DONE^Finished" -- Measurement done successfully
            if (resp.Length == 3)
            {
                if (resp[1] == "RUNNING_STATUS")
                {
                    report.Add(resp[2]);
                }
                else if (resp[1] == "RUNNING_TIMEOUT")
                {
                    string[] timeoutInfo = resp[2].Split('#');
                    string info = "Timed out while waiting for acceleration";
                    if (timeoutInfo.Length > 1)
                    {
                        info += $" {getAccelerationPoint(timeoutInfo[0])} after " +
                               $"{timeoutInfo[1].TrimEnd('\n')} seconds.";
                    }

                    tgbot.sendMessage(info);
                    Console.WriteLine(info);
                    Console.WriteLine("Application state = STATE_DONE" +
                                      $"Message: {receivedMessage}");
                    state = gnssposget_app_states.STATE_DONE;
                }
                else if (resp[1] == "RUNNING_ERROR")
                {
                    tgbot.sendMessage($"Signal lost during GNSS position measurement.");
                    Console.WriteLine("Application state = STATE_ERROR" +
                                      $"Message: {resp[2]}");
                    state = gnssposget_app_states.STATE_DONE;
                }
                else if (resp[1] == "RUNNING_DONE")
                {
                    tgbot.sendMessage($"GNSS acceleration measurement completed successfully!");
                    Console.WriteLine("Application state = STATE_DONE" +
                                      $"Message: {resp[2]}");
                    state = gnssposget_app_states.STATE_DONE;
                }
                else
                {
                    state = gnssposget_app_states.STATE_ERROR;
                }
            }
            else if ((resp.Length == 2) && statusRequested)
            {
                // GNSS Status received
                // Expected format: STATE_WORKING^Message
                tgbot.sendMessage($"GNSS status: {resp[1]}");
                statusRequested = false;
            }
            else
            {
                state = gnssposget_app_states.STATE_ERROR;
            }
        }
    }

    private string getAccelerationPoint(string pointNum)
    {
        switch (pointNum)
        {
            case "0":
                return "0-30km/h";
            case "1":
                return "0-60km/h";
            case "2":
                return "0-100km/h";
            default:
                return "Unknown point";
        }
    }

    private void sendFinalReport()
    {
        string reportText = "GNSS Acceleration Report:\n";
        if (report.Count == 0)
        {
            reportText = "No GNSS acceleration data collected.";
        }
        else
        {
            for(int i = 0; i < report.Count; i++)
            {
                string[] temp = report[i].Split('#');
                reportText += $"{getAccelerationPoint(temp[0])} --- {temp[1].TrimEnd('\n')} seconds\n";
            }
        }

        tgbot.sendMessage(reportText);
        Console.WriteLine("Sending final report to Telegram bot:\n" +
                          $"{reportText}");
        report.Clear();
    }

    public void requestStatus()
    {
        if (state == gnssposget_app_states.STATE_WORKING)
        {
            statusRequested = true;
            socket.send("REQUEST_STATUS");
        }
    }

    public void requestAbort()
    {
        if ((state == gnssposget_app_states.STATE_WORKING) ||
            (state == gnssposget_app_states.STATE_START_REQUESTED))
        {
            socket.send("REQUEST_ABORT");
            state = gnssposget_app_states.STATE_ABORT_REQUESTED;
        }
    }
}

