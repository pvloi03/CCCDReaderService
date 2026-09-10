namespace CCCDReaderService.Models;

public class CardSessionResponseDto
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public CitizenCardDto CardData { get; set; } = new();
}
