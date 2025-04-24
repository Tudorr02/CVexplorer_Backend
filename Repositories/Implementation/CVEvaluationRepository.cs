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

            return new CvEvaluationResult
            {

                CandidateName = evalDto.CandidateName,

                RequiredSkills = new CvScoreScrapedField<List<string>>
                {
                    Scraped = evalDto.RequiredSkills.Scraped.ToList(),
                    Score = evalDto.RequiredSkills.Score
                },
                NiceToHave = new CvScoreScrapedField<List<string>>
                {
                    Scraped = evalDto.NiceToHave.Scraped.ToList(),
                    Score = evalDto.NiceToHave.Score
                },
                Certifications = new CvScoreScrapedField<List<string>>
                {
                    Scraped = evalDto.Certifications.Scraped.ToList(),
                    Score = evalDto.Certifications.Score
                },
                Responsibilities = new CvScoreScrapedField<List<string>>
                {
                    Scraped = evalDto.Responsibilities.Scraped.ToList(),
                    Score = evalDto.Responsibilities.Score
                },

                Languages = new CvScoreValueField<List<string>>
                {
                    Value = evalDto.Languages.Value.ToList(),
                    Score = evalDto.Languages.Score
                },
                MinimumExperienceMonths = new CvScoreValueField<double>
                {
                    Value = evalDto.MinimumExperienceMonths.Value,
                    Score = evalDto.MinimumExperienceMonths.Score
                },
                Level = new CvScoreValueField<PositionLevel>
                {
                    Value = evalDto.Level.Value,
                    Score = evalDto.Level.Score
                },
                MinimumEducationLevel = new CvScoreValueField<EducationLevel>
                {
                    Value = evalDto.MinimumEducationLevel.Value,
                    Score = evalDto.MinimumEducationLevel.Score
                }
            };
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

    }
}
