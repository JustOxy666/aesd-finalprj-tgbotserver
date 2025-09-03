using System;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;


public class tgbot_server
{
    WTelegram.Bot bot;
    private gnssposget_app gnssapp;
    WTelegram.Types.Message messageHandle;

    private const string cmdStart = "/start";
    private const string cmdAbort = "/abort";
    private const string cmdStatus = "/status";
    private const string cmdHelp = "/help";
    private const string helpMessage = "Hello! This application can measure acceleration using GNSS data!\n\n" +
                              "Available commands:\n" +
                             $"Send \"{cmdStart}\" in this chat to start GNSS measurement application.\n" +
                             $"Send \"{cmdStatus}\" to get GNSS signal status while application is running.\n" +
                             $"Send \"{cmdHelp}\" to see this message.";
    public tgbot_server(string token, int apiId, string apiHash, gnssposget_app gnssapp)
    {
        using var connection = new Microsoft.Data.Sqlite.SqliteConnection(@"Data Source=WTelegramBot.sqlite");
        this.bot = new WTelegram.Bot(token, apiId, apiHash, connection);
        this.gnssapp = gnssapp;
        this.messageHandle = null;
    }

    public async Task Init()
    {
        var me = await bot.GetMe();
        Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");
        await bot.DropPendingUpdates();
        bot.WantUnknownTLUpdates = true;
        bot.OnError += (e, s) => Console.Error.WriteLineAsync(e.ToString());
        bot.OnMessage += OnMessage;
        bot.OnUpdate += OnUpdate;
    }

    async Task OnMessage(WTelegram.Types.Message msg, UpdateType type)
    {
        if (msg.Text == null) return;
        var text = msg.Text.ToLower();
        this.messageHandle = msg; // Store the message for later use
        // commands accepted:
        if (text == cmdStart)
        {
            Console.WriteLine("Received /start command");
            switch (gnssapp.state)
            {
                case gnssposget_app_states.STATE_INIT:
                    Console.WriteLine("Received request to start GNSS measurement");
                    await this.bot.SendMessage(msg.Chat, "Starting GNSS measurement application. Please wait for GNSS positon...", replyParameters: msg);
                    gnssapp.stateInit(this);
                    break;
                default:
                    await this.bot.SendMessage(msg.Chat, "GNSS measurement application has already started. Please use /abort to aborn it.", replyParameters: msg);
                    Console.WriteLine($"Unexpected application state {Enum.GetName(gnssapp.state)}");
                    break;
            }
        }
        else if (text == cmdHelp)
        {
            this.sendMessage(helpMessage);
        }
        else if (text == cmdAbort)
        {
            Console.WriteLine("Received /abort command");
            switch (gnssapp.state)
            {
                case gnssposget_app_states.STATE_INIT:
                    await this.bot.SendMessage(msg.Chat, $"GNSS measurement application is not running. Please use \"{cmdStart}\" to start it.", replyParameters: msg);
                    Console.WriteLine($"Unexpected application state {Enum.GetName(gnssapp.state)}");
                    break;
                case gnssposget_app_states.STATE_START_REQUESTED:
                case gnssposget_app_states.STATE_WORKING:
                    this.sendMessage("Aborting GNSS measurement application. Please wait...");
                    gnssapp.requestAbort();
                    break;
                default:
                    this.sendMessage("GNSS measurement application has already finished. Please use /start to start it again.");
                    Console.WriteLine($"Unexpected application state {Enum.GetName(gnssapp.state)}");
                    break;
            }
        }
        else if (text == cmdStatus)
        {
            //---> Get GNSS signal status from the app and send it to the user
            if (gnssapp.state == gnssposget_app_states.STATE_WORKING)
            {
                Console.WriteLine("Received /status command");
                gnssapp.requestStatus();
            }   
            else
            {
                this.sendMessage("GNSS measurement is not running yet.");
                Console.WriteLine($"Attempt to get status while state is not STATE_WORKING");
            }
        }
        else
        {
            Console.WriteLine($"Unexpected command received from Telegram: {text}");
            this.sendMessage($"Unrecognized command: {text}");
            this.sendMessage(helpMessage);
        }
    }

    Task OnUpdate(WTelegram.Types.Update update)
    {
        if (update.Type == UpdateType.Unknown)
        {
            //---> Show some update types that are unsupported by Bot API but can be handled via TLUpdate
            if (update.TLUpdate is TL.UpdateDeleteChannelMessages udcm)
                Console.WriteLine($"{udcm.messages.Length} message(s) deleted in {bot.Chat(udcm.channel_id)?.Title}");
            else if (update.TLUpdate is TL.UpdateDeleteMessages udm)
                Console.WriteLine($"{udm.messages.Length} message(s) deleted in user chat or small private group");
            else if (update.TLUpdate is TL.UpdateReadChannelOutbox urco)
                Console.WriteLine($"Someone read {bot.Chat(urco.channel_id)?.Title} up to message {urco.max_id}");
        }
        return Task.CompletedTask;
    }

    public async void sendMessage(string message)
    {
        await this.bot.SendMessage(this.messageHandle.Chat, message);
    }

}
