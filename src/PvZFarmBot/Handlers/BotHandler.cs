using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using PvZFarmBot.Services;

namespace PvZFarmBot.Handlers;

public static class BotHandler
{
    private const ParseMode Html = ParseMode.Html;

    public static async Task HandleUpdate(ITelegramBotClient bot, Update update)
    {
        if (update.Message?.Sticker != null)
        {
            var s = update.Message.Sticker;
            await bot.SendMessage(update.Message.Chat.Id,
                $"📋 <b>Sticker info:</b>\n" +
                $"<code>{s.FileId}</code>\n\n" +
                $"Emoji: {s.Emoji}\n" +
                $"Set: {s.SetName}\n" +
                $"Custom Emoji ID: <code>{s.CustomEmojiId ?? "нет"}</code>",
                parseMode: Html);
            return;
        }

        if (update.Message?.Entities != null)
        {
            var customEmoji = update.Message.Entities
                .FirstOrDefault(e => e.Type == MessageEntityType.CustomEmoji);
            if (customEmoji != null && update.Message.Text != null
                && !update.Message.Text.StartsWith("/"))
            {
                await bot.SendMessage(update.Message.Chat.Id,
                    $"📋 <b>Custom Emoji info:</b>\n\n" +
                    $"ID: <code>{customEmoji.CustomEmojiId}</code>\n" +
                    $"Тест: <tg-emoji emoji-id=\"{customEmoji.CustomEmojiId}\">🌱</tg-emoji>",
                    parseMode: Html);
                return;
            }
        }

        if (update.Message?.Text != null)
            await HandleMessage(bot, update.Message);
        else if (update.CallbackQuery != null)
            await HandleCallback(bot, update.CallbackQuery);
    }

    private static async Task HandleMessage(ITelegramBotClient bot, Message message)
    {
        var chatId = message.Chat.Id;
        var userId = message.From!.Id;
        var username = message.From.Username ?? message.From.FirstName;
        var text = message.Text!.Split(' ')[0].ToLower();

        switch (text)
        {
            case "/start":
                await HandleStart(bot, chatId, userId, username);
                break;
            case "/farm":
                await HandleFarm(bot, chatId, userId);
                break;
            case "/plant":
                await HandlePlantMenu(bot, chatId, userId);
                break;
            case "/harvest":
                await HandleHarvest(bot, chatId, userId);
                break;
            case "/shop":
                await HandleShop(bot, chatId, userId);
                break;
            case "/profile":
                await HandleProfile(bot, chatId, userId);
                break;
            case "/daily":
                await HandleDaily(bot, chatId, userId);
                break;
            case "/top":
                await HandleTop(bot, chatId);
                break;
            case "/help":
                await HandleHelp(bot, chatId);
                break;
            default:
                await bot.SendMessage(chatId, "Неизвестная команда. Используй /help");
                break;
        }
    }

    private static async Task HandleStart(ITelegramBotClient bot, long chatId, long userId, string username)
    {
        bool isNew = UserService.Register(userId, username);

        if (isNew)
        {
            await bot.SendMessage(chatId,
                "🌱 <b>Добро пожаловать в PvZ Farm!</b>\n\n" +
                "Ты получил:\n" +
                "• 💰 100 солнышек\n" +
                "• 🌻 3 семечка Sunflower\n" +
                "• 🟫 3 грядки\n\n" +
                "Начни с /plant чтобы посадить первое растение!\n" +
                "Полный список команд: /help",
                parseMode: Html);
        }
        else
        {
            await bot.SendMessage(chatId, "С возвращением! Используй /farm чтобы проверить ферму.");
        }
    }

    private static async Task HandleFarm(ITelegramBotClient bot, long chatId, long userId)
    {
        var plots = FarmService.GetUserPlots(userId);
        if (plots.Count == 0)
        {
            await bot.SendMessage(chatId, "Сначала зарегистрируйся: /start");
            return;
        }

        var text = "🏡 <b>Твой двор:</b>\n\n";

        foreach (var (plot, plant) in plots)
        {
            text += $"Грядка {plot.PlotIndex + 1}: ";

            if (plant == null || plot.PlantId == null)
            {
                text += "🟫 Пусто\n";
            }
            else
            {
                var elapsed = DateTime.UtcNow - plot.PlantedAt!.Value;
                var remaining = TimeSpan.FromMinutes(plant.GrowTimeMinutes) - elapsed;

                if (remaining <= TimeSpan.Zero)
                {
                    text += $"{plant.GetHtmlEmoji()} {plant.Name} — Готово! (+{plant.Reward}☀️)\n";
                }
                else
                {
                    text += $"{plant.GetHtmlEmoji()} {plant.Name} — ⏳ {FormatTime(remaining)}\n";
                }
            }
        }

        text += "\n/plant — посадить | /harvest — собрать";

        await bot.SendMessage(chatId, text, parseMode: Html);
    }

