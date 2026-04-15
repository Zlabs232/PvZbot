using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using PvZFarmBot.Data;
using PvZFarmBot.Handlers;

// Load token
var configPath = File.Exists("appsettings.Local.json")
    ? "appsettings.Local.json"
    : "appsettings.json";

var config = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(configPath))!;
var token = config["BotToken"];

// Init database
Database.Initialize();

// Start bot
var bot = new TelegramBotClient(token);
var me = await bot.GetMe();
Console.WriteLine($"Бот @{me.Username} запущен!");

using var cts = new CancellationTokenSource();

bot.StartReceiving(
    updateHandler: async (client, update, ct) =>
    {
        try
        {
            await BotHandler.HandleUpdate(client, update);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Ошибка: {ex.Message}");
        }
    },
    errorHandler: (client, ex, ct) =>
    {
        Console.WriteLine($"Polling error: {ex.Message}");
        return Task.CompletedTask;
    },
    receiverOptions: new ReceiverOptions
    {
        AllowedUpdates = []
    },
    cancellationToken: cts.Token
);

Console.WriteLine("Нажми Enter для остановки...");
Console.ReadLine();
cts.Cancel();
