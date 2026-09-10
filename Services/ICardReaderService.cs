using CCCDReaderService.Models;

namespace CCCDReaderService.Services;

public interface ICardReaderService : IDisposable
{
    bool IsDeviceConnected { get; }
    bool IsDeviceCameraConnected { get; }
    bool HasCardInReader { get; }
    byte[]? LastFrameBytes { get; }
    Task<CitizenCardDto> ReadCardAsync(CancellationToken cancellationToken = default);
    Task<byte[]> CaptureFaceFromDeviceAsync(CancellationToken cancellationToken = default);
    int CompareFace(byte[] chipFaceBytes, byte[] camFaceBytes);
    void StartMonitoring();
    void StopMonitoring();
    void PauseInternalCamera(bool doPause, int timeoutMs = 2000);

    event EventHandler<EventArgs>? CardInserted;
    event EventHandler<EventArgs>? CardRemoved;
    event EventHandler<string>? DeviceStatusChanged;
    event EventHandler<byte[]>? VideoFrameReceived;
}
