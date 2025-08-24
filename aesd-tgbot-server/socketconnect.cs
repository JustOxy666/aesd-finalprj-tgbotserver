using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using TL.Methods;


public class socketСonnect
{
    Socket socket;
    private bool receiving;
    public Queue<string> messageQueue;

    public socketСonnect(string ip, int port)
    {
        int connectAttempts = 30;
        int timeoutSeconds = 10;
        receiving = false;
        IPAddress address;
        // Initialize the socket connectionIPAddress address;
        if (ip == "localhost")
        {
            address = IPAddress.Loopback;
        }
        else
        {
            address = IPAddress.Parse(ip);
        }
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        
        // Create a TCP socket
        IPEndPoint endPoint = new IPEndPoint(address, port);
        while (connectAttempts > 0)
        {
            try
            {
                socket.Connect(endPoint);
                Console.WriteLine($"Socket bound to {endPoint}");
                break;
            }
            catch (Exception e)
            {
                Console.WriteLine($"Socket bind failed: {e.Message}");
                Console.WriteLine($"Waiting {timeoutSeconds} secods before new connect attempt...");
                connectAttempts--;
                Thread.Sleep(timeoutSeconds * 1000);
            }
        }

        this.socket = socket;
        this.receiving = false;
        messageQueue = new Queue<string>();
    }

    private async Task receiveAsync(CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[512];
        string text = String.Empty;

        int bytesReceived = await this.socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

        if (bytesReceived == 0)
        {
            // remote closed connection
            Console.WriteLine("Connection closed by server.");
            text = "ERROR^connection closed by server";

            Console.WriteLine($"Adding to Queue: {text}");
            messageQueue.Enqueue(text);
            return;
        }

        foreach(string str in Encoding.UTF8.GetString(buffer, 0, bytesReceived).Split('\n'))
        {
            if (str.Length > 0)
            {
                text += str + "\n"; // Append each line to the text
                Console.WriteLine($"Adding to Queue: {text}");
                messageQueue.Enqueue(text);
                text = string.Empty; // Reset text for the next line
            }
        }

        return;
    }

    public CancellationTokenSource startReceiving()
    {
        // Starts listening for incoming messages
        // Listens until CancellationToken is invoked using stopReceiving()

        if (this.receiving)
        {
            Console.WriteLine("Already receiving messages.");
            return null;
        }

        this.receiving = true;
        var cts = new CancellationTokenSource();

        {
            string rec = string.Empty;
            _ = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    await receiveAsync(cts.Token);
                }
            });
        }

        return cts;
    }

    public bool close()
    {
        bool result = true;
        try
        {
            this.socket.Shutdown(SocketShutdown.Both);
            this.socket.Close();
            Console.WriteLine("Socket closed successfully.");
        }
        catch (Exception e)
        {
            Console.WriteLine($"Failed to close socket: {e.Message}");
            result = false;
        }
        return result;
    }

    public bool send(string message)
    {
        bool result = true;

        message += "\n"; // Ensure the message ends with a newline character
        byte[] msg = Encoding.UTF8.GetBytes(message);
        if (this.socket.Send(msg) > 0)
        {
            Console.WriteLine($"Sent message: {message}");
        }
        else
        {
            Console.WriteLine("Failed to send message.");
            result = false;
        }

        return result;
    }

    public void stopReceiving(CancellationTokenSource cts)
    {
        if (this.receiving)
        {
            this.receiving = false;
            cts.Cancel();
            Console.WriteLine("Stopping message receiving.");
        }
        else
        {
            Console.WriteLine("Not currently receiving messages.");
        }
    }
}

