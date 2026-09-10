using Microsoft.AspNetCore.Mvc;
using CCCDReaderService.Exceptions;
using CCCDReaderService.Models;
using CCCDReaderService.Services;

namespace CCCDReaderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FaceController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly IHardwareLock _hardwareLock;
    private readonly IFaceService _faceService;
    private readonly ILogger<FaceController> _logger;

    public FaceController(
        ISessionManager sessionManager,
        IHardwareLock hardwareLock,
        IFaceService faceService,
        ILogger<FaceController> logger)
    {
        _sessionManager = sessionManager;
        _hardwareLock = hardwareLock;
        _faceService = faceService;
        _logger = logger;
    }

    /// <summary>
    /// Chụp một khung hình từ Camera/Webcam hiện tại.
    /// </summary>
    [HttpGet("capture")]
    public async Task<ActionResult<FaceCaptureResponseDto>> CaptureFrame(CancellationToken cancellationToken)
    {
        if (!_faceService.IsCameraAvailable)
        {
            return StatusCode(503, new { message = "Thiết bị Camera không khả dụng hoặc chưa được kết nối." });
        }

        if (_hardwareLock.IsLocked("Camera"))
        {
            return StatusCode(409, new { message = "Camera đang bận thực hiện tác vụ khác." });
        }

        try
        {
            using var lockHandle = await _hardwareLock.TryAcquireAsync("Camera", TimeSpan.FromSeconds(2), cancellationToken);
            if (lockHandle == null)
            {
                return StatusCode(409, new { message = "Camera đang bận thực hiện tác vụ khác." });
            }

            var imageBase64 = await _faceService.CaptureFrameBase64Async(cancellationToken);
            return Ok(new FaceCaptureResponseDto
            {
                ImageBase64 = imageBase64,
                FaceDetected = true,
                FaceCount = 1,
                CapturedAt = DateTime.Now
            });
        }
        catch (CameraNotAvailableException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi chụp khung hình từ Camera.");
            return StatusCode(500, new { message = "Lỗi khi chụp hình từ Camera: " + ex.Message });
        }
    }

    /// <summary>
    /// Xác thực sinh trắc học khuôn mặt người thật (Liveness) và so khớp với ảnh thẻ chip CCCD trong phiên hiện tại.
    /// </summary>
    [HttpPost("verify")]
    public async Task<ActionResult<FaceVerifyResultDto>> Verify([FromBody] FaceVerifyRequest? request, CancellationToken cancellationToken)
    {
        request ??= new FaceVerifyRequest();

        var activeSession = _sessionManager.GetActiveSession();
        if (activeSession == null)
        {
            return NotFound(new { message = "Không tìm thấy phiên đọc thẻ nào đang hoạt động. Vui lòng đọc thẻ CCCD trước." });
        }

        if (string.IsNullOrWhiteSpace(activeSession.CardData.FaceImageBase64))
        {
            return BadRequest(new { message = "Dữ liệu thẻ CCCD của phiên hiện tại không chứa ảnh chân dung vi mạch (DG2)." });
        }

        var threshold = request.SimilarityThreshold ?? 0.62;

        try
        {
            FaceVerifyResultDto result;

            if (!string.IsNullOrWhiteSpace(request.CameraImageBase64))
            {
                // Sử dụng ảnh client cung cấp
                result = await _faceService.VerifyAsync(
                    request.CameraImageBase64,
                    activeSession.CardData.FaceImageBase64,
                    threshold,
                    cancellationToken);
            }
            else
            {
                // Tự động chụp từ Camera vật lý
                if (!_faceService.IsCameraAvailable)
                {
                    return StatusCode(503, new { message = "Thiết bị Camera không khả dụng hoặc chưa được kết nối." });
                }

                if (_hardwareLock.IsLocked("Camera"))
                {
                    return StatusCode(409, new { message = "Camera đang bận phục vụ một tiến trình khác." });
                }

                using var lockHandle = await _hardwareLock.TryAcquireAsync("Camera", TimeSpan.FromSeconds(2), cancellationToken);
                if (lockHandle == null)
                {
                    return StatusCode(409, new { message = "Camera đang bận phục vụ một tiến trình khác." });
                }

                result = await _faceService.CaptureAndVerifyAsync(
                    activeSession.CardData.FaceImageBase64,
                    threshold,
                    cancellationToken);
            }

            return Ok(result);
        }
        catch (CameraNotAvailableException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
        catch (FaceNotDetectedException ex)
        {
            return StatusCode(422, new { message = ex.Message });
        }
        catch (MultipleFacesDetectedException ex)
        {
            return StatusCode(422, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi xảy ra trong quá trình xác thực khuôn mặt.");
            return StatusCode(500, new { message = "Lỗi xử lý xác thực khuôn mặt: " + ex.Message });
        }
    }
}
