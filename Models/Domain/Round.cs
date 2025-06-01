namespace CVexplorer.Models.Domain
{
    public class Round
    {
        public int Id { get; set; }

        public required string PublicId { get; set; }
        public required string Name { get; set; } = string.Empty;

        public required Guid PositionId { get; set; }
        public Position Position { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  
        public ICollection<RoundEntry> RoundEntries { get; set; } = new List<RoundEntry>();

        public ICollection<IntegrationSubscription>? IntegrationSubscriptions { get; set; } = new List<IntegrationSubscription>();

    }
}