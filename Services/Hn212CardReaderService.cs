using Microsoft.AspNetCore.SignalR;
using CCCDReaderService.Exceptions;
using CCCDReaderService.Hubs;
using CCCDReaderService.Models;
using RAR.IdCard.Models;
using RAR.IdCard.Sdk.Models;
using RAR.IdCard.Sdk.Reader;
using RAR.IdCard.Sdk.Reader.HN212;

namespace CCCDReaderService.Services;

public class Hn212CardReaderService : ICardReaderService
{
    private readonly ISessionManager _sessionManager;
    private readonly IHardwareLock _hardwareLock;
    private readonly IHubContext<DeviceHub, IDeviceClient> _hubContext;
    private readonly ILogger<Hn212CardReaderService> _logger;
    private readonly VnHn212Reader _reader;
    private readonly object _lock = new();

    private bool _isMonitoring;
    private bool _hasCard;
    private TaskCompletionSource<CardFullData>? _readTcs;

    public bool IsDeviceConnected
    {
        get
        {
            try
            {
                return _reader.IsDeviceConnected();
            }
            catch
            {
                return false;
            }
        }
    }

    public bool HasCardInReader
    {
        get
        {
            try
            {
                var sensors = _reader.GetSensorsStatus();
                if (sensors != null)
                {
                    return sensors.CardOnReader;
                }
            }
            catch
            {
                // ignore
            }

            lock (_lock)
            {
                return _hasCard;
            }
        }
    }

    public event EventHandler<EventArgs>? CardInserted;
    public event EventHandler<EventArgs>? CardRemoved;
    public event EventHandler<string>? DeviceStatusChanged;

    public Hn212CardReaderService(
        ISessionManager sessionManager,
        IHardwareLock hardwareLock,
        IHubContext<DeviceHub, IDeviceClient> hubContext,
        ILogger<Hn212CardReaderService> logger)
    {
        _sessionManager = sessionManager;
        _hardwareLock = hardwareLock;
        _hubContext = hubContext;
        _logger = logger;
        _reader = new VnHn212Reader();
        _reader.OnStatusChanged += OnReaderStatusChanged;
    }

    public void StartMonitoring()
    {
        lock (_lock)
        {
            if (_isMonitoring) return;

            try
            {
                _logger.LogInformation("Khởi động giám sát đầu đọc HN-212 theo cấu hình Hanel SDK...");
                var config = new VnHn212Config
                {
                    AutoReadWhenPresent = true
                };
                _reader.StartMonitor(config);
                _isMonitoring = true;
                _logger.LogInformation("Giám sát đầu đọc HN-212 đã kích hoạt thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Không thể khởi động giám sát đầu đọc (thiết bị chưa cắm hoặc bận): {Message}", ex.Message);
            }
        }
    }

    public void StopMonitoring()
    {
        lock (_lock)
        {
            if (!_isMonitoring) return;

            try
            {
                _reader.StopMonitor();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Lỗi khi dừng giám sát đầu đọc: {Message}", ex.Message);
            }
            finally
            {
                _isMonitoring = false;
            }
        }
    }

    public void PauseInternalCamera(bool doPause, int timeoutMs = 2000)
    {
        try
        {
            _reader.ReqInternalCamToPause(doPause, timeoutMs);
            _logger.LogDebug("ReqInternalCamToPause: {Pause}, timeout: {Timeout}ms", doPause, timeoutMs);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Lỗi khi tạm dừng OCR-Camera nội bộ: {Message}", ex.Message);
        }
    }

    private void OnReaderStatusChanged(object sender, StatusEventArgs e)
    {
        _logger.LogDebug("HN-212 SDK Event: {EventName} - Serial: {Serial}", e.EventName, e.ReaderSerialNumber);

        switch (e.EventName)
        {
            case EVENT_NAMES.READER:
                ProcessReaderEvent(e as StatusEventReaderArgs);
                break;

            case EVENT_NAMES.CARD:
                ProcessCardEvent(e as StatusEventCardArgs);
                break;

            case EVENT_NAMES.READ:
                ProcessReadEvent(e as StatusEventReadArgs);
                break;

            case EVENT_NAMES.READER_CAMERA:
                ProcessCameraEvent(e as StatusEventCameraArgs);
                break;
        }
    }

    private void ProcessReaderEvent(StatusEventReaderArgs? ev)
    {
        if (ev == null) return;

        bool isConnected = ev.NewState == READER_STATUS.ADDED;
        var statusStr = isConnected ? "Connected" : "Disconnected";
        _logger.LogInformation("Đầu đọc HN-212 trạng thái: {Status}", statusStr);

        DeviceStatusChanged?.Invoke(this, statusStr);
        _hubContext.Clients.All.DeviceStatusChanged(new
        {
            @event = "DeviceStatusChanged",
            device = "CardReader",
            status = statusStr,
            timestamp = DateTime.Now
        });
    }

    private void ProcessCardEvent(StatusEventCardArgs? ev)
    {
        if (ev == null) return;

        lock (_lock)
        {
            if (ev.NewState == CARD_STATUS.PRESENT)
            {
                _hasCard = true;
                _logger.LogInformation("Thẻ CCCD được cắm vào đầu đọc (CARD_STATUS.PRESENT).");
                CardInserted?.Invoke(this, EventArgs.Empty);
                _hubContext.Clients.All.CardInserted(new
                {
                    @event = "CardInserted",
                    timestamp = DateTime.Now
                });
            }
            else if (ev.NewState == CARD_STATUS.EMPTY)
            {
                _hasCard = false;
                _logger.LogInformation("Thẻ CCCD bị rút ra khỏi đầu đọc (CARD_STATUS.EMPTY).");
                CardRemoved?.Invoke(this, EventArgs.Empty);
                _hubContext.Clients.All.CardRemoved(new
                {
                    @event = "CardRemoved",
                    timestamp = DateTime.Now
                });
            }
        }
    }

