namespace CCCDReaderService.Models;

public class CitizenCardDto
{
    public string CardNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Nationality { get; set; } = "Việt Nam";
    public string Hometown { get; set; } = string.Empty;
    public string PermanentAddress { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;
    public string ExpiryDate { get; set; } = string.Empty;
    public string? FaceImageBase64 { get; set; }
}
