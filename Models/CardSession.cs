namespace CCCDReaderService.Models;

public class CardSession
{
    private bool _isCancelled;

    public string SessionId { get; }
    public DateTime CreatedAt { get; }
    public DateTime ExpiresAt { get; }
    public CitizenCardDto CardData { get; }

    public bool IsActive => !_isCancelled && DateTime.Now < ExpiresAt;

    public CardSession(CitizenCardDto cardData, TimeSpan ttl)
    {
        SessionId = Guid.NewGuid().ToString("N");
        CreatedAt = DateTime.Now;
        ExpiresAt = CreatedAt.Add(ttl);
        CardData = cardData;
    }

    public void Cancel()
    {
        _isCancelled = true;
    }
}
