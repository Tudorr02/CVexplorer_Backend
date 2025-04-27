using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Models.Domain
{
    [Owned]
    public class ScoreWeights
    {
        public double RequiredSkills { get; set; } = 0.4;
        public double NiceToHave { get; set; } = 0.1;
        public double Languages { get; set; } = 0.1;
        public double Certification { get; set; } = 0.3;
        public double Responsibilities { get; set; } = 0.1;
        public double ExperienceMonths { get; set; } = 0.0;
        public double Level { get; set; } = 0.0;
        public double MinimumEducation { get; set; } = 0.0;
    }
}
