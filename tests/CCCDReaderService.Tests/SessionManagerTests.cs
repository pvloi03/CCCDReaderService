using CCCDReaderService.Models;
using CCCDReaderService.Services;
using Xunit;

namespace CCCDReaderService.Tests;

public class SessionManagerTests
{
    private readonly ISessionManager _sessionManager;

    public SessionManagerTests()
    {
        // Khởi tạo SessionManager với TTL mặc định 120s
        _sessionManager = new SessionManager(defaultTtlSeconds: 120);
    }

    [Fact]
    public void CreateSession_GeneratesUniqueSessionId_AndStoresCardData()
    {
        // Arrange
        var cardData = new CitizenCardDto
        {
            CardNumber = "001200001234",
            FullName = "NGUYEN VAN A",
            DateOfBirth = "01/01/1990",
            Gender = "Nam",
            Hometown = "Hà Nội",
            PermanentAddress = "Ba Đình, Hà Nội",
            ExpiryDate = "01/01/2030",
            FaceImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="
        };

        // Act
        var session = _sessionManager.CreateSession(cardData);

        // Assert
        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
        Assert.Equal("001200001234", session.CardData.CardNumber);
        Assert.Equal("NGUYEN VAN A", session.CardData.FullName);
        Assert.True(session.IsActive);
        Assert.True(session.ExpiresAt > DateTime.Now);

        var activeSession = _sessionManager.GetActiveSession();
        Assert.NotNull(activeSession);
        Assert.Equal(session.SessionId, activeSession.SessionId);
    }

    [Fact]
    public void GetActiveSession_ReturnsNull_WhenSessionExpires()
    {
        // Arrange: Tạo phiên với TTL ngắn 100ms
        var cardData = new CitizenCardDto
        {
            CardNumber = "001200001234",
            FullName = "NGUYEN VAN A"
        };
        _sessionManager.CreateSession(cardData, ttl: TimeSpan.FromMilliseconds(100));

        // Act: Chờ 150ms để phiên hết hạn
        Thread.Sleep(150);
        var activeSession = _sessionManager.GetActiveSession();

        // Assert: Phiên đã hết hạn phải trả về null
        Assert.Null(activeSession);
    }

    [Fact]
    public void CancelCurrentSession_DeactivatesActiveSession()
    {
        // Arrange
        var cardData = new CitizenCardDto { CardNumber = "001200009999", FullName = "TRAN THI B" };
        var session = _sessionManager.CreateSession(cardData);
        Assert.NotNull(_sessionManager.GetActiveSession());

        // Act
        var cancelled = _sessionManager.CancelCurrentSession();

        // Assert
        Assert.True(cancelled);
        Assert.Null(_sessionManager.GetActiveSession());
        Assert.False(session.IsActive);
    }
}
