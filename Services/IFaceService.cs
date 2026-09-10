using CCCDReaderService.Models;

namespace CCCDReaderService.Services;

public interface IFaceService : IDisposable
{
    /// <summary>
    /// Kiểm tra trạng thái thiết bị Camera/Webcam có sẵn sàng hay không.
    /// </summary>
    bool IsCameraAvailable { get; }

    /// <summary>
    /// Chụp một khung hình từ Camera đang kết nối và trả về dạng ảnh base64.
    /// </summary>
    Task<string> CaptureFrameBase64Async(CancellationToken cancellationToken = default);

    /// <summary>
    /// Chụp một khung hình từ Camera và kiểm tra số lượng khuôn mặt xuất hiện.
    /// </summary>
    Task<FaceCaptureResponseDto> CaptureAndDetectAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new FaceCaptureResponseDto());
    }

    /// <summary>
    /// So khớp khuôn mặt giữa ảnh truyền vào (probe) và ảnh thẻ CCCD chip (cardImage).
    /// Đồng thời kiểm tra tính sống động người thật (Anti-spoofing / Liveness).
    /// </summary>
    Task<FaceVerifyResultDto> VerifyAsync(string probeImageBase64, string cardImageBase64, double threshold = 0.62, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tự động chụp từ Camera đang kết nối và so khớp với ảnh thẻ CCCD chip.
    /// </summary>
    Task<FaceVerifyResultDto> CaptureAndVerifyAsync(string cardImageBase64, double threshold = 0.62, CancellationToken cancellationToken = default);
}
