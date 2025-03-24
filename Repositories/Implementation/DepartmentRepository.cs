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

        /** ✅ GET Department including access */
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
                      user => user.Id,  // ✅ Match Users.Id
                      role => role.UserId,  // ✅ Match UserRoles.UserId
                      (user, role) => user) // ✅ Select only Users (avoid unnecessary fields)
                .GroupJoin(_context.UserDepartmentAccesses.Where(uda => uda.DepartmentId == departmentId),
                           user => user.Id,  // ✅ Match Users.Id
                           access => access.UserId,  // ✅ Match UserDepartmentAccesses.UserId
                           (user, access) => new DepartmentAccessDTO
                           {
                               UserId = user.Id,
                               UserName = user.UserName,
                               HasAccess = access.Any() // ✅ Directly check in SQL
                           })
                .ToListAsync();

            return new DepartmentDTO
            {
                Name = department.Name,
                DepartmentAccesses = allUsers
            };
           
        }



        // ✅ Create a department within a company
        public async Task<DepartmentListDTO> CreateDepartmentAsync(int companyId, DepartmentDTO dto)
        {
            var company = await _context.Companies
                .Include(c => c.Departments)
                .FirstOrDefaultAsync(c => c.Id == companyId);

            if (company == null) throw new NotFoundException("Company not found!");

            if(dto.DepartmentAccesses != null && dto.DepartmentAccesses.Any())
            {
                // ✅ Extract user IDs from the DTO where HasAccess = true
                var userIdsFromDto = dto.DepartmentAccesses?
                    .Where(da => da.HasAccess)
                    .Select(da => da.UserId)
                    .ToList() ?? [];

                // ✅ Retrieve only users who belong to the company and have the "HRUser" role
                var validCompanyUsers = await _context.Users
                    .Join(_context.UserRoles.Where(ur => ur.Role.Name == "HRUser"),
                          user => user.Id,
                          role => role.UserId,
                          (user, role) => user)
                    .Where(u => u.CompanyId == companyId)
                    .Select(u => u.Id)
                    .Distinct() // ✅ Ensures unique user IDs
                    .ToListAsync();

                // ✅ Check if all users in the DTO exist in the validCompanyUsers list
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

            // ✅ Handle department access if provided
            if (dto.DepartmentAccesses != null && dto.DepartmentAccesses.Any())
            {
                var accessList = dto.DepartmentAccesses
                    .Where(da => da.HasAccess) // Only add users with access enabled
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

        // ✅ Update a department within a company
        public async Task<DepartmentDTO> UpdateDepartmentAsync(int departmentId, int companyId,DepartmentDTO dto)
        {
            var department = await _context.Departments
                .Include(d => d.UserDepartmentAccesses)
                .ThenInclude(uda => uda.User)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId);

            if (department == null) throw new NotFoundException("Department not found!");


            // ✅ Check if all users in dto.DepartmentAccesses belong to the company & have HRUser role
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
                .Distinct() // ✅ Ensures unique user IDs
                .ToListAsync();

            if (userIdsFromDto.Any(userId => !validCompanyUsers.Contains(userId)))
            {
                throw new UnauthorizedAccessException("Some users do not belong to this company or do not have the HRUser role.");
            }

            department.Name = dto.Name;

            // ✅ Update department access (removes old, adds new)
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

        // ✅ Delete a department within a company
        public async Task<bool> DeleteDepartmentAsync(int departmentId)
        {
            var department = await _context.Departments
                .Include(d => d.Company)
                .Include(d => d.Positions)
                .Include(d =>  d.UserDepartmentAccesses)
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

        /// ✅ Get all users with access to a department
        public async Task<List<DepartmentAccessDTO>> GetDepartmentAccessAsync(int companyId)
        {

            return await _context.Users
                    .Join(
                        _context.UserRoles,
                        u => u.Id,                  // ✅ User ID from Users table
                        ur => ur.UserId,            // ✅ Match with User ID in UserRoles
                        (u, ur) => new { u, ur }    // ✅ Select both user and role mapping
                    )
                    .Join(
                        _context.Roles.Where(r => r.Name == "HRUser"), // ✅ Filter by HRUser role
                        ur => ur.ur.RoleId,        // ✅ Match role ID from UserRoles
                        r => r.Id,                 // ✅ Match with Role ID
                        (ur, r) => ur.u            // ✅ Select the user
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

        ///// ✅ Update department access for users
        public async Task<bool> UpdateDepartmentAccessAsync(int departmentId, int companyId, List<int> userIds)
        {
            var department = await _context.Departments
                .Include(d => d.UserDepartmentAccesses)
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.CompanyId == companyId);

            if (department == null) return false;

            var existingUserIds = department.UserDepartmentAccesses.Select(uda => uda.UserId).ToList();

            // ✅ Find users to remove (existing but not in new list)
            var usersToRemove = department.UserDepartmentAccesses
                .Where(uda => !userIds.Contains(uda.UserId))
                .ToList();

            // ✅ Remove only those users
            if (usersToRemove.Any())
            _context.UserDepartmentAccesses.RemoveRange(usersToRemove);

            // ✅ Find users to add (in userIds but not in existingUserIds)
            var usersToAdd = userIds.Except(existingUserIds).ToList();

            // ✅ Add only the new users
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
