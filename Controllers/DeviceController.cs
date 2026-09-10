using Microsoft.AspNetCore.Mvc;
using CCCDReaderService.Models;
using CCCDReaderService.Services;

namespace CCCDReaderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly ISessionManager? _sessionManager;
    private readonly IHardwareLock? _hardwareLock;
    private readonly ICardReaderService? _cardReaderService;
    private readonly IFaceService? _faceService;
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(
        ILogger<DeviceController> logger,
        ISessionManager? sessionManager = null,
        IHardwareLock? hardwareLock = null,
        ICardReaderService? cardReaderService = null,
        IFaceService? faceService = null)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _hardwareLock = hardwareLock;
        _cardReaderService = cardReaderService;
        _faceService = faceService;
    }

    /// <summary>
    /// Lấy trạng thái hoạt động của dịch vụ và các thiết bị ngoại vi.
    /// </summary>
    [HttpGet("status")]
    public ActionResult<DeviceStatusDto> GetStatus()
    {
        var activeSession = _sessionManager?.GetActiveSession();
        var isCardLocked = _hardwareLock?.IsLocked("CardReader") ?? false;
        var isCameraLocked = _hardwareLock?.IsLocked("Camera") ?? false;

        string cardStatus = "Ready";
        if (_cardReaderService != null && !_cardReaderService.IsDeviceConnected)
        {
            cardStatus = "Disconnected";
        }
        else if (isCardLocked)
        {
            cardStatus = "Busy";
        }

        string cameraStatus = "Ready";
        if (_faceService != null && !_faceService.IsCameraAvailable)
        {
            cameraStatus = "Disconnected";
        }
        else if (isCameraLocked)
        {
            cameraStatus = "Busy";
        }

        var status = new DeviceStatusDto
        {
            IsOnline = true,
            Service = "CCCDReaderService",
            Version = "1.0.0",
            CardReaderStatus = cardStatus,
            CameraStatus = cameraStatus,
            ActiveSessionId = activeSession?.SessionId,
            ServerTime = DateTime.Now
        };

        return Ok(status);
    }
}
