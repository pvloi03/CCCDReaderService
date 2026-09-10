using CCCDReaderService.Services;
using Xunit;

namespace CCCDReaderService.Tests;

public class HardwareLockTests
{
    private readonly IHardwareLock _hardwareLock;

    public HardwareLockTests()
    {
        _hardwareLock = new HardwareLock();
    }

    [Fact]
    public void TryAcquire_Succeeds_WhenResourceIsAvailable()
    {
        // Act: Khóa tài nguyên đầu đọc thẻ
        using var handle = _hardwareLock.TryAcquire("CardReader");

        // Assert
        Assert.NotNull(handle);
        Assert.True(_hardwareLock.IsLocked("CardReader"));
    }

    [Fact]
    public void TryAcquire_Fails_WhenResourceIsAlreadyLocked()
    {
        // Arrange: Client 1 chiếm khóa
        using var handle1 = _hardwareLock.TryAcquire("CardReader");
        Assert.NotNull(handle1);

        // Act: Client 2 cố tình chiếm khóa cùng lúc
        var handle2 = _hardwareLock.TryAcquire("CardReader");

        // Assert: Client 2 phải bị từ chối (null handle)
        Assert.Null(handle2);
    }

    [Fact]
    public void DisposingHandle_ReleasesLock_AllowingSubsequentAcquire()
    {
        // Arrange & Act 1: Chiếm và giải phóng khóa
        using (var handle1 = _hardwareLock.TryAcquire("CardReader"))
        {
            Assert.NotNull(handle1);
            Assert.True(_hardwareLock.IsLocked("CardReader"));
        }

        // Assert 1: Sau khi using kết thúc, khóa đã được tự động giải phóng
        Assert.False(_hardwareLock.IsLocked("CardReader"));

        // Act 2: Client tiếp theo có thể chiếm khóa thành công
        using var handle2 = _hardwareLock.TryAcquire("CardReader");
        Assert.NotNull(handle2);
        Assert.True(_hardwareLock.IsLocked("CardReader"));
    }

    [Fact]
    public void IndependentResources_DoNotBlockEachOther()
    {
        // Act: Chiếm khóa CardReader và Camera đồng thời
        using var cardHandle = _hardwareLock.TryAcquire("CardReader");
        using var cameraHandle = _hardwareLock.TryAcquire("Camera");

        // Assert: Cả 2 khóa cho 2 thiết bị khác nhau đều thành công độc lập
        Assert.NotNull(cardHandle);
        Assert.NotNull(cameraHandle);
        Assert.True(_hardwareLock.IsLocked("CardReader"));
        Assert.True(_hardwareLock.IsLocked("Camera"));
    }
}
