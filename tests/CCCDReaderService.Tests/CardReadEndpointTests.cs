using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using CCCDReaderService.Controllers;
using CCCDReaderService.Exceptions;
using CCCDReaderService.Models;
using CCCDReaderService.Services;
using Xunit;

namespace CCCDReaderService.Tests;

public class CardReadEndpointTests
{
    private readonly ISessionManager _sessionManager;
    private readonly IHardwareLock _hardwareLock;

    public CardReadEndpointTests()
    {
        _sessionManager = new SessionManager(defaultTtlSeconds: 120);
        _hardwareLock = new HardwareLock();
    }

    [Fact]
    public async Task ReadCard_Returns503_WhenDeviceNotConnected()
    {
        // Arrange: Mock đầu đọc báo chưa kết nối
        var stubReader = new StubCardReaderService(isConnected: false, hasCard: false);
        var controller = new CardController(_sessionManager, _hardwareLock, NullLogger<CardController>.Instance, stubReader);

        // Act
        var actionResult = await controller.ReadCard(CancellationToken.None);

        // Assert: 503 Service Unavailable
        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(503, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReadCard_Returns409_WhenHardwareIsLocked()
    {
        // Arrange: Đầu đọc đang bị chiếm bởi tác vụ khác
        var stubReader = new StubCardReaderService(isConnected: true, hasCard: true);
        var controller = new CardController(_sessionManager, _hardwareLock, NullLogger<CardController>.Instance, stubReader);
        _hardwareLock.TryAcquire("CardReader");

        // Act
        var actionResult = await controller.ReadCard(CancellationToken.None);

        // Assert: 409 Conflict
        var objectResult = Assert.IsType<ObjectResult>(actionResult.Result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    [Fact]
    public async Task ReadCard_Returns404_WhenNoCardInReader()
    {
        // Arrange: Đầu đọc cắm nhưng không có thẻ trong khe
        var stubReader = new StubCardReaderService(isConnected: true, hasCard: false);
        stubReader.ThrowOnRead = new CardNotFoundException();
        var controller = new CardController(_sessionManager, _hardwareLock, NullLogger<CardController>.Instance, stubReader);

        // Act
        var actionResult = await controller.ReadCard(CancellationToken.None);

        // Assert: 404 Not Found
        Assert.IsType<NotFoundObjectResult>(actionResult.Result);
    }

    [Fact]
    public async Task ReadCard_Returns200_AndCreatesSession_WhenReadSucceeds()
    {
        // Arrange: Đầu đọc cắm và đọc thành công dữ liệu thẻ
        var expectedCard = new CitizenCardDto
        {
            CardNumber = "001200005555",
            FullName = "VU THI E",
            DateOfBirth = "20/10/1998",
            FaceImageBase64 = "base64image..."
        };
        var stubReader = new StubCardReaderService(isConnected: true, hasCard: true, cardToReturn: expectedCard);
        var controller = new CardController(_sessionManager, _hardwareLock, NullLogger<CardController>.Instance, stubReader);

        // Act
        var actionResult = await controller.ReadCard(CancellationToken.None);

        // Assert: 200 OK
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var sessionDto = Assert.IsType<CardSessionResponseDto>(okResult.Value);

        Assert.Equal("001200005555", sessionDto.CardData.CardNumber);
        Assert.Equal("VU THI E", sessionDto.CardData.FullName);
        Assert.False(string.IsNullOrWhiteSpace(sessionDto.SessionId));

        // Phiên làm việc phải được tự động kích hoạt trong SessionManager
        var activeSession = _sessionManager.GetActiveSession();
        Assert.NotNull(activeSession);
        Assert.Equal(sessionDto.SessionId, activeSession.SessionId);
    }

    private sealed class StubCardReaderService : ICardReaderService
    {
        private readonly bool _isConnected;
        private readonly bool _hasCard;
        private readonly CitizenCardDto? _cardToReturn;

        public Exception? ThrowOnRead { get; set; }

        public bool IsDeviceConnected => _isConnected;
        public bool IsDeviceCameraConnected => true;
        public bool HasCardInReader => _hasCard;
        public byte[]? LastFrameBytes => null;

#pragma warning disable CS0067
        public event EventHandler<EventArgs>? CardInserted;
        public event EventHandler<EventArgs>? CardRemoved;
        public event EventHandler<string>? DeviceStatusChanged;
        public event EventHandler<byte[]>? VideoFrameReceived;
#pragma warning restore CS0067

        public StubCardReaderService(bool isConnected, bool hasCard, CitizenCardDto? cardToReturn = null)
        {
            _isConnected = isConnected;
            _hasCard = hasCard;
            _cardToReturn = cardToReturn;
        }

        public Task<CitizenCardDto> ReadCardAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnRead != null) throw ThrowOnRead;
            if (_cardToReturn != null) return Task.FromResult(_cardToReturn);

            return Task.FromResult(new CitizenCardDto
            {
                CardNumber = "001200000001",
                FullName = "TEST CITIZEN"
            });
        }

        public Task<byte[]> CaptureFaceFromDeviceAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new byte[] { 1, 2, 3 });
        }

        public int CompareFace(byte[] chipFaceBytes, byte[] camFaceBytes)
        {
            return 85;
        }

        public void StartMonitoring() { }
        public void StopMonitoring() { }
        public void PauseInternalCamera(bool doPause, int timeoutMs = 2000) { }
        public void Dispose() { }
    }
}
