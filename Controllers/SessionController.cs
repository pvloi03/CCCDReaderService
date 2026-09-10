using Microsoft.AspNetCore.Mvc;
using CCCDReaderService.Services;

namespace CCCDReaderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly IHardwareLock _hardwareLock;
    private readonly ILogger<SessionController> _logger;

    public SessionController(
        ISessionManager sessionManager,
        IHardwareLock hardwareLock,
        ILogger<SessionController> logger)
    {
        _sessionManager = sessionManager;
        _hardwareLock = hardwareLock;
        _logger = logger;
    }

    /// <summary>
    /// Hủy phiên làm việc hiện tại và giải phóng ngay lập tức mọi khóa phần cứng.
    /// </summary>
    [HttpPost("cancel")]
    public IActionResult CancelSession()
    {
        _logger.LogInformation("Yêu cầu hủy phiên làm việc hiện tại.");
        _sessionManager.CancelCurrentSession();
        _hardwareLock.ReleaseAll();

        return Ok(new
        {
            success = true,
            message = "Đã hủy phiên làm việc và giải phóng khóa phần cứng thành công."
        });
    }
}
