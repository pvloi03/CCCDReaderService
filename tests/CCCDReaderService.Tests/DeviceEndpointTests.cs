using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using CCCDReaderService.Controllers;
using CCCDReaderService.Models;
using Xunit;

namespace CCCDReaderService.Tests;

public class DeviceEndpointTests
{
    [Fact]
    public void GetStatus_ReturnsHttpOkResult_WithDeviceStatusPayload()
    {
        // Arrange: Khởi tạo controller với NullLogger (không mock nội bộ, chỉ test ranh giới công khai)
        var logger = NullLogger<DeviceController>.Instance;
        var controller = new DeviceController(logger);

        // Act: Gọi API GET /api/device/status
        var actionResult = controller.GetStatus();

        // Assert: Xác nhận mã trạng thái 200 OK và dữ liệu trả về
        var okResult = Assert.IsType<OkObjectResult>(actionResult.Result);
        var status = Assert.IsType<DeviceStatusDto>(okResult.Value);

        Assert.True(status.IsOnline);
        Assert.Equal("CCCDReaderService", status.Service);
        Assert.Equal("1.0.0", status.Version);
        Assert.Equal("Ready", status.CardReaderStatus);
        Assert.Equal("Ready", status.CameraStatus);
        Assert.Null(status.ActiveSessionId);
        Assert.True((DateTime.Now - status.ServerTime).TotalSeconds < 5);
    }

    [Fact]
    public void DeviceStatusDto_DefaultValues_AreConsistent()
    {
        // Arrange & Act
        var dto = new DeviceStatusDto();

        // Assert: Đảm bảo các giá trị mặc định luôn an toàn cho client parse
        Assert.True(dto.IsOnline);
        Assert.False(string.IsNullOrWhiteSpace(dto.Service));
        Assert.False(string.IsNullOrWhiteSpace(dto.Version));
        Assert.Null(dto.ActiveSessionId);
    }

    [Fact]
    public void DeviceStatusDto_SerializesToJson_WithProperFields()
    {
        // Arrange
        var dto = new DeviceStatusDto
        {
            IsOnline = true,
            Service = "CCCDReaderService",
            Version = "1.0.0",
            CardReaderStatus = "Ready",
            CameraStatus = "Ready"
        };

        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        };

        // Act: Serialize giống như ASP.NET Core MVC mặc định
        var json = System.Text.Json.JsonSerializer.Serialize(dto, options);

        // Assert: Đảm bảo format JSON khớp 100% với hợp đồng API của client
        Assert.Contains("\"isOnline\":true", json);
        Assert.Contains("\"service\":\"CCCDReaderService\"", json);
        Assert.Contains("\"version\":\"1.0.0\"", json);
        Assert.Contains("\"cardReaderStatus\":\"Ready\"", json);
        Assert.Contains("\"cameraStatus\":\"Ready\"", json);
    }
}
