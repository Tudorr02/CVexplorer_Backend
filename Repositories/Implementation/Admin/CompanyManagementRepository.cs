using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO.Admin;
using CVexplorer.Repositories.Interface.Admin;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation.Admin
{
    public class CompanyManagementRepository(DataContext _context) : ICompanyManagement
    {
        public async Task<List<CompanyManagementDTO>> GetCompaniesAsync()
        {
            var companies = await _context.Companies
              .Include(c => c.Departments) // Ensure departments are loaded
              .ToListAsync();

            return companies.Select(c => new CompanyManagementDTO
            {
                Name = c.Name,
          
            }).ToList();
        }
        public async Task<CompanyManagementDTO> GetCompanyAsync(string companyName)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .FirstOrDefaultAsync(c => c.Name == companyName);

            if (company == null) throw new NotFoundException("Company not found !");

            return new CompanyManagementDTO
            {
                Name = company.Name
            };
        }
        // ✅ Update a company
        public async Task<CompanyManagementDTO> UpdateCompanyAsync(string companyName, CompanyManagementDTO dto)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .FirstOrDefaultAsync(c => c.Name == companyName);

            if (company == null) throw new NotFoundException("Company not found !"); // ✅ Return null if company doesn't exist

            // ✅ Update the company name
            company.Name = dto.Name;

            _context.Companies.Update(company);
            await _context.SaveChangesAsync();

            // ✅ Return the updated company details
            return new CompanyManagementDTO
            {
                Name = company.Name
            };
        }
        // ✅ Delete a company
        public async Task<bool> DeleteCompanyAsync(string companyName)
        {
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Name == companyName);
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
