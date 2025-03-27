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
                MinimumEducationLevel = position.MinimumEducationLevel
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
                MinimumEducationLevel = dto.MinimumEducationLevel
            };

            _context.Positions.Add(position);
            await _context.SaveChangesAsync();

            return new PositionListDTO
            {
                PublicId = position.PublicId,
                Name = position.Name,
                RequiredSkills = position.RequiredSkills,
                NiceToHave = position.NiceToHave,
                Languages = position.Languages,
                Certifications = position.Certification,
                Responsibilities = position.Responsibilities,
                MinimumExperienceMonths = position.MinimumExperienceMonths,
                Level = position.Level,
                MinimumEducationLevel = position.MinimumEducationLevel
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

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> DeletePositionAsync(string publicId)
        {
            var position = await _context.Positions.FirstOrDefaultAsync(p => p.PublicId == publicId);
            if (position == null) return false;

            _context.Positions.Remove(position);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
