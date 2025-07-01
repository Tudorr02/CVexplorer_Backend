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
        public async Task<List<DepartmentListDTO>> GetDepartmentsAsync(int companyId,int userId,bool isHrLeader = false)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .ThenInclude(d => d.UserDepartmentAccesses)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) throw new NotFoundException("Company not found!");



            var accessibleDepartments = isHrLeader
                ? company.Departments 
                : company.Departments.Where(d => d.UserDepartmentAccesses.Any(uda => uda.UserId == userId)) // ✅ Regular users see only accessible departments
                .ToList();

            return accessibleDepartments.Select(d => new DepartmentListDTO
            {
                Id = d.Id,
                Name = d.Name
            }).ToList();
        }

        public async Task<DepartmentDTO?> GetDepartmentAsync(int departmentId, int companyId)
        {
            var department = await _context.Departments
                .Include(d => d.UserDepartmentAccesses)
                .ThenInclude(uda => uda.User)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId);

            if (department == null) return null;

            var allUsers = await _context.Users
                .Where(u => u.CompanyId == companyId)
                .Join(_context.UserRoles.Where(ur => ur.Role.Name == "HRUser"),
                      user => user.Id,
                      role => role.UserId,
                      (user, role) => user) 
                .GroupJoin(_context.UserDepartmentAccesses.Where(uda => uda.DepartmentId == departmentId),
                           user => user.Id, 
                           access => access.UserId,
                           (user, access) => new DepartmentAccessDTO
                           {
                               UserId = user.Id,
                               UserName = user.UserName,
                               HasAccess = access.Any() 
                           })
                .ToListAsync();

            return new DepartmentDTO
            {
                Name = department.Name,
                DepartmentAccesses = allUsers
            };
           
        }

        public async Task<DepartmentListDTO> CreateDepartmentAsync(int companyId, DepartmentDTO dto)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) throw new NotFoundException("Company not found!");

            if(dto.DepartmentAccesses != null && dto.DepartmentAccesses.Any())
            {
                var userIdsFromDto = dto.DepartmentAccesses?
                    .Where(da => da.HasAccess)
                    .Select(da => da.UserId)
                    .ToList() ?? [];

                var validCompanyUsers = await _context.Users
                    .Join(_context.UserRoles.Where(ur => ur.Role.Name == "HRUser"),
                          user => user.Id,
                          role => role.UserId,
                          (user, role) => user)
                    .Where(u => u.CompanyId == companyId)
                    .Select(u => u.Id)
                    .Distinct()
                    .ToListAsync();

                if (userIdsFromDto.Any(userId => !validCompanyUsers.Contains(userId)))
                {
                    throw new UnauthorizedAccessException("Some users do not belong to this company or do not have the HRUser role.");
                }

            }

            var department = new Department
            {
                Name = dto.Name,
                CompanyId = company.Id,
                Company = company
             
            };

            await _context.Departments.AddAsync(department);
            await _context.SaveChangesAsync();

            if (dto.DepartmentAccesses != null && dto.DepartmentAccesses.Any())
            {
                var accessList = dto.DepartmentAccesses
                    .Where(da => da.HasAccess) 
                    .Select(da => new UserDepartmentAccess
                    {
                        UserId = da.UserId,
                        DepartmentId = department.Id
                    }).ToList();

                _context.UserDepartmentAccesses.AddRange(accessList);
                await _context.SaveChangesAsync();
            }

            return new DepartmentListDTO
            {
                Id = department.Id,
                Name = department.Name
            };
        }

        public async Task<DepartmentDTO> UpdateDepartmentAsync(int departmentId, int companyId,DepartmentDTO dto)
        {
            var department = await _context.Departments
                .Include(d => d.UserDepartmentAccesses)
                .ThenInclude(uda => uda.User)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId);

            if (department == null) throw new NotFoundException("Department not found!");

            var userIdsFromDto = dto.DepartmentAccesses?
                .Where(da => da.HasAccess)
                .Select(da => da.UserId)
                .ToList() ?? [];

            var validCompanyUsers = await _context.Users
                .Join(_context.UserRoles.Where(ur => ur.Role.Name == "HRUser"),
                    user => user.Id,
                    role => role.UserId,
                    (user, role) => user)
                .Where(u => u.CompanyId == companyId)
                .Select(u => u.Id)
                .Distinct() 
                .ToListAsync();

            if (userIdsFromDto.Any(userId => !validCompanyUsers.Contains(userId)))
            {
                throw new UnauthorizedAccessException("Some users do not belong to this company or do not have the HRUser role.");
            }

            department.Name = dto.Name;

            var result = await UpdateDepartmentAccessAsync(departmentId, companyId, userIdsFromDto);

            if (!result) throw new Exception("Failed to update department access.");

            _context.Departments.Update(department);
            await _context.SaveChangesAsync();

            return new DepartmentDTO
            {
                Name = department.Name,
                DepartmentAccesses = validCompanyUsers.Select(userId => new DepartmentAccessDTO
                {
                    UserId = userId,
                    UserName = _context.Users.FirstOrDefault(u => u.Id == userId)?.UserName ?? "Unknown",
                    HasAccess = userIdsFromDto.Contains(userId)
                }).ToList()
            };
        }

        public async Task<bool> DeleteDepartmentAsync(int departmentId)
        {
            var department = await _context.Departments
                .Include(d => d.Company)
                .Include(d => d.Positions)
                .Include(d =>  d.UserDepartmentAccesses)
                .FirstOrDefaultAsync(d => d.Id == departmentId);

            if (department == null) throw new NotFoundException("Department not found!");


            var positionIds = department.Positions.Select(p => p.Id).ToList();

            // 2) verifică dacă există vreo subscription pentru oricare dintre ele
            bool hasSubs = await _context.IntegrationSubscriptions
                .AnyAsync(s => positionIds.Contains(s.PositionId));

            if (hasSubs)
                throw new InvalidOperationException(
                  "Cannot delete department because at least one of its positions has active subscriptions.");


            _context.Departments.Remove(department);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<DepartmentTreeNodeDTO>> GetDepartmentsTreeAsync(int companyId, int userId, bool isHrLeader = false)
        {
            var company = await _context.Companies
                 .Include(c => c.Departments)
                 .ThenInclude(d => d.UserDepartmentAccesses)
                 .Include(c => c.Departments)
                 .ThenInclude(d => d.Positions)
                 .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) throw new NotFoundException("Company not found!");

            var accessibleDepartments = isHrLeader
                ? company.Departments
                : company.Departments.Where(d => d.UserDepartmentAccesses.Any(uda => uda.UserId == userId))
                .ToList();

            return accessibleDepartments.Select(d => new DepartmentTreeNodeDTO
            {
                Id = d.Id,
                Name = d.Name,
                Positions = d.Positions.Select(p => new PositionTreeNodeDTO
                {
                    PublicId = p.PublicId,
                    Name = p.Name
                }).ToList()
            }).ToList();
        }

        public async Task<List<DepartmentAccessDTO>> GetDepartmentAccessAsync(int companyId)
        {

            return await _context.Users
                    .Join(
                        _context.UserRoles,
                        u => u.Id,                  
                        ur => ur.UserId,          
                        (u, ur) => new { u, ur }    
                    )
                    .Join(
                        _context.Roles.Where(r => r.Name == "HRUser"), 
                        ur => ur.ur.RoleId,        
                        r => r.Id,            
                        (ur, r) => ur.u            
                    )
                    .Where(u => u.CompanyId == companyId)  // ✅ Filter users by company
                    .Select(u => new DepartmentAccessDTO
                    {
                        UserId = u.Id,
                        UserName = u.UserName,
                        HasAccess = false
                    })
                    .ToListAsync();

        }

        public async Task<bool> UpdateDepartmentAccessAsync(int departmentId, int companyId, List<int> userIds)
        {
            var department = await _context.Departments
                .Include(d => d.UserDepartmentAccesses)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId);

            if (department == null) return false;

            var existingUserIds = department.UserDepartmentAccesses.Select(uda => uda.UserId).ToList();

            var usersToRemove = department.UserDepartmentAccesses
                .Where(uda => !userIds.Contains(uda.UserId))
                .ToList();

            if (usersToRemove.Any())
            _context.UserDepartmentAccesses.RemoveRange(usersToRemove);

            var usersToAdd = userIds.Except(existingUserIds).ToList();

            if (usersToAdd.Any())
            {
                var newAccessEntries = usersToAdd.Select(userId => new UserDepartmentAccess
                {
                    UserId = userId,
                    DepartmentId = departmentId

                }).ToList();

                _context.UserDepartmentAccesses.AddRange(newAccessEntries);

            }

            if(usersToAdd.Any() || usersToRemove.Any())
            await _context.SaveChangesAsync();

            return true;
        }
    }
}
