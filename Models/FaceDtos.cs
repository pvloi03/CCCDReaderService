namespace CCCDReaderService.Models;

public class FaceVerifyRequest
{
    /// <summary>
    /// Ảnh chụp camera dưới dạng chuỗi base64 (tùy chọn).
    /// Nếu để trống, hệ thống sẽ tự động điều khiển Camera đang kết nối để chụp khung hình.
    /// </summary>
    public string? CameraImageBase64 { get; set; }

    /// <summary>
    /// Ngưỡng tương đồng tối thiểu để đánh giá là cùng một người (mặc định 0.62 theo chuẩn ViewFaceCore).
    /// </summary>
    public double? SimilarityThreshold { get; set; }
}

public class FaceVerifyResultDto
{
    public bool IsMatch { get; set; }
    public double Similarity { get; set; }
    public bool IsLive { get; set; }
    public string? LivenessStatus { get; set; }
    public string? CapturedFaceImageBase64 { get; set; }
    public string? Message { get; set; }
}

public class FaceCaptureResponseDto
{
    public string ImageBase64 { get; set; } = string.Empty;
    public bool FaceDetected { get; set; }
    public int FaceCount { get; set; }
    public DateTime CapturedAt { get; set; } = DateTime.Now;
}
