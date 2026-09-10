using Microsoft.AspNetCore.SignalR;
using CCCDReaderService.Exceptions;
using CCCDReaderService.Hubs;
using CCCDReaderService.Models;
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
                _logger.LogInformation("Khởi động giám sát đầu đọc HN-212...");
                var config = new VnHn212Config();
                _reader.StartMonitor(config);
                _isMonitoring = true;
                _logger.LogInformation("Giám sát đầu đọc HN-212 đã kích hoạt.");
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

    private void OnReaderStatusChanged(object sender, StatusEventArgs e)
    {
        _logger.LogDebug("Reader Event: {EventName} - Serial: {Serial}", e.EventName, e.ReaderSerialNumber);

        switch (e.EventName)
        {
            case EVENT_NAMES.READER:
                var connected = IsDeviceConnected;
                var statusStr = connected ? "Connected" : "Disconnected";
                DeviceStatusChanged?.Invoke(this, statusStr);
                _hubContext.Clients.All.DeviceStatusChanged(new
                {
                    @event = "DeviceStatusChanged",
                    device = "CardReader",
                    status = statusStr,
                    timestamp = DateTime.Now
                });
                break;

            case EVENT_NAMES.CARD:
            case EVENT_NAMES.SENSORS:
                HandleCardPresenceChanged();
                break;

            case EVENT_NAMES.READ:
                HandleReadCompleted();
                break;
        }
    }

    private void HandleCardPresenceChanged()
    {
        bool currentPresence = false;
        try
        {
            var sensors = _reader.GetSensorsStatus();
            currentPresence = sensors != null && sensors.CardInserted(new RAR.IdCard.Sdk.Reader.HN212.Hn212SensorInfo(0));
        }
        catch
        {
            // Fallback: assume card event implies presence
            currentPresence = !_hasCard;
        }

        lock (_lock)
        {
            if (currentPresence != _hasCard)
            {
                _hasCard = currentPresence;
                if (_hasCard)
                {
                    _logger.LogInformation("Phát hiện thẻ CCCD được cắm vào đầu đọc.");
                    CardInserted?.Invoke(this, EventArgs.Empty);
                    _hubContext.Clients.All.CardInserted(new
                    {
                        @event = "CardInserted",
                        timestamp = DateTime.Now
                    });
                }
                else
                {
                    _logger.LogInformation("Phát hiện thẻ CCCD đã được rút ra.");
                    CardRemoved?.Invoke(this, EventArgs.Empty);
                    _hubContext.Clients.All.CardRemoved(new
                    {
                        @event = "CardRemoved",
                        timestamp = DateTime.Now
                    });
                }
            }
        }
    }

    private void HandleReadCompleted()
    {
        lock (_lock)
        {
            if (_readTcs != null && !_readTcs.Task.IsCompleted)
            {
                var cardData = _reader.CardData;
                _readTcs.TrySetResult(cardData);
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
            _logger.LogInformation("Bắt đầu đọc dữ liệu thẻ CCCD qua APDU...");
            _reader.StartReadCard("", true);

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            linkedCts.Token.Register(() => _readTcs.TrySetCanceled());

            var cardFullData = await _readTcs.Task;
            if (cardFullData == null || cardFullData.Dg13File == null)
            {
                throw new CardReadException("Không thể đọc hoặc giải mã cấu trúc dữ liệu chip thẻ CCCD.");
            }

            var citizenDto = new CitizenCardDto
            {
                CardNumber = cardFullData.Dg13File.DocumentNumber ?? string.Empty,
                FullName = cardFullData.Dg13File.Name ?? string.Empty,
                DateOfBirth = cardFullData.Dg13File.DateOfBirth ?? string.Empty,
                Gender = cardFullData.Dg13File.Sex ?? string.Empty,
                Nationality = cardFullData.Dg13File.Nationality ?? "Việt Nam",
                Hometown = cardFullData.Dg13File.Hometown ?? string.Empty,
                PermanentAddress = cardFullData.Dg13File.Address ?? string.Empty,
                IssueDate = cardFullData.Dg13File.IssueDate ?? string.Empty,
                ExpiryDate = cardFullData.Dg13File.ExpiredDate ?? string.Empty,
                FaceImageBase64 = cardFullData.Dg2File?.FaceImage
            };

            var session = _sessionManager.CreateSession(citizenDto);
            _logger.LogInformation("Đọc thẻ CCCD thành công cho số {CardNumber}, SessionId: {SessionId}", citizenDto.CardNumber, session.SessionId);

            await _hubContext.Clients.All.CardReadSuccess(new
            {
                @event = "CardReadSuccess",
                sessionId = session.SessionId,
                cardData = citizenDto,
                timestamp = DateTime.Now
            });

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
