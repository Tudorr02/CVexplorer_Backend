using CVexplorer.Data;
using CVexplorer.Enums;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Models.Primitives;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class RoundEntryRepository(DataContext _context, IPositionRepository _posRepository) : IRoundEntryRepository
    {
        public async Task<IEnumerable<RoundEntryListDTO>> GetAllAsync(string roundId)
        {
            return await _context.RoundEntries
                    .Include(re => re.Cv)
                    .Include(re => re.Round)
                   .Where(re => re.Round.PublicId == roundId)
                   .Select(re => new RoundEntryListDTO
                   {
                       Id = re.Id,
                       CandidateName = re.Cv.Evaluation.CandidateName,
                       Score = Convert.ToInt16(re.Cv.Score),
                       Selected = re.Selected
                   })
                   .OrderByDescending(r=>r.Score)
                   .ToListAsync();
        }

        public async Task<CvEvaluationDTO> GetRoundEntryAsync(int reId)
        {
            var rEntry = await _context.RoundEntries
                .Include(re => re.Cv)
                .ThenInclude(cv => cv.Evaluation)
                .Include(re => re.Round)
                .ThenInclude(r => r.Position)
                .FirstOrDefaultAsync(re => re.Id == reId);

            return new CvEvaluationDTO
            {
                FileData =Convert.ToBase64String(rEntry.Cv.Data),
                Score = Convert.ToInt16(rEntry.Cv.Score),
                cvPublicId = rEntry.Cv.PublicId.ToString(),
                Evaluation = new CvEvaluationResultDTO
                {
                    CandidateName = rEntry.Cv.Evaluation.CandidateName,
                    RequiredSkills = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = rEntry.Cv.Evaluation.RequiredSkills.Scraped.ToList(),
                        Score = rEntry.Cv.Evaluation.RequiredSkills.Score
                    },
                    NiceToHave = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = rEntry.Cv.Evaluation.NiceToHave.Scraped.ToList(),
                        Score = rEntry.Cv.Evaluation.NiceToHave.Score
                    },
                    Certifications = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = rEntry.Cv.Evaluation.Certifications.Scraped.ToList(),
                        Score = rEntry.Cv.Evaluation.Certifications.Score
                    },
                    Responsibilities = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = rEntry.Cv.Evaluation.Responsibilities.Scraped.ToList(),
                        Score = rEntry.Cv.Evaluation.Responsibilities.Score
                    },
                    Languages = new CvScoreValueField<IList<string>>
                    {
                        Value = rEntry.Cv.Evaluation.Languages.Value.ToList(),
                        Score = rEntry.Cv.Evaluation.Languages.Score
                    },
                    MinimumExperienceMonths = new CvScoreValueField<double>
                    {
                        Value = rEntry.Cv.Evaluation.MinimumExperienceMonths.Value,
                        Score = rEntry.Cv.Evaluation.MinimumExperienceMonths.Score
                    },
                    Level = new CvScoreValueField<PositionLevel>
                    {
                        Value = rEntry.Cv.Evaluation.Level.Value,
                        Score = rEntry.Cv.Evaluation.Level.Score
                    },
                    MinimumEducationLevel = new CvScoreValueField<EducationLevel>
                    {
                        Value = rEntry.Cv.Evaluation.MinimumEducationLevel.Value,
                        Score = rEntry.Cv.Evaluation.MinimumEducationLevel.Score
                    }
                },
                PositionData = _posRepository.GetPositionAsync(rEntry.Round.Position.PublicId).Result
            };
        }

        public async Task CreateAsync (int roundId , int cvId)
        {
            _context.RoundEntries.Add( new RoundEntry { RoundId = roundId, CvId = cvId });
            await _context.SaveChangesAsync();
        }

        public async Task<bool> UpdateAsync(int reId, bool selected)
        {
            var roundEntry = await _context.RoundEntries.FindAsync(reId);
            if (roundEntry == null) return false;
            roundEntry.Selected = selected;
            _context.RoundEntries.Update(roundEntry);
            await _context.SaveChangesAsync();
            return true;
        }


    }
}
