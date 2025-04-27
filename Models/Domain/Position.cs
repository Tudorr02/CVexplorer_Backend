using CVexplorer.Enums;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Models.Domain;

public class Position
{
    public required Guid Id { get; set; }
    public required string PublicId { get; set; } 
    public required string Name { get; set; }
    public required int DepartmentId { get; set; }
    public required Department Department { get; set; }
    public List<string> RequiredSkills { get; set; } = [];
    public List<string> NiceToHave { get; set; } = [];
    public List<string> Languages { get; set; } = [];
    public List<string> Certification { get; set; } = [];
    public List<string> Responsibilities { get; set; } = [];
    public int MinimumExperienceMonths { get; set; } = 0;
    public PositionLevel Level { get; set; } = PositionLevel.Intern;
    public EducationLevel MinimumEducationLevel { get; set; } = EducationLevel.HighSchool;

    public ScoreWeights Weights { get; set; } = new();
}

