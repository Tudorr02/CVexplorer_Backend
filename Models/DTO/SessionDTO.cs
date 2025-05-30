namespace CVexplorer.Models.DTO
{
    public class SessionDTO
    {
        public int ProcessedCVs { get; set; }
        public string   Expiry { get; set; }

        public bool IsProcessing { get; set; }

        public string? ProcessingRoundId { get; set; }
    }
}
