using Microsoft.Data.Sqlite;

namespace PvZFarmBot.Data;

public static class Database
{
    private const string ConnectionString = "Data Source=pvzfarm.db";

    public static SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        return connection;
    }

    public static void Initialize()
    {
        using var connection = GetConnection();

        var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Users (
                TelegramId INTEGER PRIMARY KEY,
                Username TEXT,
                Balance INTEGER NOT NULL DEFAULT 100,
                PlotsCount INTEGER NOT NULL DEFAULT 3,
                RegisteredAt TEXT NOT NULL DEFAULT (datetime('now')),
                LastDaily TEXT
            );

            CREATE TABLE IF NOT EXISTS Plants (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Emoji TEXT NOT NULL,
                CustomEmojiId TEXT,
                GrowTimeMinutes INTEGER NOT NULL,
                Reward INTEGER NOT NULL,
                Price INTEGER NOT NULL
            );

            CREATE TABLE IF NOT EXISTS Inventory (
                UserId INTEGER NOT NULL,
                PlantId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL DEFAULT 0,
                PRIMARY KEY (UserId, PlantId),
                FOREIGN KEY (UserId) REFERENCES Users(TelegramId),
                FOREIGN KEY (PlantId) REFERENCES Plants(Id)
            );

            CREATE TABLE IF NOT EXISTS Plots (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                PlotIndex INTEGER NOT NULL,
                PlantId INTEGER,
                PlantedAt TEXT,
                FOREIGN KEY (UserId) REFERENCES Users(TelegramId),
                FOREIGN KEY (PlantId) REFERENCES Plants(Id)
            );

            CREATE TABLE IF NOT EXISTS Transactions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Type TEXT NOT NULL,
                Amount INTEGER NOT NULL,
                Description TEXT,
                CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (UserId) REFERENCES Users(TelegramId)
            );
        ";
        cmd.ExecuteNonQuery();

        SeedPlants(connection);
    }

    private static void SeedPlants(SqliteConnection connection)
    {
        var check = connection.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM Plants";
        if ((long)check.ExecuteScalar()! > 0) return;

        //                  Name          Emoji  CustomEmojiId                GrowMin  Reward  Price
        var plants = new[]
        {
            ("Sunflower",   "🌻", "5974278416151615842",  1,   10,  0),
            ("Peashooter",  "🟢", "5974298456469018685",  5,   30,  50),
            ("Wall-nut",    "🥜", "5974318574095834333",  15,  80,  150),
            ("Snow Pea",    "❄️",  "5974571788187736601",  30,  200, 400),
            ("Chomper",     "🪴", "5963211389236418101",  60,  500, 1000),
            ("Jalapeno",    "🌶️", "5976424460985572032",  180, 1500, 3000),
            ("Doom-shroom", "🍄", (string?)null,          360, 4000, 8000),
        };

        foreach (var (name, emoji, customEmojiId, time, reward, price) in plants)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = @"INSERT INTO Plants (Name, Emoji, CustomEmojiId, GrowTimeMinutes, Reward, Price)
                                VALUES (@name, @emoji, @customEmojiId, @time, @reward, @price)";
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@emoji", emoji);
            cmd.Parameters.AddWithValue("@customEmojiId", customEmojiId ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@time", time);
            cmd.Parameters.AddWithValue("@reward", reward);
            cmd.Parameters.AddWithValue("@price", price);
            cmd.ExecuteNonQuery();
        }
    }
}
