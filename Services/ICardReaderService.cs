using CCCDReaderService.Models;

namespace CCCDReaderService.Services;

public interface ICardReaderService : IDisposable
{
    bool IsDeviceConnected { get; }
    bool HasCardInReader { get; }
    Task<CitizenCardDto> ReadCardAsync(CancellationToken cancellationToken = default);
    void StartMonitoring();
    void StopMonitoring();
    void PauseInternalCamera(bool doPause, int timeoutMs = 2000);

    event EventHandler<EventArgs>? CardInserted;
    event EventHandler<EventArgs>? CardRemoved;
    event EventHandler<string>? DeviceStatusChanged;
}
