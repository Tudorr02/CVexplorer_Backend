using CVexplorer.Enums;

namespace CVexplorer.Models.DTO
{
    public class PositionListDTO
    {
        public required string PublicId { get; set; } = string.Empty;
        public required string Name { get; set; } = string.Empty;
        public List<string> RequiredSkills { get; set; } = [];
        public List<string> NiceToHave { get; set; } = [];
        public List<string> Languages { get; set; } = [];
        public List<string> Certifications { get; set; } = [];
        public List<string> Responsibilities { get; set; } = [];
        public int MinimumExperienceMonths { get; set; } = 0;
        public PositionLevel Level { get; set; } = PositionLevel.Intern;
        public EducationLevel MinimumEducationLevel { get; set; } = EducationLevel.HighSchool;
    }
}
