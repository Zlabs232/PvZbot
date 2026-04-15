namespace PvZFarmBot.Models;

public class Plant
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Emoji { get; set; } = "";
    public string? CustomEmojiId { get; set; }
    public int GrowTimeMinutes { get; set; }
    public int Reward { get; set; }
    public int Price { get; set; }

    public string GetHtmlEmoji()
    {
        if (CustomEmojiId != null)
            return $"<tg-emoji emoji-id=\"{CustomEmojiId}\">{Emoji}</tg-emoji>";
        return Emoji;
    }
}
