using PvZFarmBot.Data;
using PvZFarmBot.Models;

namespace PvZFarmBot.Services;

public static class UserService
{
    public static bool Register(long telegramId, string? username)
    {
        using var conn = Database.GetConnection();

        var check = conn.CreateCommand();
        check.CommandText = "SELECT COUNT(*) FROM Users WHERE TelegramId = @id";
        check.Parameters.AddWithValue("@id", telegramId);
        if ((long)check.ExecuteScalar()! > 0) return false;

        var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO Users (TelegramId, Username) VALUES (@id, @username)";
        cmd.Parameters.AddWithValue("@id", telegramId);
        cmd.Parameters.AddWithValue("@username", username ?? (object)DBNull.Value);
        cmd.ExecuteNonQuery();

        var seed = conn.CreateCommand();
        seed.CommandText = "INSERT INTO Inventory (UserId, PlantId, Quantity) VALUES (@id, 1, 3)";
        seed.Parameters.AddWithValue("@id", telegramId);
        seed.ExecuteNonQuery();

        for (int i = 0; i < 3; i++)
        {
            var plot = conn.CreateCommand();
            plot.CommandText = "INSERT INTO Plots (UserId, PlotIndex) VALUES (@id, @idx)";
            plot.Parameters.AddWithValue("@id", telegramId);
            plot.Parameters.AddWithValue("@idx", i);
            plot.ExecuteNonQuery();
        }

        return true;
    }

    public static User? GetUser(long telegramId)
    {
        using var conn = Database.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TelegramId, Username, Balance, PlotsCount, RegisteredAt, LastDaily FROM Users WHERE TelegramId = @id";
        cmd.Parameters.AddWithValue("@id", telegramId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        return new User
        {
            TelegramId = reader.GetInt64(0),
            Username = reader.IsDBNull(1) ? null : reader.GetString(1),
            Balance = reader.GetInt32(2),
            PlotsCount = reader.GetInt32(3),
            RegisteredAt = DateTime.Parse(reader.GetString(4)),
            LastDaily = reader.IsDBNull(5) ? null : DateTime.Parse(reader.GetString(5))
        };
    }

    public static void AddBalance(long telegramId, int amount, string description)
    {
        using var conn = Database.GetConnection();

        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET Balance = Balance + @amount WHERE TelegramId = @id";
        cmd.Parameters.AddWithValue("@amount", amount);
        cmd.Parameters.AddWithValue("@id", telegramId);
        cmd.ExecuteNonQuery();

        var tx = conn.CreateCommand();
        tx.CommandText = @"INSERT INTO Transactions (UserId, Type, Amount, Description)
                           VALUES (@id, @type, @amount, @desc)";
        tx.Parameters.AddWithValue("@id", telegramId);
        tx.Parameters.AddWithValue("@type", amount >= 0 ? "income" : "expense");
        tx.Parameters.AddWithValue("@amount", amount);
        tx.Parameters.AddWithValue("@desc", description);
        tx.ExecuteNonQuery();
    }

    public static bool ClaimDaily(long telegramId)
    {
        var user = GetUser(telegramId);
        if (user == null) return false;

        if (user.LastDaily.HasValue && (DateTime.UtcNow - user.LastDaily.Value).TotalHours < 24)
            return false;

        using var conn = Database.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE Users SET LastDaily = datetime('now'), Balance = Balance + 50 WHERE TelegramId = @id";
        cmd.Parameters.AddWithValue("@id", telegramId);
        cmd.ExecuteNonQuery();

        var tx = conn.CreateCommand();
        tx.CommandText = @"INSERT INTO Transactions (UserId, Type, Amount, Description)
                           VALUES (@id, 'income', 50, 'Ежедневный бонус')";
        tx.Parameters.AddWithValue("@id", telegramId);
        tx.ExecuteNonQuery();

        return true;
    }

    public static List<(string Username, int Balance)> GetTopUsers(int limit = 10)
    {
        using var conn = Database.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Username, Balance FROM Users ORDER BY Balance DESC LIMIT @limit";
        cmd.Parameters.AddWithValue("@limit", limit);

        var result = new List<(string, int)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((reader.IsDBNull(0) ? "Фермер" : reader.GetString(0), reader.GetInt32(1)));
        }
        return result;
    }
}
