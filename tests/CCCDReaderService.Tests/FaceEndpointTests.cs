using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using CCCDReaderService.Controllers;
using CCCDReaderService.Exceptions;
using CCCDReaderService.Models;
using CCCDReaderService.Services;
using Xunit;

namespace CCCDReaderService.Tests;

public class FaceEndpointTests
{
    private readonly ISessionManager _sessionManager;
    private readonly IHardwareLock _hardwareLock;

    public FaceEndpointTests()
    {
        _sessionManager = new SessionManager(defaultTtlSeconds: 120);
        _hardwareLock = new HardwareLock();
    }

    [Fact]
    public async Task Verify_Returns404_WhenNoActiveSession()
    {
        // Arrange: Không có phiên làm việc đọc thẻ nào
        var stubFaceService = new StubFaceService(isCameraAvailable: true);
        var controller = new FaceController(_sessionManager, _hardwareLock, stubFaceService, NullLogger<FaceController>.Instance);

        // Act
        var result = await controller.Verify(new FaceVerifyRequest(), CancellationToken.None);

        // Assert: 404 Not Found
        var objectResult = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.NotNull(objectResult.Value);
    }

    [Fact]
    public async Task Verify_Returns400_WhenActiveSessionHasNoCardFaceImage()
    {
        // Arrange: Phiên làm việc có thẻ nhưng không có ảnh chân dung chip DG2
        var cardWithoutFace = new CitizenCardDto
        {
            CardNumber = "001200001234",
            FullName = "NGUYEN VAN A",
            FaceImageBase64 = null
        };
        _sessionManager.CreateSession(cardWithoutFace);

        var stubFaceService = new StubFaceService(isCameraAvailable: true);
        var controller = new FaceController(_sessionManager, _hardwareLock, stubFaceService, NullLogger<FaceController>.Instance);

        // Act
        var result = await controller.Verify(new FaceVerifyRequest(), CancellationToken.None);

        // Assert: 400 Bad Request
        var objectResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.NotNull(objectResult.Value);
    }

    [Fact]
    public async Task Verify_Returns409_WhenCameraHardwareIsLocked()
    {
        // Arrange: Phiên hợp lệ nhưng Camera đang bị tiến trình khác chiếm khóa
        var cardWithFace = new CitizenCardDto
        {
            CardNumber = "001200001234",
            FullName = "NGUYEN VAN A",
            FaceImageBase64 = "valid_card_face_base64"
        };
        _sessionManager.CreateSession(cardWithFace);

        var stubFaceService = new StubFaceService(isCameraAvailable: true);
        var controller = new FaceController(_sessionManager, _hardwareLock, stubFaceService, NullLogger<FaceController>.Instance);

        _hardwareLock.TryAcquire("Camera");

        // Act
        var result = await controller.Verify(new FaceVerifyRequest(), CancellationToken.None);

        // Assert: 409 Conflict
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(409, objectResult.StatusCode);
    }

    [Fact]
    public async Task Verify_Returns422_WhenNoFaceDetectedInProbeImage()
    {
        // Arrange: Phiên hợp lệ nhưng ảnh không tìm thấy khuôn mặt
        var cardWithFace = new CitizenCardDto
        {
            CardNumber = "001200001234",
            FullName = "NGUYEN VAN A",
            FaceImageBase64 = "valid_card_face_base64"
        };
        _sessionManager.CreateSession(cardWithFace);

        var stubFaceService = new StubFaceService(isCameraAvailable: true);
        stubFaceService.ThrowOnVerify = new FaceNotDetectedException("Không phát hiện thấy khuôn mặt trong ảnh chụp camera.");

        var controller = new FaceController(_sessionManager, _hardwareLock, stubFaceService, NullLogger<FaceController>.Instance);

        // Act
        var result = await controller.Verify(new FaceVerifyRequest(), CancellationToken.None);

        // Assert: 422 Unprocessable Entity
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(422, objectResult.StatusCode);
    }

