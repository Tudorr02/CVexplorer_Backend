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
    public class CVEvaluationRepository(DataContext _context , ICvEvaluationService _evaluator , IPositionRepository _posRepository) : ICVEvaluationRepository
    {
        public async Task<CvEvaluationResult> CreateAsync(string cvText, Position position)
        {
            CvEvaluationResultDTO evalDto = await _evaluator.EvaluateAsync(cvText, position).ConfigureAwait(false);
            var evaluation = Map(evalDto);

            await _context.CvEvaluationResults.AddAsync(evaluation);
            await _context.SaveChangesAsync();
            return evaluation;
            
        }

        public async Task<CvEvaluationDTO> GetEvaluationAsync(Guid publicId)
        {
            var cv = await _context.CVs
                .Include(c => c.Evaluation)
                .Include(r => r.Position)
                .FirstOrDefaultAsync(c => c.PublicId == publicId);
            if (cv == null) return null;

            return new CvEvaluationDTO
            {
                FileData = Convert.ToBase64String(cv.Data),
                Score = Convert.ToInt16(cv.Score),
                Evaluation = new CvEvaluationResultDTO
                {
                    CandidateName = cv.Evaluation.CandidateName,
                    RequiredSkills = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = cv.Evaluation.RequiredSkills.Scraped.ToList(),
                        Score = cv.Evaluation.RequiredSkills.Score
                    },
                    NiceToHave = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = cv.Evaluation.NiceToHave.Scraped.ToList(),
                        Score = cv.Evaluation.NiceToHave.Score
                    },
                    Certifications = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = cv.Evaluation.Certifications.Scraped.ToList(),
                        Score = cv.Evaluation.Certifications.Score
                    },
                    Responsibilities = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = cv.Evaluation.Responsibilities.Scraped.ToList(),
                        Score = cv.Evaluation.Responsibilities.Score
                    },
                    Languages = new CvScoreValueField<IList<string>>
                    {
                        Value = cv.Evaluation.Languages.Value.ToList(),
                        Score = cv.Evaluation.Languages.Score
                    },
                    MinimumExperienceMonths = new CvScoreValueField<double>
                    {
                        Value = cv.Evaluation.MinimumExperienceMonths.Value,
                        Score = cv.Evaluation.MinimumExperienceMonths.Score
                    },
                    Level = new CvScoreValueField<PositionLevel>
                    {
                        Value = cv.Evaluation.Level.Value,
                        Score = cv.Evaluation.Level.Score
                    },
                    MinimumEducationLevel = new CvScoreValueField<EducationLevel>
                    {
                        Value = cv.Evaluation.MinimumEducationLevel.Value,
                        Score = cv.Evaluation.MinimumEducationLevel.Score
                    }
                },
                PositionData = _posRepository.GetPositionAsync(cv.Position.PublicId).Result
            };
        }

        public async Task<CvEvaluationResultDTO> UpdateAsync(Guid cvPublicId, CvEvaluationResultDTO editDto)
        {
            var cv = await _context.CVs
                .Include(c => c.Evaluation)
                .Include(cv => cv.Position)
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

            cv.Score =  CalculateScore(eval, cv.Position.Weights);
            await _context.SaveChangesAsync();

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


            var evaluations = evalDtos.Select(dto => Map(dto)).ToList();
            await _context.CvEvaluationResults.AddRangeAsync(evaluations);
            await _context.SaveChangesAsync();
            return evaluations;
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

        private double CalculateScore(CvEvaluationResult eval, ScoreWeights weights)
        {
            return Math.Round(
            ((weights.RequiredSkills / 100) * eval.RequiredSkills.Score +
            (weights.NiceToHave / 100) * eval.NiceToHave.Score +
            (weights.Languages / 100) * eval.Languages.Score +
            (weights.Certification / 100) * eval.Certifications.Score +
            (weights.Responsibilities / 100) * eval.Responsibilities.Score +
            (weights.ExperienceMonths / 100) * eval.MinimumExperienceMonths.Score +
            (weights.Level / 100) * eval.Level.Score +
            (weights.MinimumEducation / 100) * eval.MinimumEducationLevel.Score), 0, MidpointRounding.AwayFromZero);
        }

    }
}
