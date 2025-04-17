using CVexplorer.Enums;
using System.Text.Json.Serialization;
using CVexplorer.Models.Primitives;
using System.Collections.Generic;

namespace CVexplorer.Models.DTO;

public class CvEvaluationResultDTO
{
    public string CandidateName { get; init; } = "Not found";

    public CvScoreScrapedField<IList<string>> RequiredSkills { get; init; } = new() { Scraped = new List<string>() , Score = 0};
    public CvScoreScrapedField<IList<string>> NiceToHave { get; init; } = new() { Scraped = new List<string>(), Score = 0 };
    public CvScoreValueField<IList<string>> Languages { get; init; } = new() { Value = new List<string>(), Score = 0 };
    public CvScoreScrapedField<IList<string>> Certifications { get; init; } = new() { Scraped = new List<string>(), Score = 0 };
    public CvScoreScrapedField<IList<string>> Responsibilities { get; init; } = new() { Scraped = new List<string>(), Score = 0 };
    public CvScoreValueField<double> MinimumExperienceMonths { get; init; } = new() { Value = 0 , Score = 0 };
    public CvScoreValueField<PositionLevel> Level { get; init; } = new() { Value = PositionLevel.Intern, Score = 0 };
    public CvScoreValueField<EducationLevel> MinimumEducationLevel { get; init; } = new() { Value = EducationLevel.HighSchool, Score = 0 };
}
//public sealed class SkillsResult
//{
//    public IList<string> Scraped { get; init; } = new List<string>();
//    public double Score { get; init; }
//}

//public sealed class LanguagesResult
//{
//    public IList<string> Value { get; init; } = new List<string>();
//    public double Score { get; init; }
//}

//public sealed class ExperienceResult
//{
//    public double Value { get; init; }
//    public double Score { get; init; }
//}

//public sealed class LevelResult
//{
//    [JsonConverter(typeof(JsonStringEnumConverter))]
//    public PositionLevel Value { get; init; }
//    public double Score { get; init; }
//}

//public sealed class EducationResult
//{
//    [JsonConverter(typeof(JsonStringEnumConverter))]
//    public EducationLevel Value { get; init; }
//    public double Score { get; init; }
//}
