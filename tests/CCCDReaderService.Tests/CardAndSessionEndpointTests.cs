using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using CCCDReaderService.Controllers;
using CCCDReaderService.Models;
using CCCDReaderService.Services;
using Xunit;

namespace CCCDReaderService.Tests;

public class CardAndSessionEndpointTests
{
    private readonly ISessionManager _sessionManager;
    private readonly IHardwareLock _hardwareLock;
    private readonly CardController _cardController;
    private readonly SessionController _sessionController;

    public CardAndSessionEndpointTests()
    {
        _sessionManager = new SessionManager(defaultTtlSeconds: 120);
        _hardwareLock = new HardwareLock();
        _cardController = new CardController(_sessionManager, _hardwareLock, NullLogger<CardController>.Instance);
        _sessionController = new SessionController(_sessionManager, _hardwareLock, NullLogger<SessionController>.Instance);
    }

    [Fact]
    public void GetCurrentCard_ReturnsNotFound_WhenNoSessionActive()
    {
        // Act: Chưa có phiên nào được tạo
        var actionResult = _cardController.GetCurrentCard();

        // Assert: Phải trả về 404 Not Found
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public void GetCurrentCard_ReturnsCardData_WhenSessionIsActive()
    {
        // Arrange: Tạo một phiên đang hoạt động
        var cardData = new CitizenCardDto
        {
            CardNumber = "001200008888",
            FullName = "LE VAN C",
            DateOfBirth = "15/05/1995"
        };
        var session = _sessionManager.CreateSession(cardData);

        // Act
        var actionResult = _cardController.GetCurrentCard();

        // Assert: Trả về 200 OK với thông tin phiên
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var sessionDto = Assert.IsType<CardSessionResponseDto>(okResult.Value);

        Assert.Equal(session.SessionId, sessionDto.SessionId);
        Assert.Equal("001200008888", sessionDto.CardData.CardNumber);
        Assert.Equal("LE VAN C", sessionDto.CardData.FullName);
    }

    [Fact]
    public void CancelSession_CancelsActiveSession_AndReleasesLocks()
    {
        // Arrange: Có phiên đang hoạt động và đang chiếm lock phần cứng
        var cardData = new CitizenCardDto { CardNumber = "001200007777", FullName = "HOANG VAN D" };
        _sessionManager.CreateSession(cardData);
        _hardwareLock.TryAcquire("CardReader");

        Assert.NotNull(_sessionManager.GetActiveSession());
        Assert.True(_hardwareLock.IsLocked("CardReader"));

        // Act: Gọi API hủy phiên
        var actionResult = _sessionController.CancelSession();

        // Assert: Trả về 200 OK, phiên đã hủy và khóa đã giải phóng
        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Null(_sessionManager.GetActiveSession());
        Assert.False(_hardwareLock.IsLocked("CardReader"));
    }
}
