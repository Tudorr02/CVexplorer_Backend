using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class DepartmentManagementRepository(DataContext _context) : IDepartmentManagement
    {
        // ✅ Get departments for a user (filters by `UserDepartmentAccess`)
        public async Task<List<DepartmentManagementDTO>> GetDepartmentsAsync(string companyName, int userId, bool hrLeader)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .ThenInclude(d => d.UserDepartmentAccesses) // ✅ Include access control
                .Include(c => c.Departments)
                .ThenInclude(d => d.Positions) // ✅ Include positions
                .FirstOrDefaultAsync(c => c.Name == companyName);

            if (company == null) throw new NotFoundException("Company not found!");



            // ✅ If NOT HR Leader, filter by `UserDepartmentAccess`
            var accessibleDepartments = hrLeader
                ? company.Departments // ✅ HR Leaders see ALL departments
                : company.Departments.Where(d => d.UserDepartmentAccesses.Any(uda => uda.UserId == userId)) // ✅ Regular users see only accessible departments
                .ToList();

            return accessibleDepartments.Select(d => new DepartmentManagementDTO
            {
                Name = d.Name,
                CompanyName = company.Name,
                Positions = d.Positions.Select(p => p.Name).ToList()
            }).ToList();
        }

        // ✅ Get a specific department within a company
        public async Task<DepartmentManagementDTO?> GetDepartmentAsync(string companyName, string departmentName)
        {
            var department = await _context.Departments
                .Include(d => d.Company)
                .Include(d => d.Positions) // ✅ Include positions
                .FirstOrDefaultAsync(d => d.Name == departmentName && d.Company.Name == companyName);

            if (department == null) throw new NotFoundException("Department not found!");

            return new DepartmentManagementDTO
            {
                Name = department.Name,
                CompanyName = department.Company.Name,
                Positions = department.Positions.Select(p => p.Name).ToList()
            };
        }

        // ✅ Create a department within a company
        public async Task<DepartmentManagementDTO?> CreateDepartmentAsync(string companyName, string departmentName)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .FirstOrDefaultAsync(c => c.Name == companyName);

            if (company == null) throw new NotFoundException("Company not found!");

            var department = new Department
            {
                Name = departmentName,
                CompanyId = company.Id,
                Company = company
             
            };

            await _context.Departments.AddAsync(department);
            await _context.SaveChangesAsync();

            return new DepartmentManagementDTO
            {
                Name = department.Name,
                CompanyName = department.Company.Name
                
            };
        }

        // ✅ Update a department within a company
        public async Task<DepartmentManagementDTO?> UpdateDepartmentAsync(string companyName, string departmentName, DepartmentManagementDTO dto)
        {
            var department = await _context.Departments
                .Include(d => d.Company)
                .Include(d => d.Positions)
                .FirstOrDefaultAsync(d => d.Name == departmentName && d.Company.Name == companyName);

            if (department == null) throw new NotFoundException("Department not found!");

            department.Name = dto.Name;

            // ✅ Update positions
            //department.Positions.Clear();
            //department.Positions = dto.Positions.Select(p => new Position { Name = p }).ToList();

            _context.Departments.Update(department);
            await _context.SaveChangesAsync();

            return new DepartmentManagementDTO
            {
                Name = department.Name,
                CompanyName = department.Company.Name,
                //Positions = department.Positions.Select(p => p.Name).ToList()
            };
        }

        // ✅ Delete a department within a company
        public async Task<bool> DeleteDepartmentAsync(string companyName, string departmentName)
        {
            var department = await _context.Departments
                .Include(d => d.Company)
                .FirstOrDefaultAsync(d => d.Name == departmentName && d.Company.Name == companyName);

            if (department == null) throw new NotFoundException("Department not found!");

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
