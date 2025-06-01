namespace CVexplorer.Models.Domain
{
    public class RoundStage
    {
        public int Id { get; set; }

        public int RoundId { get; set; }
        public Round Round { get; set; } = null!;

        public required string Name { get; set; } = "All Candidates";
        public int Ordinal { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public ICollection<RoundEntry> Entries { get; set; } = new List<RoundEntry>();
    }
}
