using CCCDReaderService.Models;

namespace CCCDReaderService.Services;

public class SessionManager : ISessionManager
{
    private readonly object _lock = new();
    private readonly TimeSpan _defaultTtl;
    private CardSession? _currentSession;

    public SessionManager(int defaultTtlSeconds = 120)
    {
        _defaultTtl = TimeSpan.FromSeconds(Math.Max(10, defaultTtlSeconds));
    }

    public CardSession? CurrentSession
    {
        get
        {
            lock (_lock)
            {
                if (_currentSession != null && !_currentSession.IsActive)
                {
                    _currentSession = null;
                }
                return _currentSession;
            }
        }
    }

    public CardSession CreateSession(CitizenCardDto cardData, TimeSpan? ttl = null)
    {
        ArgumentNullException.ThrowIfNull(cardData);

        lock (_lock)
        {
            var effectiveTtl = ttl ?? _defaultTtl;
            _currentSession = new CardSession(cardData, effectiveTtl);
            return _currentSession;
        }
    }

    public CardSession? GetActiveSession()
    {
        return CurrentSession;
    }

    public bool CancelCurrentSession()
    {
        lock (_lock)
        {
            if (_currentSession == null || !_currentSession.IsActive)
            {
                _currentSession = null;
                return false;
            }

            _currentSession.Cancel();
            _currentSession = null;
            return true;
        }
    }
}
