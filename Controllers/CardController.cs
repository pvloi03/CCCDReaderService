using Microsoft.AspNetCore.Mvc;
using CCCDReaderService.Exceptions;
using CCCDReaderService.Models;
using CCCDReaderService.Services;

namespace CCCDReaderService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CardController : ControllerBase
{
    private readonly ISessionManager _sessionManager;
    private readonly IHardwareLock _hardwareLock;
    private readonly ICardReaderService? _cardReaderService;
    private readonly ILogger<CardController> _logger;

    public CardController(
        ISessionManager sessionManager,
        IHardwareLock hardwareLock,
        ILogger<CardController> logger,
        ICardReaderService? cardReaderService = null)
    {
        _sessionManager = sessionManager;
        _hardwareLock = hardwareLock;
        _logger = logger;
        _cardReaderService = cardReaderService;
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

    /// <summary>
    /// Kích hoạt đọc vi mạch thẻ CCCD gắn chip qua đầu đọc HN-212 và tạo phiên làm việc mới.
    /// </summary>
    [HttpPost("read")]
    public async Task<ActionResult<CardSessionResponseDto>> ReadCard(CancellationToken cancellationToken)
    {
        if (_cardReaderService == null || !_cardReaderService.IsDeviceConnected)
        {
            return StatusCode(503, new { message = "Thiết bị đầu đọc thẻ chưa được kết nối vào máy tính." });
        }

        if (_hardwareLock.IsLocked("CardReader"))
        {
            return StatusCode(409, new { message = "Đầu đọc thẻ đang bận thực hiện một thao tác khác." });
        }

        try
        {
            var cardData = await _cardReaderService.ReadCardAsync(cancellationToken);
            var session = _sessionManager.GetActiveSession();
            if (session == null || session.CardData.CardNumber != cardData.CardNumber)
            {
                session = _sessionManager.CreateSession(cardData);
            }

            var response = new CardSessionResponseDto
            {
                SessionId = session.SessionId,
                CreatedAt = session.CreatedAt,
                ExpiresAt = session.ExpiresAt,
                CardData = cardData
            };

            return Ok(response);
        }
        catch (DeviceNotConnectedException ex)
        {
            return StatusCode(503, new { message = ex.Message });
        }
        catch (CardNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(409, new { message = ex.Message });
        }
        catch (CardReadException ex)
        {
            return StatusCode(422, new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi đọc thẻ CCCD");
            return StatusCode(500, new { message = $"Lỗi không xác định khi đọc thẻ: {ex.Message}" });
        }
    }
}
