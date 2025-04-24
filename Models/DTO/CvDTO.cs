namespace CVexplorer.Models.DTO
{
    public class CvDTO
    {
        public string FileName { get; set; } = null!;
        public string UploadedAt { get; set; }
        public string? UploadedBy { get; set; }
        public byte[] FileData { get; set; } = null!;

    }
}
