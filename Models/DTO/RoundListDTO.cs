using CVexplorer.Models.Domain;

namespace CVexplorer.Models.DTO
{
    public class RoundListDTO
    {
        public required string PublicId { get; set; }
        public required string Name { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
