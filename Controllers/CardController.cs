using Microsoft.AspNetCore.Mvc;
using CCCDReaderService.Models;
using CCCDReaderService.Services;

namespace CCCDReaderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly IHardwareLock _hardwareLock;
    private readonly ILogger<CardController> _logger;

    public CardController(
        ISessionManager sessionManager,
        IHardwareLock hardwareLock,
        ILogger<CardController> logger)
    {
        _sessionManager = sessionManager;
        _hardwareLock = hardwareLock;
        _logger = logger;
    }

    /// <summary>
    /// Lấy thông tin thẻ của phiên làm việc đang hoạt động hiện tại.
    /// </summary>
    [HttpGet("current")]
    public ActionResult<CardSessionResponseDto> GetCurrentCard()
    {
        var session = _sessionManager.GetActiveSession();
        if (session == null)
        {
            return NotFound(new { message = "Không có phiên làm việc nào đang hoạt động." });
        }

        var response = new CardSessionResponseDto
        {
            SessionId = session.SessionId,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
            CardData = session.CardData
        };

        return Ok(response);
    }
}
