namespace PvZFarmBot.Models;

public class Plot
{
    public int Id { get; set; }
    public long UserId { get; set; }
    public int PlotIndex { get; set; }
    public int? PlantId { get; set; }
    public DateTime? PlantedAt { get; set; }
}