    [Fact]
    public async Task Verify_Returns200_WithMatchAndLiveness_WhenValidFacesProvided()
    {
        // Arrange: Phiên hợp lệ, nhận diện mặt khớp và là người thật (Liveness passed)
        var cardWithFace = new CitizenCardDto
        {
            CardNumber = "001200001234",
            FullName = "NGUYEN VAN A",
            FaceImageBase64 = "valid_card_face_base64"
        };
        _sessionManager.CreateSession(cardWithFace);

        var stubFaceService = new StubFaceService(isCameraAvailable: true)
        {
            ResultToReturn = new FaceVerifyResultDto
            {
                IsMatch = true,
                Similarity = 0.85,
                IsLive = true,
                LivenessStatus = "Real",
                CapturedFaceImageBase64 = "captured_frame_base64",
                Message = "Xác thực khuôn mặt thành công và hợp lệ."
            }
        };

        var controller = new FaceController(_sessionManager, _hardwareLock, stubFaceService, NullLogger<FaceController>.Instance);

        // Act
        var result = await controller.Verify(new FaceVerifyRequest(), CancellationToken.None);

        // Assert: 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<FaceVerifyResultDto>(okResult.Value);

        Assert.True(payload.IsMatch);
        Assert.True(payload.IsLive);
        Assert.Equal(0.85, payload.Similarity);
        Assert.Equal("Real", payload.LivenessStatus);
        Assert.False(string.IsNullOrEmpty(payload.CapturedFaceImageBase64));
    }

    [Fact]
    public async Task Capture_Returns503_WhenCameraNotAvailable()
    {
        // Arrange: Camera không khả dụng
        var stubFaceService = new StubFaceService(isCameraAvailable: false);
        var controller = new FaceController(_sessionManager, _hardwareLock, stubFaceService, NullLogger<FaceController>.Instance);

        // Act
        var result = await controller.CaptureFrame(CancellationToken.None);

        // Assert: 503 Service Unavailable
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, objectResult.StatusCode);
    }

    [Fact]
    public async Task Capture_Returns200_WhenCameraCapturesSuccessfully()
    {
        // Arrange: Camera chụp thành công
        var stubFaceService = new StubFaceService(isCameraAvailable: true)
        {
            CapturedImageToReturn = "frame_base64_data"
        };
        var controller = new FaceController(_sessionManager, _hardwareLock, stubFaceService, NullLogger<FaceController>.Instance);

        // Act
        var result = await controller.CaptureFrame(CancellationToken.None);

        // Assert: 200 OK
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        Assert.NotNull(okResult.Value);
    }

    private sealed class StubFaceService : IFaceService
    {
        private readonly bool _isCameraAvailable;

        public bool IsCameraAvailable => _isCameraAvailable;
        public Exception? ThrowOnVerify { get; set; }
        public FaceVerifyResultDto? ResultToReturn { get; set; }
        public string? CapturedImageToReturn { get; set; }

        public StubFaceService(bool isCameraAvailable)
        {
            _isCameraAvailable = isCameraAvailable;
        }

        public Task<string> CaptureFrameBase64Async(CancellationToken cancellationToken = default)
        {
            if (!_isCameraAvailable) throw new CameraNotAvailableException("Không tìm thấy camera.");
            return Task.FromResult(CapturedImageToReturn ?? "default_frame_base64");
        }

        public Task<FaceVerifyResultDto> VerifyAsync(string probeImageBase64, string cardImageBase64, double threshold = 0.62, CancellationToken cancellationToken = default)
        {
            if (ThrowOnVerify != null) throw ThrowOnVerify;
            return Task.FromResult(ResultToReturn ?? new FaceVerifyResultDto
            {
                IsMatch = true,
                Similarity = 0.8,
                IsLive = true
            });
        }

        public Task<FaceVerifyResultDto> CaptureAndVerifyAsync(string cardImageBase64, double threshold = 0.62, CancellationToken cancellationToken = default)
        {
            if (!_isCameraAvailable) throw new CameraNotAvailableException("Không tìm thấy camera.");
            if (ThrowOnVerify != null) throw ThrowOnVerify;
            return Task.FromResult(ResultToReturn ?? new FaceVerifyResultDto
            {
                IsMatch = true,
                Similarity = 0.8,
                IsLive = true
            });
        }

        public void Dispose() { }
    }
}
