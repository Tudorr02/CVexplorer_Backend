namespace CVexplorer.Models.DTO
{
    public class CvListDTO
    {
        public Guid PublicId { get; set; }
        public string FileName { get; set; } 
        public DateTime UploadedAt { get; set; } 
        public string? UploadedBy { get; set; }

        public int Score { get; set; }
    }
}
