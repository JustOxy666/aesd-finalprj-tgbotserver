// ----------------------------------------------------------------------------------------------
// This example demonstrates a lot of things you cannot normally do with Telegram.Bot / Bot API
// ----------------------------------------------------------------------------------------------
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

string token = string.Empty;
int apiId = 0;
string apiHash = string.Empty;

if (args.Length != 3)
{
	Console.WriteLine("Usage: WTelegramBotClient.exe [token] [app ID] [hash]");
    Environment.Exit(1);
}
else
{
	token = args[0];
	apiId = int.Parse(args[1]);
	apiHash = args[2];

    Console.WriteLine("Token: " + token);
	Console.WriteLine("API ID: " + apiId);
	Console.WriteLine("API Hash: " + apiHash);
}

StreamWriter WTelegramLogs = new StreamWriter("WTelegramBot.log", true, Encoding.UTF8) { AutoFlush = true };
WTelegram.Helpers.Log = (lvl, str) => WTelegramLogs.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{"TDIWE!"[lvl]}] {str}");

// Using SQLite DB for storage. Other DBs below (remember to add/uncomment the adequate PackageReference in .csproj)
using var connection = new Microsoft.Data.Sqlite.SqliteConnection(@"Data Source=WTelegramBot.sqlite");
//SQL Server:	using var connection = new Microsoft.Data.SqlClient.SqlConnection(@"Data Source=(LocalDB)\MSSQLLocalDB;AttachDbFilename=PATH_TO.mdf;Integrated Security=True;Connect Timeout=60");
//MySQL:    	using var connection = new MySql.Data.MySqlClient.MySqlConnection(@"Data Source=...");
//PosgreSQL:	using var connection = new Npgsql.NpgsqlConnection(@"Data Source=...");

using var bot = new WTelegram.Bot(token, apiId, apiHash, connection);

var me = await bot.GetMe();
Console.WriteLine($"Hello, World! I am user {me.Id} and my name is {me.FirstName}.");

//---------------------------------------------------------------------------------------
Console.WriteLine("___________________________________________________\n");
Console.WriteLine("I'm listening now. Send me a command in private or in a group where I am... Or press Escape to exit");
await bot.DropPendingUpdates();
bot.WantUnknownTLUpdates = true;
bot.OnError += (e, s) => Console.Error.WriteLineAsync(e.ToString());
bot.OnMessage += OnMessage;
bot.OnUpdate += OnUpdate;
while (Console.ReadKey(true).Key != ConsoleKey.Escape) { }
Console.WriteLine("Exiting...");


async Task OnMessage(WTelegram.Types.Message msg, UpdateType type)
{
    if (msg.Text == null) return;
    var text = msg.Text.ToLower();
    // commands accepted:
    if (text == "/start")
    {
        //---> It's easy to reply to a message by giving its id to replyParameters: (was broken in Telegram.Bot v20.0.0)
        await bot.SendMessage(msg.Chat, $"Hello, {msg.From}!\nTry commands /pic /react /lastseen /getchat /setphoto", replyParameters: msg);
    }
    else if (text == "/pic")
    {
        //---> It's easy to send a file by id or by url by just passing the string: (was broken in Telegram.Bot v19.0.0)
        await bot.SendPhoto(msg.Chat, "https://picsum.photos/310/200.jpg"); // easily send file by URL or FileID
    }
    else if (text == "/react")
    {
        //---> It's easy to send reaction emojis by just giving the emoji string or id
        await bot.SetMessageReaction(msg.Chat, msg.MessageId, ["👍"]);
    }
    else if (text == "/lastseen")
    {
        //---> Show more user info that is normally not accessible in Bot API:
        var tlUser = msg.From?.TLUser();
        await bot.SendMessage(msg.Chat, $"Your last seen is: {tlUser?.status?.ToString()?[13..]}");
    }
    else if (text == "/getchat")
    {
        var chat = await bot.GetChat(msg.Chat);
        //---> Demonstrate how to serialize structure to Json, and post it in <pre> code
        var dump = System.Text.Json.JsonSerializer.Serialize(chat, JsonBotAPI.Options);
        dump = $"<pre>{TL.HtmlText.Escape(dump)}</pre>";
        await bot.SendMessage(msg.Chat, dump, parseMode: ParseMode.Html);
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
