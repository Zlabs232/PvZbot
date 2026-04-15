using PvZFarmBot.Data;
using PvZFarmBot.Models;

namespace PvZFarmBot.Services;

public static class FarmService
{
    private static Plant ReadPlant(Microsoft.Data.Sqlite.SqliteDataReader reader, int offset = 0)
    {
        return new Plant
        {
            Id = reader.GetInt32(offset),
            Name = reader.GetString(offset + 1),
            Emoji = reader.GetString(offset + 2),
            CustomEmojiId = reader.IsDBNull(offset + 3) ? null : reader.GetString(offset + 3),
            GrowTimeMinutes = reader.GetInt32(offset + 4),
            Reward = reader.GetInt32(offset + 5),
            Price = reader.GetInt32(offset + 6)
        };
    }

    public static List<Plant> GetAllPlants()
    {
        using var conn = Database.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Emoji, CustomEmojiId, GrowTimeMinutes, Reward, Price FROM Plants";

        var result = new List<Plant>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            result.Add(ReadPlant(reader));
        return result;
    }

    public static Plant? GetPlant(int plantId)
    {
        using var conn = Database.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Name, Emoji, CustomEmojiId, GrowTimeMinutes, Reward, Price FROM Plants WHERE Id = @id";
        cmd.Parameters.AddWithValue("@id", plantId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return ReadPlant(reader);
    }

    public static List<(Plot Plot, Plant? Plant)> GetUserPlots(long userId)
    {
        using var conn = Database.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.UserId, p.PlotIndex, p.PlantId, p.PlantedAt,
                   pl.Name, pl.Emoji, pl.CustomEmojiId, pl.GrowTimeMinutes, pl.Reward
            FROM Plots p
            LEFT JOIN Plants pl ON p.PlantId = pl.Id
            WHERE p.UserId = @userId
            ORDER BY p.PlotIndex";
        cmd.Parameters.AddWithValue("@userId", userId);

        var result = new List<(Plot, Plant?)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var plot = new Plot
            {
                Id = reader.GetInt32(0),
                UserId = reader.GetInt64(1),
                PlotIndex = reader.GetInt32(2),
                PlantId = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                PlantedAt = reader.IsDBNull(4) ? null : DateTime.Parse(reader.GetString(4))
            };

            Plant? plant = null;
            if (!reader.IsDBNull(5))
            {
                plant = new Plant
                {
                    Name = reader.GetString(5),
                    Emoji = reader.GetString(6),
                    CustomEmojiId = reader.IsDBNull(7) ? null : reader.GetString(7),
                    GrowTimeMinutes = reader.GetInt32(8),
                    Reward = reader.GetInt32(9)
                };
            }

            result.Add((plot, plant));
        }
        return result;
    }

    public static bool PlantSeed(long userId, int plotIndex, int plantId)
    {
        using var conn = Database.GetConnection();

        var checkPlot = conn.CreateCommand();
        checkPlot.CommandText = "SELECT PlantId FROM Plots WHERE UserId = @uid AND PlotIndex = @idx";
        checkPlot.Parameters.AddWithValue("@uid", userId);
        checkPlot.Parameters.AddWithValue("@idx", plotIndex);
        var currentPlant = checkPlot.ExecuteScalar();
        if (currentPlant != null && currentPlant != DBNull.Value) return false;

        var checkInv = conn.CreateCommand();
        checkInv.CommandText = "SELECT Quantity FROM Inventory WHERE UserId = @uid AND PlantId = @pid";
        checkInv.Parameters.AddWithValue("@uid", userId);
        checkInv.Parameters.AddWithValue("@pid", plantId);
        var qty = checkInv.ExecuteScalar();
        if (qty == null || Convert.ToInt32(qty) <= 0) return false;

        var plant = conn.CreateCommand();
        plant.CommandText = @"UPDATE Plots SET PlantId = @pid, PlantedAt = datetime('now')
                              WHERE UserId = @uid AND PlotIndex = @idx";
        plant.Parameters.AddWithValue("@pid", plantId);
        plant.Parameters.AddWithValue("@uid", userId);
        plant.Parameters.AddWithValue("@idx", plotIndex);
        plant.ExecuteNonQuery();

        var inv = conn.CreateCommand();
        inv.CommandText = "UPDATE Inventory SET Quantity = Quantity - 1 WHERE UserId = @uid AND PlantId = @pid";
        inv.Parameters.AddWithValue("@uid", userId);
        inv.Parameters.AddWithValue("@pid", plantId);
        inv.ExecuteNonQuery();

        return true;
    }

    public static (int totalReward, int plantsHarvested) HarvestAll(long userId)
    {
        var plots = GetUserPlots(userId);
        int totalReward = 0;
        int harvested = 0;

        foreach (var (plot, plant) in plots)
        {
            if (plot.PlantId == null || plot.PlantedAt == null || plant == null) continue;

            var elapsed = DateTime.UtcNow - plot.PlantedAt.Value;
            if (elapsed.TotalMinutes < plant.GrowTimeMinutes) continue;

            totalReward += plant.Reward;
            harvested++;

            using var conn = Database.GetConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Plots SET PlantId = NULL, PlantedAt = NULL WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", plot.Id);
            cmd.ExecuteNonQuery();
        }

        if (totalReward > 0)
            UserService.AddBalance(userId, totalReward, $"Урожай: {harvested} растений");

        return (totalReward, harvested);
    }

    public static List<(Plant Plant, int Quantity)> GetInventory(long userId)
    {
        using var conn = Database.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT p.Id, p.Name, p.Emoji, p.CustomEmojiId, p.GrowTimeMinutes, p.Reward, p.Price, i.Quantity
            FROM Inventory i
            JOIN Plants p ON i.PlantId = p.Id
            WHERE i.UserId = @uid AND i.Quantity > 0
            ORDER BY p.Id";
        cmd.Parameters.AddWithValue("@uid", userId);

        var result = new List<(Plant, int)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add((ReadPlant(reader), reader.GetInt32(7)));
        }
        return result;
    }
}
