using CVexplorer.Data;
using CVexplorer.Enums;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Models.Primitives;
using CVexplorer.Repositories.Interface;
using CVexplorer.Services.Interface;
using iText.StyledXmlParser.Jsoup.Select;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class CVEvaluationRepository(DataContext _context , ICvEvaluationService _evaluator) : ICVEvaluationRepository
    {
        public async Task<CvEvaluationResult> CreateAsync(string cvText, Position position)
        {
            CvEvaluationResultDTO evalDto = await _evaluator.EvaluateAsync(cvText, position).ConfigureAwait(false);

            return Map(evalDto);
            
        }

        public async Task<CvEvaluationResultDTO> UpdateAsync(Guid cvPublicId, CvEvaluationResultDTO editDto)
        {
            var cv = await _context.CVs
                .Include(c => c.Evaluation)
                .FirstOrDefaultAsync(c => c.PublicId == cvPublicId);

            var eval = cv.Evaluation;

            if (eval == null)
                throw new KeyNotFoundException($"Evaluation for CV '{cvPublicId}' not found.");

            eval.CandidateName = editDto.CandidateName;

            eval.RequiredSkills.Scraped = editDto.RequiredSkills.Scraped.ToList();
            eval.RequiredSkills.Score = editDto.RequiredSkills.Score;

            eval.NiceToHave.Scraped = editDto.NiceToHave.Scraped.ToList();
            eval.NiceToHave.Score = editDto.NiceToHave.Score;

            eval.Certifications.Scraped = editDto.Certifications.Scraped.ToList();
            eval.Certifications.Score = editDto.Certifications.Score;

            eval.Responsibilities.Scraped = editDto.Responsibilities.Scraped.ToList();
            eval.Responsibilities.Score = editDto.Responsibilities.Score;

            eval.Languages.Value = editDto.Languages.Value.ToList();
            eval.Languages.Score = editDto.Languages.Score;

            eval.MinimumExperienceMonths.Value = editDto.MinimumExperienceMonths.Value;
            eval.MinimumExperienceMonths.Score = editDto.MinimumExperienceMonths.Score;

            eval.Level.Value = editDto.Level.Value;
            eval.Level.Score = editDto.Level.Score;

            eval.MinimumEducationLevel.Value = editDto.MinimumEducationLevel.Value;
            eval.MinimumEducationLevel.Score = editDto.MinimumEducationLevel.Score;

            _context.CvEvaluationResults.Update(eval);
            await _context.SaveChangesAsync();


            // Return updated DTO
            return new CvEvaluationResultDTO
            {
                CandidateName = eval.CandidateName,
                RequiredSkills = new CvScoreScrapedField<IList<string>> { Scraped = eval.RequiredSkills.Scraped, Score = eval.RequiredSkills.Score },
                NiceToHave = new CvScoreScrapedField<IList<string>> { Scraped = eval.NiceToHave.Scraped, Score = eval.NiceToHave.Score },
                Certifications = new CvScoreScrapedField<IList<string>> { Scraped = eval.Certifications.Scraped, Score = eval.Certifications.Score },
                Responsibilities = new CvScoreScrapedField<IList<string>> { Scraped = eval.Responsibilities.Scraped, Score = eval.Responsibilities.Score },
                Languages = new CvScoreValueField<IList<string>> { Value = eval.Languages.Value, Score = eval.Languages.Score },
                MinimumExperienceMonths = new CvScoreValueField<double> { Value = eval.MinimumExperienceMonths.Value, Score = eval.MinimumExperienceMonths.Score },
                Level = new CvScoreValueField<PositionLevel> { Value = eval.Level.Value, Score = eval.Level.Score },
                MinimumEducationLevel = new CvScoreValueField<EducationLevel> { Value = eval.MinimumEducationLevel.Value, Score = eval.MinimumEducationLevel.Score }

            };
        }

        public async Task<IReadOnlyList<CvEvaluationResult>> CreateBulkAsync(List<string> cvTexts,Position position)
        {
            var evalDtos = await _evaluator
                .BulkEvaluateAsync(cvTexts, position)
                .ConfigureAwait(false);

           
            var results = new List<CvEvaluationResult>(evalDtos.Count);

            foreach (var dto in evalDtos)
                results.Add(Map(dto));

            return results;
        }

        private static CvEvaluationResult Map(CvEvaluationResultDTO dto) => new()
        {
            CandidateName = dto.CandidateName,

            RequiredSkills = new CvScoreScrapedField<List<string>>
            {
                Scraped = dto.RequiredSkills.Scraped.ToList(),
                Score = dto.RequiredSkills.Score
            },
            NiceToHave = new CvScoreScrapedField<List<string>>
            {
                Scraped = dto.NiceToHave.Scraped.ToList(),
                Score = dto.NiceToHave.Score
            },
            Certifications = new CvScoreScrapedField<List<string>>
            {
                Scraped = dto.Certifications.Scraped.ToList(),
                Score = dto.Certifications.Score
            },
            Responsibilities = new CvScoreScrapedField<List<string>>
            {
                Scraped = dto.Responsibilities.Scraped.ToList(),
                Score = dto.Responsibilities.Score
            },

            Languages = new CvScoreValueField<List<string>>
            {
                Value = dto.Languages.Value.ToList(),
                Score = dto.Languages.Score
            },
            MinimumExperienceMonths = new CvScoreValueField<double>
            {
                Value = dto.MinimumExperienceMonths.Value,
                Score = dto.MinimumExperienceMonths.Score
            },
            Level = new CvScoreValueField<PositionLevel>
            {
                Value = dto.Level.Value,
                Score = dto.Level.Score
            },
            MinimumEducationLevel = new CvScoreValueField<EducationLevel>
            {
                Value = dto.MinimumEducationLevel.Value,
                Score = dto.MinimumEducationLevel.Score
            }
        };

    }
}
