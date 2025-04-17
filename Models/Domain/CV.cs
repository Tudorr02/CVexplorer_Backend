using System.ComponentModel.DataAnnotations;

namespace CVexplorer.Models.Domain
{
    public class CV
    {
        public int Id { get; set; }

        public Guid PublicId { get; set; } = Guid.NewGuid();

        public required Position Position { get; set; }
        public required Guid PositionId { get; set; }
        public  string? FileName { get; set; }
        public required string ContentType { get; set; }
        public required byte[] Data { get; set; } = null!;
        public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
        public User? UserUploadedBy { get; set; }
        public int? UserUploadedById { get; set; }
        //public double Score { get; set; } = 0;

        //public int? EvaluationId { get; set; } 
        //public CvEvaluationResult? Evaluation { get; set; }
    }
}
