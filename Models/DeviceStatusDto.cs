namespace CCCDReaderService.Models;

public class DeviceStatusDto
{
    public bool IsOnline { get; set; } = true;
    public string Service { get; set; } = "CCCDReaderService";
    public string Version { get; set; } = "1.0.0";
    public string CardReaderStatus { get; set; } = "Ready";
    public string CameraStatus { get; set; } = "Ready";
    public string? ActiveSessionId { get; set; }
    public DateTime ServerTime { get; set; } = DateTime.Now;
}
