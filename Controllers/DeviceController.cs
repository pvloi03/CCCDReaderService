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
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(
        ILogger<DeviceController> logger,
        ISessionManager? sessionManager = null,
        IHardwareLock? hardwareLock = null)
    {
        _logger = logger;
        _sessionManager = sessionManager;
        _hardwareLock = hardwareLock;
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

        var status = new DeviceStatusDto
        {
            IsOnline = true,
            Service = "CCCDReaderService",
            Version = "1.0.0",
            CardReaderStatus = isCardLocked ? "Busy" : "Ready",
            CameraStatus = isCameraLocked ? "Busy" : "Ready",
            ActiveSessionId = activeSession?.SessionId,
            ServerTime = DateTime.Now
        };

        return Ok(status);
    }
}
