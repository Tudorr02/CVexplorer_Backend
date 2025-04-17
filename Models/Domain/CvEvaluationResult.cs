using CVexplorer.Enums;
using CVexplorer.Models.Primitives;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Models.Domain
{
    public class CvEvaluationResult
    {
        public int Id { get; set; }

        public required int CvId { get; set; }
        public CV Cv { get; set; } 

        public string CandidateName { get; set; } = "Not found";

        public CvScoreScrapedField<List<string>> RequiredSkills { get; set; } = new();
        public CvScoreScrapedField<List<string>> NiceToHave { get; set; } = new();
        public CvScoreValueField<List<string>> Languages { get; set; } = new();
        public CvScoreScrapedField<List<string>> Certifications { get; set; } = new();
        public CvScoreScrapedField<List<string>> Responsibilities { get; set; } = new();
        public CvScoreValueField<double> MinimumExperienceMonths { get; set; } = new();
        public CvScoreValueField<PositionLevel> Level { get; set; } = new();
        public CvScoreValueField<EducationLevel> MinimumEducationLevel { get; set; } = new();
        public DateTime EvaluatedAt { get; set; } = DateTime.UtcNow;

    }
}
