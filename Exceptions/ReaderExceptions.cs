namespace CCCDReaderService.Exceptions;

public class DeviceNotConnectedException : Exception
{
    public DeviceNotConnectedException(string message = "Thiết bị đầu đọc thẻ chưa được kết nối vào cổng USB máy tính.")
        : base(message)
    {
    }
}

public class CardNotFoundException : Exception
{
    public CardNotFoundException(string message = "Không phát hiện thấy thẻ CCCD trong đầu đọc.")
        : base(message)
    {
    }
}

public class CardReadException : Exception
{
    public CardReadException(string message)
        : base(message)
    {
    }

    public CardReadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
