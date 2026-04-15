using PvZFarmBot.Data;

namespace PvZFarmBot.Services;

public static class ShopService
{
    public static bool BuySeeds(long userId, int plantId, int quantity = 1)
    {
        var plant = FarmService.GetPlant(plantId);
        if (plant == null || plant.Price == 0) return false;

        var user = UserService.GetUser(userId);
        if (user == null) return false;

        int totalCost = plant.Price * quantity;
        if (user.Balance < totalCost) return false;

        UserService.AddBalance(userId, -totalCost, $"Покупка: {plant.Name} x{quantity}");

        using var conn = Database.GetConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Inventory (UserId, PlantId, Quantity) VALUES (@uid, @pid, @qty)
            ON CONFLICT(UserId, PlantId) DO UPDATE SET Quantity = Quantity + @qty";
        cmd.Parameters.AddWithValue("@uid", userId);
        cmd.Parameters.AddWithValue("@pid", plantId);
        cmd.Parameters.AddWithValue("@qty", quantity);
        cmd.ExecuteNonQuery();

        return true;
    }

    public static bool BuyPlot(long userId)
    {
        var user = UserService.GetUser(userId);
        if (user == null || user.PlotsCount >= 9) return false;

        int cost = GetPlotPrice(user.PlotsCount + 1);
        if (user.Balance < cost) return false;

        UserService.AddBalance(userId, -cost, $"Покупка грядки #{user.PlotsCount + 1}");

        using var conn = Database.GetConnection();

        var updateUser = conn.CreateCommand();
        updateUser.CommandText = "UPDATE Users SET PlotsCount = PlotsCount + 1 WHERE TelegramId = @id";
        updateUser.Parameters.AddWithValue("@id", userId);
        updateUser.ExecuteNonQuery();

        var addPlot = conn.CreateCommand();
        addPlot.CommandText = "INSERT INTO Plots (UserId, PlotIndex) VALUES (@id, @idx)";
        addPlot.Parameters.AddWithValue("@id", userId);
        addPlot.Parameters.AddWithValue("@idx", user.PlotsCount);
        addPlot.ExecuteNonQuery();

        return true;
    }

    public static int GetPlotPrice(int plotNumber) => plotNumber switch
    {
        4 => 200,
        5 => 500,
        6 => 1000,
        7 => 2000,
        8 => 5000,
        9 => 10000,
        _ => 0
    };
}
