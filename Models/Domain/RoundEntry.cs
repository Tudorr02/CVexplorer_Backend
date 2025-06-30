
namespace CVexplorer.Models.Domain
{
    public class RoundEntry
    {
        public int Id { get; set; }
        public int StageId { get; set; }
        public RoundStage Stage { get; set; } = null!;

        public required int CvId { get; set; }
        public CV Cv { get; set; }

        public string Details { get; set; } = string.Empty;

    }
}