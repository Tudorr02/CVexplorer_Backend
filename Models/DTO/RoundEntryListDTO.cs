namespace CVexplorer.Models.DTO
{
    public class RoundEntryListDTO
    {
        public required int Id { get; set; }
        public required string CandidateName { get; set; }
        public int Score { get; set; }
        public Guid PublicCvId { get; set; }
        public string Details { get; set; } = null;

    }
}
