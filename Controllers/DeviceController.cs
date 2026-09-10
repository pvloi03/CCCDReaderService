using Microsoft.AspNetCore.Mvc;
using CCCDReaderService.Models;

namespace CCCDReaderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DeviceController : ControllerBase
{
    private readonly ILogger<DeviceController> _logger;

    public DeviceController(ILogger<DeviceController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Lấy trạng thái hoạt động của dịch vụ và các thiết bị ngoại vi.
    /// </summary>
    [HttpGet("status")]
    public ActionResult<DeviceStatusDto> GetStatus()
    {
        var status = new DeviceStatusDto
        {
            IsOnline = true,
            Service = "CCCDReaderService",
            Version = "1.0.0",
            CardReaderStatus = "Ready",
            CameraStatus = "Ready",
            ServerTime = DateTime.Now
        };

        return Ok(status);
    }
}
