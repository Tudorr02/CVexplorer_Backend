using CVexplorer.Data;
using CVexplorer.Exceptions;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Repositories.Implementation
{
    public class DepartmentRepository(DataContext _context) : IDepartmentRepository
    {
        // ✅ Get departments for a user (filters by `UserDepartmentAccess`)
        public async Task<List<DepartmentListDTO>> GetDepartmentsAsync(int companyId,int userId,bool isHrLeader = false)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .ThenInclude(d => d.UserDepartmentAccesses) // ✅ Include access control
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) throw new NotFoundException("Company not found!");



            // ✅ If NOT HR Leader, filter by `UserDepartmentAccess`
            var accessibleDepartments = isHrLeader
                ? company.Departments // ✅ HR Leaders see ALL departments
                : company.Departments.Where(d => d.UserDepartmentAccesses.Any(uda => uda.UserId == userId)) // ✅ Regular users see only accessible departments
                .ToList();

            return accessibleDepartments.Select(d => new DepartmentListDTO
            {
                Id = d.Id,
                Name = d.Name
            }).ToList();
        }

      
        // ✅ Create a department within a company
        public async Task<DepartmentListDTO> CreateDepartmentAsync(int companyId, string departmentName)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) throw new NotFoundException("Company not found!");

            var department = new Department
            {
                Name = departmentName,
                CompanyId = company.Id,
                Company = company
             
            };

            await _context.Departments.AddAsync(department);
            await _context.SaveChangesAsync();

            return new DepartmentListDTO
            {
                Id = department.Id,
                Name = department.Name
            };
        }

        // ✅ Update a department within a company
        public async Task<DepartmentDTO?> UpdateDepartmentAsync(int departmentID, DepartmentDTO dto)
        {
            var department = await _context.Departments
                .Include(d => d.Company)
                .Include(d => d.Positions)
                .FirstOrDefaultAsync(d => d.Id == departmentID);

            if (department == null) throw new NotFoundException("Department not found!");

            department.Name = dto.Name;

            // ✅ Update positions
            //department.Positions.Clear();
            //department.Positions = dto.Positions.Select(p => new Position { Name = p }).ToList();

            _context.Departments.Update(department);
            await _context.SaveChangesAsync();

            return new DepartmentDTO
            {
                Name = department.Name,
                
                //Positions = department.Positions.Select(p => p.Name).ToList()
            };
        }

        // ✅ Delete a department within a company
        public async Task<bool> DeleteDepartmentAsync(int departmentId)
        {
            var department = await _context.Departments
                .Include(d => d.Company)
                .FirstOrDefaultAsync(d => d.Id == departmentId);

            if (department == null) throw new NotFoundException("Department not found!");

            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<DepartmentTreeNodeDTO>> GetDepartmentsTreeAsync(int companyId, int userId, bool isHrLeader = false)
        {
            var company = await _context.Companies
                 .Include(c => c.Departments)
                 .ThenInclude(d => d.UserDepartmentAccesses) // ✅ Include access control
                 .Include(c => c.Departments)
                 .ThenInclude(d => d.Positions)
                 .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) throw new NotFoundException("Company not found!");



            // ✅ If NOT HR Leader, filter by `UserDepartmentAccess`
            var accessibleDepartments = isHrLeader
                ? company.Departments // ✅ HR Leaders see ALL departments
                : company.Departments.Where(d => d.UserDepartmentAccesses.Any(uda => uda.UserId == userId)) // ✅ Regular users see only accessible departments
                .ToList();

            return accessibleDepartments.Select(d => new DepartmentTreeNodeDTO
            {
                Id = d.Id,
                Name = d.Name,
                Positions = d.Positions.Select(p => new PositionTreeNodeDTO
                {
                    Id = p.Id,
                    Name = p.Name
                }).ToList()
            }).ToList();
        }
    }
}
