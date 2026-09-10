using Microsoft.AspNetCore.SignalR;

namespace CCCDReaderService.Hubs;

public class DeviceHub : Hub<IDeviceClient>
{
    private readonly ILogger<DeviceHub> _logger;

    public DeviceHub(ILogger<DeviceHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected to DeviceHub: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected from DeviceHub: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
