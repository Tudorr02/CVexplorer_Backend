using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface.Admin;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation.Admin
{
    public class CompanyManagementRepository(DataContext _context) : ICompanyManagementRepository
    {
        public async Task<List<CompanyManagementListDTO>> GetCompaniesAsync()
        {
            var companies = await _context.Companies
              .Include(c => c.Departments) // Ensure departments are loaded
              .ToListAsync();

            return companies.Select(c => new CompanyManagementListDTO
            {
                Id = c.Id,
                Name = c.Name,
                Employees = _context.Users.Count(u => u.CompanyId == c.Id),

            }).ToList();
        }
      
        public async Task<CompanyManagementDTO> UpdateCompanyAsync(int companyId, CompanyManagementDTO dto)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) throw new NotFoundException("Company not found !"); 

            bool nameExists = await _context.Companies
                .AnyAsync(c => c.Name.ToLower() == dto.Name.ToLower() && c.Id != companyId);

            if (nameExists)
                throw new ArgumentException($"A company with the name '{dto.Name}' already exists.");


           

            if(dto.Name != company.Name)
            {
                company.Name = dto.Name;

                _context.Companies.Update(company);
                await _context.SaveChangesAsync();
            }

          
            return new CompanyManagementDTO
            {
                Name = company.Name
            };
        }

        public async Task<bool> DeleteCompanyAsync(int companyId)
        {
            var company = await _context.Companies.Include(c => c.Users).FirstOrDefaultAsync(c => c.Id == companyId);
            if (company == null) throw new NotFoundException("Company not found !");

            _context.Companies.Remove(company);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<CompanyManagementDTO> CreateCompanyAsync(CompanyManagementDTO dto)
        {
            var company = new Company
            {
                Name = dto.Name
            };

            await _context.Companies.AddAsync(company);
            await _context.SaveChangesAsync();

            return new CompanyManagementDTO
            {
                Name = company.Name           
            };
        }
    }
}
