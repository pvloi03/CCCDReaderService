using CCCDReaderService.Models;

namespace CCCDReaderService.Services;

public interface ISessionManager
{
    CardSession? CurrentSession { get; }
    CardSession CreateSession(CitizenCardDto cardData, TimeSpan? ttl = null);
    CardSession? GetActiveSession();
    bool CancelCurrentSession();
}
