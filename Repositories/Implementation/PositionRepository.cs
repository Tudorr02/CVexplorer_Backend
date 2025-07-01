using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class PositionRepository(DataContext _context) : IPositionRepository
    {

        public async Task<PositionDTO> GetPositionAsync(string publicId)
        {
            var position = await _context.Positions.FirstOrDefaultAsync(p => p.PublicId == publicId);
            if (position == null) throw new Exception("Position not found");

            return new PositionDTO
            {
                Name = position.Name,
                RequiredSkills = position.RequiredSkills,
                NiceToHave = position.NiceToHave,
                Languages = position.Languages,
                Certifications = position.Certification,
                Responsibilities = position.Responsibilities,
                MinimumExperienceMonths = position.MinimumExperienceMonths,
                Level = position.Level,
                MinimumEducationLevel = position.MinimumEducationLevel,
                Weights = new ScoreWeightsDTO
                {
                    RequiredSkills = position.Weights.RequiredSkills,
                    NiceToHave = position.Weights.NiceToHave,
                    Languages = position.Weights.Languages,
                    Certifications = position.Weights.Certification,
                    Responsibilities = position.Weights.Responsibilities,
                    ExperienceMonths = position.Weights.ExperienceMonths,
                    Level = position.Weights.Level,
                    MinimumEducation = position.Weights.MinimumEducation
                }
            };
        }

        public async Task<PositionListDTO> CreatePositionAsync(int departmentId ,PositionDTO dto)
        {
            var position = new Position
            {
                Id = Guid.NewGuid(),
                PublicId =String.Format("{0}-{1}", dto.Name.Split(" ")[0], Guid.NewGuid().ToString().Substring(0, 6)),
                DepartmentId = departmentId,
                Department = await _context.Departments.FindAsync(departmentId),
                Name = dto.Name,
                RequiredSkills = dto.RequiredSkills,
                NiceToHave = dto.NiceToHave,
                Languages = dto.Languages,
                Certification = dto.Certifications,
                Responsibilities = dto.Responsibilities,
                MinimumExperienceMonths = dto.MinimumExperienceMonths,
                Level = dto.Level,
                MinimumEducationLevel = dto.MinimumEducationLevel,
                Weights = new ScoreWeights
                {
                    RequiredSkills = dto.Weights.RequiredSkills,
                    NiceToHave = dto.Weights.NiceToHave,
                    Languages = dto.Weights.Languages,
                    Certification = dto.Weights.Certifications,
                    Responsibilities = dto.Weights.Responsibilities,
                    ExperienceMonths = dto.Weights.ExperienceMonths,
                    Level = dto.Weights.Level,
                    MinimumEducation = dto.Weights.MinimumEducation
                }
            };

            _context.Positions.Add(position);
            await _context.SaveChangesAsync();

            return new PositionListDTO
            {
                PublicId = position.PublicId,
                Name = position.Name
                
            };
        }

        public async Task<bool> UpdatePositionAsync(string publicId, PositionDTO dto)
        {
            var position = await _context.Positions.FirstOrDefaultAsync(p => p.PublicId == publicId);
            if (position == null) return false;

            position.Name = dto.Name;
            position.RequiredSkills = dto.RequiredSkills;
            position.NiceToHave = dto.NiceToHave;
            position.Languages = dto.Languages;
            position.Certification = dto.Certifications;
            position.Responsibilities = dto.Responsibilities;
            position.MinimumExperienceMonths = dto.MinimumExperienceMonths;
            position.Level = dto.Level;
            position.MinimumEducationLevel = dto.MinimumEducationLevel;

            position.Weights.RequiredSkills = dto.Weights.RequiredSkills;
            position.Weights.NiceToHave = dto.Weights.NiceToHave;
            position.Weights.Languages = dto.Weights.Languages;
            position.Weights.Certification = dto.Weights.Certifications;
            position.Weights.Responsibilities = dto.Weights.Responsibilities;
            position.Weights.ExperienceMonths = dto.Weights.ExperienceMonths;
            position.Weights.Level = dto.Weights.Level;
            position.Weights.MinimumEducation = dto.Weights.MinimumEducation;

            _context.Positions.Update(position);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePositionAsync(string publicId)
        {
            var position = await _context.Positions.FirstOrDefaultAsync(p => p.PublicId == publicId);
            if (position == null) return false;

            bool hasSubs = await _context.IntegrationSubscriptions
                .AnyAsync(s => s.PositionId == position.Id);


            if(hasSubs)
                throw new InvalidOperationException("Cannot delete position with active subscriptions");

            _context.Positions.Remove(position);
            await _context.SaveChangesAsync();
            return true;
        }


    }
}
