using OpenCvSharp;
using SkiaSharp;
using ViewFaceCore.Core;
using ViewFaceCore.Model;
using ViewFaceCore;
using CCCDReaderService.Exceptions;
using CCCDReaderService.Models;

namespace CCCDReaderService.Services;

public class ViewFaceCoreService : IFaceService
{
    private readonly ILogger<ViewFaceCoreService> _logger;
    private readonly FaceDetector _detector;
    private readonly FaceLandmarker _landmarker;
    private readonly FaceRecognizer _recognizer;
    private readonly FaceAntiSpoofing _antiSpoofing;
    private readonly object _lock = new();

    public ViewFaceCoreService(ILogger<ViewFaceCoreService> logger)
    {
        _logger = logger;
        _detector = new FaceDetector();
        _landmarker = new FaceLandmarker();
        _recognizer = new FaceRecognizer();
        _antiSpoofing = new FaceAntiSpoofing();
    }

    public bool IsCameraAvailable
    {
        get
        {
            try
            {
                using var capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
                return capture.IsOpened();
            }
            catch
            {
                return false;
            }
        }
    }

    public Task<string> CaptureFrameBase64Async(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            try
            {
                using var capture = new VideoCapture(0, VideoCaptureAPIs.DSHOW);
                if (!capture.IsOpened())
                {
                    throw new CameraNotAvailableException("Không thể kết nối đến Camera trên máy tính.");
                }

                using var mat = new Mat();
                // Đọc bỏ vài frame đầu để auto-exposure và cân bằng trắng ổn định
                for (int i = 0; i < 3; i++)
                {
                    capture.Read(mat);
                }

                if (mat.Empty())
                {
                    throw new CameraNotAvailableException("Không thu được khung hình từ Camera.");
                }

                Cv2.ImEncode(".jpg", mat, out var buf);
                var base64 = Convert.ToBase64String(buf);
                return Task.FromResult(base64);
            }
            catch (CameraNotAvailableException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi thao tác với phần cứng Camera.");
                throw new CameraNotAvailableException("Lỗi truy cập Camera: " + ex.Message);
            }
        }
    }

    public Task<FaceVerifyResultDto> VerifyAsync(
        string probeImageBase64,
        string cardImageBase64,
        double threshold = 0.62,
        CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            var probeBitmap = DecodeBase64Image(probeImageBase64);
            var cardBitmap = DecodeBase64Image(cardImageBase64);

            try
            {
                // 1. Phát hiện khuôn mặt ảnh Camera (probe)
                var probeFaces = _detector.Detect(probeBitmap);
                if (probeFaces == null || probeFaces.Length == 0)
                {
                    throw new FaceNotDetectedException("Không phát hiện thấy khuôn mặt trong ảnh chụp camera.");
                }
                if (probeFaces.Length > 1)
                {
                    throw new MultipleFacesDetectedException("Phát hiện nhiều hơn một khuôn mặt trong khung hình camera.");
                }

                var probePoints = _landmarker.Mark(probeBitmap, probeFaces[0]);

                // 2. Kiểm tra tính sống động người thật (Anti-spoofing)
                var livenessResult = _antiSpoofing.AntiSpoofing(probeBitmap, probeFaces[0], probePoints);
                bool isLive = livenessResult.Status == AntiSpoofingStatus.Real;
                string livenessStatusStr = livenessResult.Status.ToString();

                // 3. Phát hiện khuôn mặt ảnh thẻ CCCD chip
                var cardFaces = _detector.Detect(cardBitmap);
                if (cardFaces == null || cardFaces.Length == 0)
                {
                    throw new FaceNotDetectedException("Không phát hiện được khuôn mặt trong ảnh vi mạch thẻ CCCD.");
                }

                var cardPoints = _landmarker.Mark(cardBitmap, cardFaces[0]);

                // 4. Trích xuất đặc trưng và so khớp
                var probeFeatures = _recognizer.Extract(probeBitmap, probePoints);
                var cardFeatures = _recognizer.Extract(cardBitmap, cardPoints);

                float similarityScore = _recognizer.Compare(probeFeatures, cardFeatures);
                bool isMatch = similarityScore >= (float)threshold;

                string message;
                if (!isMatch)
                {
                    message = "Khuôn mặt người trước camera không khớp với ảnh trong thẻ CCCD.";
                }
                else if (!isLive)
                {
                    message = "Khuôn mặt trùng khớp nhưng cảnh báo giả mạo (không vượt qua kiểm tra người thật).";
                }
                else
                {
                    message = "Xác thực khuôn mặt người thật và so khớp thẻ CCCD thành công.";
                }

                _logger.LogInformation("Kết quả so khớp: Similarity={Similarity}, IsMatch={IsMatch}, IsLive={IsLive}, Status={Status}",
                    similarityScore, isMatch, isLive, livenessStatusStr);

                return Task.FromResult(new FaceVerifyResultDto
                {
                    IsMatch = isMatch,
                    Similarity = Math.Round((double)similarityScore, 4),
                    IsLive = isLive,
                    LivenessStatus = livenessStatusStr,
                    CapturedFaceImageBase64 = probeImageBase64,
                    Message = message
                });
            }
            finally
            {
                probeBitmap.Dispose();
                cardBitmap.Dispose();
            }
        }
    }

    public async Task<FaceVerifyResultDto> CaptureAndVerifyAsync(
        string cardImageBase64,
        double threshold = 0.62,
        CancellationToken cancellationToken = default)
    {
        var capturedBase64 = await CaptureFrameBase64Async(cancellationToken);
        return await VerifyAsync(capturedBase64, cardImageBase64, threshold, cancellationToken);
    }

    private static SKBitmap DecodeBase64Image(string base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new ArgumentException("Chuỗi ảnh base64 không được để trống.");
        }

        // Loại bỏ data URI prefix nếu có (data:image/jpeg;base64,...)
        var cleanBase64 = base64;
        var commaIndex = cleanBase64.IndexOf(',');
        if (commaIndex >= 0 && cleanBase64.Substring(0, commaIndex).Contains("base64"))
        {
            cleanBase64 = cleanBase64[(commaIndex + 1)..];
        }

        try
        {
            var bytes = Convert.FromBase64String(cleanBase64.Trim());
            var bitmap = SKBitmap.Decode(bytes);
            if (bitmap == null)
            {
                throw new InvalidOperationException("Không thể giải mã dữ liệu hình ảnh từ base64.");
            }
            return bitmap;
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("Định dạng chuỗi base64 không hợp lệ: " + ex.Message, ex);
        }
    }

    public void Dispose()
    {
        _detector.Dispose();
        _landmarker.Dispose();
        _recognizer.Dispose();
        _antiSpoofing.Dispose();
    }
}