    private void ProcessCameraEvent(StatusEventCameraArgs? ev)
    {
        if (ev == null) return;

        bool isCamConnected = ev.NewState == CAMERA_STATUS.PRESENT;
        _logger.LogInformation("Camera đầu đọc trạng thái: {State}", ev.NewState);
        _hubContext.Clients.All.DeviceStatusChanged(new
        {
            @event = "DeviceStatusChanged",
            device = "ReaderCamera",
            status = isCamConnected ? "Connected" : "Disconnected",
            timestamp = DateTime.Now
        });
    }

    private void ProcessReadEvent(StatusEventReadArgs? ev)
    {
        if (ev == null) return;

        _logger.LogInformation("Đang đọc thẻ - Bước: {Step}, Trạng thái: {Status}", ev.Step, ev.Status);

        if (ev.Step == READ_CARD_STEPS.FINISH)
        {
            if (ev.Status == READ_CARD_STATUS.SUCCESS)
            {
                var cardFullData = _reader.CardData;
                if (cardFullData?.Dg13File != null)
                {
                    var citizenDto = ExtractCitizenDto(cardFullData);
                    var session = _sessionManager.CreateSession(citizenDto);
                    _logger.LogInformation("Đọc thẻ CCCD thành công cho số {CardNumber}, SessionId: {SessionId}", citizenDto.CardNumber, session.SessionId);

                    _hubContext.Clients.All.CardReadSuccess(new
                    {
                        @event = "CardReadSuccess",
                        sessionId = session.SessionId,
                        cardData = citizenDto,
                        timestamp = DateTime.Now
                    });

                    lock (_lock)
                    {
                        _readTcs?.TrySetResult(cardFullData);
                    }
                }
                else
                {
                    lock (_lock)
                    {
                        _readTcs?.TrySetException(new CardReadException("Đọc thẻ thành công nhưng không có cấu trúc dữ liệu cá nhân (DG13)."));
                    }
                }
            }
            else if (ev.Status == READ_CARD_STATUS.FAILURE)
            {
                lock (_lock)
                {
                    _readTcs?.TrySetException(new CardReadException($"Đọc thẻ CCCD thất bại ở bước {ev.Step}"));
                }
            }
        }
    }

    public async Task<CitizenCardDto> ReadCardAsync(CancellationToken cancellationToken = default)
    {
        if (!IsDeviceConnected)
        {
            throw new DeviceNotConnectedException();
        }

        using var lockHandle = await _hardwareLock.TryAcquireAsync("CardReader", TimeSpan.FromSeconds(1), cancellationToken);
        if (lockHandle == null)
        {
            throw new InvalidOperationException("Đầu đọc thẻ đang bận phục vụ một tiến trình khác.");
        }

        lock (_lock)
        {
            _readTcs = new TaskCompletionSource<CardFullData>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            _logger.LogInformation("Bắt đầu đọc dữ liệu thẻ CCCD qua Hanel SDK (StartReadCard)...");
            _reader.StartReadCard("", true);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            linkedCts.Token.Register(() => _readTcs.TrySetCanceled());

            var cardFullData = await _readTcs.Task;
            if (cardFullData == null || cardFullData.Dg13File == null)
            {
                throw new CardReadException("Không thể đọc hoặc giải mã cấu trúc dữ liệu chip thẻ CCCD.");
            }

            var citizenDto = ExtractCitizenDto(cardFullData);
            var session = _sessionManager.CreateSession(citizenDto);
            return citizenDto;
        }
        catch (OperationCanceledException)
        {
            throw new CardReadException("Quá trình đọc thẻ bị quá thời gian chờ hoặc bị hủy.");
        }
        finally
        {
            lock (_lock)
            {
                _readTcs = null;
            }
        }
    }

    private static CitizenCardDto ExtractCitizenDto(CardFullData cardFullData)
    {
        return new CitizenCardDto
        {
            CardNumber = cardFullData.Dg13File?.DocumentNumber ?? string.Empty,
            FullName = cardFullData.Dg13File?.Name ?? string.Empty,
            DateOfBirth = cardFullData.Dg13File?.DateOfBirth ?? string.Empty,
            Gender = cardFullData.Dg13File?.Sex ?? string.Empty,
            Nationality = cardFullData.Dg13File?.Nationality ?? "Việt Nam",
            Hometown = cardFullData.Dg13File?.Hometown ?? string.Empty,
            PermanentAddress = cardFullData.Dg13File?.Address ?? string.Empty,
            IssueDate = cardFullData.Dg13File?.IssueDate ?? string.Empty,
            ExpiryDate = cardFullData.Dg13File?.ExpiredDate ?? string.Empty,
            FaceImageBase64 = cardFullData.Dg2File?.FaceImage
        };
    }

    public void Dispose()
    {
        StopMonitoring();
        try
        {
            _reader.OnStatusChanged -= OnReaderStatusChanged;
        }
        catch
        {
            // Ignore on dispose
        }
    }
}
