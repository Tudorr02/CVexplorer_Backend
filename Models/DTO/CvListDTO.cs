namespace CVexplorer.Models.DTO
{
    public class CvListDTO
    {
        public Guid PublicId { get; set; }
        public string FileName { get; set; } 
        public string UploadedAt { get; set; } 
        public string? UploadedBy { get; set; }
    }
}
