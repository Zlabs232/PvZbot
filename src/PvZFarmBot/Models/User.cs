namespace PvZFarmBot.Models;

public class User
{
    public long TelegramId { get; set; }
    public string? Username { get; set; }
    public int Balance { get; set; }
    public int PlotsCount { get; set; }
    public DateTime RegisteredAt { get; set; }
    public DateTime? LastDaily { get; set; }
}