    private static async Task HandlePlantMenu(ITelegramBotClient bot, long chatId, long userId)
    {
        var inventory = FarmService.GetInventory(userId);
        if (inventory.Count == 0)
        {
            await bot.SendMessage(chatId, "У тебя нет семян! Загляни в /shop");
            return;
        }

        var plots = FarmService.GetUserPlots(userId);
        var emptyPlots = plots.Where(p => p.Plot.PlantId == null).ToList();

        if (emptyPlots.Count == 0)
        {
            await bot.SendMessage(chatId, "Все грядки заняты! Жди урожай или купи ещё в /shop");
            return;
        }

        var text = "🌱 <b>Выбери семена для посадки:</b>\n\n";
        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var (plant, qty) in inventory)
        {
            text += $"{plant.GetHtmlEmoji()} {plant.Name} — {qty} шт. (⏳{plant.GrowTimeMinutes} мин → +{plant.Reward}☀️)\n";
            buttons.Add([InlineKeyboardButton.WithCallbackData(
                $"{plant.Emoji} {plant.Name} ({qty})", $"selectplant_{plant.Id}")]);
        }

        await bot.SendMessage(chatId, text, parseMode: Html,
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    private static async Task HandleHarvest(ITelegramBotClient bot, long chatId, long userId)
    {
        var (reward, count) = FarmService.HarvestAll(userId);

        if (count == 0)
        {
            await bot.SendMessage(chatId, "Ничего не готово к сбору. Проверь /farm");
        }
        else
        {
            await bot.SendMessage(chatId,
                $"🌾 <b>Урожай собран!</b>\n\n" +
                $"Собрано растений: {count}\n" +
                $"Получено: +{reward}☀️",
                parseMode: Html);
        }
    }

    private static async Task HandleShop(ITelegramBotClient bot, long chatId, long userId)
    {
        var user = UserService.GetUser(userId);
        if (user == null) { await bot.SendMessage(chatId, "Сначала /start"); return; }

        var plants = FarmService.GetAllPlants().Where(p => p.Price > 0).ToList();
        var text = $"🏪 <b>Магазин</b>\n💰 Баланс: {user.Balance}☀️\n\n<b>Семена:</b>\n";

        var buttons = new List<List<InlineKeyboardButton>>();

        foreach (var p in plants)
        {
            text += $"{p.GetHtmlEmoji()} {p.Name} — {p.Price}☀️ (⏳{p.GrowTimeMinutes} мин → +{p.Reward}☀️)\n";
            buttons.Add([InlineKeyboardButton.WithCallbackData(
                $"🛒 {p.Name} — {p.Price}☀️", $"buy_{p.Id}")]);
        }

        if (user.PlotsCount < 9)
        {
            int plotPrice = ShopService.GetPlotPrice(user.PlotsCount + 1);
            text += $"\n<b>Грядки:</b>\n🟫 Грядка #{user.PlotsCount + 1} — {plotPrice}☀️\n";
            buttons.Add([InlineKeyboardButton.WithCallbackData(
                $"🟫 Купить грядку — {plotPrice}☀️", "buy_plot")]);
        }

        await bot.SendMessage(chatId, text, parseMode: Html,
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    private static async Task HandleProfile(ITelegramBotClient bot, long chatId, long userId)
    {
        var user = UserService.GetUser(userId);
        if (user == null) { await bot.SendMessage(chatId, "Сначала /start"); return; }

        var plots = FarmService.GetUserPlots(userId);
        var busy = plots.Count(p => p.Plot.PlantId != null);

        await bot.SendMessage(chatId,
            $"👤 <b>Профиль</b>\n\n" +
            $"🏷 Имя: {user.Username ?? "Фермер"}\n" +
            $"💰 Баланс: {user.Balance}☀️\n" +
            $"🟫 Грядки: {busy}/{plots.Count} заняты\n" +
            $"📅 На ферме с: {user.RegisteredAt:dd.MM.yyyy}",
            parseMode: Html);
    }

    private static async Task HandleDaily(ITelegramBotClient bot, long chatId, long userId)
    {
        if (UserService.ClaimDaily(userId))
            await bot.SendMessage(chatId, "🎁 Ежедневный бонус получен: +50☀️!");
        else
            await bot.SendMessage(chatId, "⏰ Ты уже забирал бонус сегодня. Приходи завтра!");
    }

    private static async Task HandleTop(ITelegramBotClient bot, long chatId)
    {
        var top = UserService.GetTopUsers();
        if (top.Count == 0)
        {
            await bot.SendMessage(chatId, "Пока нет фермеров 🤷");
            return;
        }

        var text = "🏆 <b>Топ фермеров:</b>\n\n";
        for (int i = 0; i < top.Count; i++)
        {
            var medal = i switch { 0 => "🥇", 1 => "🥈", 2 => "🥉", _ => $"{i + 1}." };
            text += $"{medal} {top[i].Username} — {top[i].Balance}☀️\n";
        }

        await bot.SendMessage(chatId, text, parseMode: Html);
    }

    private static async Task HandleHelp(ITelegramBotClient bot, long chatId)
    {
        await bot.SendMessage(chatId,
            "🌱 <b>PvZ Farm — Команды:</b>\n\n" +
            "/start — Начать игру\n" +
            "/farm — Посмотреть ферму\n" +
            "/plant — Посадить растение\n" +
            "/harvest — Собрать урожай\n" +
            "/shop — Магазин семян и грядок\n" +
            "/profile — Твой профиль\n" +
            "/daily — Ежедневный бонус\n" +
            "/top — Таблица лидеров\n" +
            "/help — Эта справка",
            parseMode: Html);
    }

    private static async Task HandleCallback(ITelegramBotClient bot, CallbackQuery callback)
    {
        var chatId = callback.Message!.Chat.Id;
        var userId = callback.From.Id;
        var data = callback.Data!;

        if (data.StartsWith("selectplant_"))
        {
            int plantId = int.Parse(data.Replace("selectplant_", ""));
            await ShowPlotSelection(bot, chatId, userId, plantId);
        }
        else if (data.StartsWith("plant_"))
        {
            var parts = data.Replace("plant_", "").Split('_');
            int plantId = int.Parse(parts[0]);
            int plotIndex = int.Parse(parts[1]);

            if (FarmService.PlantSeed(userId, plotIndex, plantId))
            {
                var plant = FarmService.GetPlant(plantId);
                await bot.SendMessage(chatId,
                    $"{plant!.GetHtmlEmoji()} {plant.Name} посажен на грядку {plotIndex + 1}!\n" +
                    $"Готов через {plant.GrowTimeMinutes} мин.",
                    parseMode: Html);
            }
            else
            {
                await bot.SendMessage(chatId, "Не удалось посадить. Грядка занята или нет семян.");
            }
        }
        else if (data.StartsWith("buy_"))
        {
            if (data == "buy_plot")
            {
                if (ShopService.BuyPlot(userId))
                {
                    var user = UserService.GetUser(userId);
                    await bot.SendMessage(chatId, $"Куплена грядка #{user!.PlotsCount}! Баланс: {user.Balance}☀️");
                }
                else
                {
                    await bot.SendMessage(chatId, "Не хватает солнышек или максимум грядок (9).");
                }
            }
            else
            {
                int plantId = int.Parse(data.Replace("buy_", ""));
                if (ShopService.BuySeeds(userId, plantId))
                {
                    var plant = FarmService.GetPlant(plantId);
                    var user = UserService.GetUser(userId);
                    await bot.SendMessage(chatId,
                        $"Куплено: {plant!.GetHtmlEmoji()} {plant.Name} x1\n💰 Баланс: {user!.Balance}☀️",
                        parseMode: Html);
                }
                else
                {
                    await bot.SendMessage(chatId, "Не хватает солнышек!");
                }
            }
        }

        await bot.AnswerCallbackQuery(callback.Id);
    }

    private static async Task ShowPlotSelection(ITelegramBotClient bot, long chatId, long userId, int plantId)
    {
        var plots = FarmService.GetUserPlots(userId);
        var emptyPlots = plots.Where(p => p.Plot.PlantId == null).ToList();

        if (emptyPlots.Count == 0)
        {
            await bot.SendMessage(chatId, "Все грядки заняты!");
            return;
        }

        var buttons = new List<List<InlineKeyboardButton>>();
        foreach (var (plot, _) in emptyPlots)
        {
            buttons.Add([InlineKeyboardButton.WithCallbackData(
                $"🟫 Грядка {plot.PlotIndex + 1}", $"plant_{plantId}_{plot.PlotIndex}")]);
        }

        await bot.SendMessage(chatId, "Выбери грядку для посадки:",
            replyMarkup: new InlineKeyboardMarkup(buttons));
    }

    private static string FormatTime(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}ч {ts.Minutes}мин";
        return $"{ts.Minutes}мин {ts.Seconds}сек";
    }
}
