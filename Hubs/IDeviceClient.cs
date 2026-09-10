namespace CCCDReaderService.Hubs;

public interface IDeviceClient
{
    Task CardInserted(object payload);
    Task CardRemoved(object payload);
    Task CardReadSuccess(object payload);
    Task DeviceStatusChanged(object payload);
}
