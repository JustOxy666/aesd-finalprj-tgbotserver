using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System;
using System.Net;
using System.Net.Sockets;

string token = string.Empty;
int apiId = 0;
string apiHash = string.Empty;

string ipAddress = "localhost"; // Change this to your desired IP address
int socketPort = 9000; // Change this to your desired port



bool parseArgs(string[] args)
{
    if (args.Length != 5)
    {
        Console.WriteLine("Usage: WTelegramBotClient [token] [app ID] [hash] [socket IP] [socket port]");
        return false;
    }
    else
    {
        token = args[0];
        apiId = int.Parse(args[1]);
        apiHash = args[2];
        ipAddress = args[3];
        socketPort = int.Parse(args[4]);

        Console.WriteLine("Token: " + token);
        Console.WriteLine("API ID: " + apiId);
        Console.WriteLine("API Hash: " + apiHash);
        Console.WriteLine("Socket IP: " + ipAddress);
        Console.WriteLine("Socket Port: " + socketPort);
        return true;
    }
}


/*******************************/
/******       MAIN     *********/
/*******************************/


if (!parseArgs(args))
{
    Environment.Exit(1);
}

var socket = new socketСonnect(ipAddress, socketPort);
var gnssapp = new gnssposget_app(socket);
var tgbot = new tgbot_server(token, apiId, apiHash, gnssapp);
await tgbot.Init();
gnssapp.run();
