namespace CCCDReaderService.Exceptions;

public class CameraNotAvailableException : Exception
{
    public CameraNotAvailableException(string message = "Không tìm thấy thiết bị Camera hoặc Camera đang bị khóa.")
        : base(message)
    {
    }
}

public class FaceNotDetectedException : Exception
{
    public FaceNotDetectedException(string message = "Không phát hiện thấy khuôn mặt nào trong ảnh.")
        : base(message)
    {
    }
}

public class MultipleFacesDetectedException : Exception
{
    public MultipleFacesDetectedException(string message = "Phát hiện nhiều hơn một khuôn mặt trong khung hình.")
        : base(message)
    {
    }
}

public class NoActiveCardSessionException : Exception
{
    public NoActiveCardSessionException(string message = "Chưa có phiên đọc thẻ CCCD nào đang hoạt động.")
        : base(message)
    {
    }
}

public class CardFaceImageMissingException : Exception
{
    public CardFaceImageMissingException(string message = "Dữ liệu thẻ CCCD hiện tại không chứa ảnh chân dung trong chip (DG2).")
        : base(message)
    {
    }
}
