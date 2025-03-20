using CVexplorer.Data;
using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CVexplorer.Controllers
{

    [Authorize] // ✅ Ensure user is authenticated
    [ApiController]
    [Route("api/[controller]")]
    public class RolesController(DataContext _context, UserManager<User> _userManager) : Controller
    {
        [HttpGet]
        public async Task<ActionResult<List<string>>> GetRoles()
        {
            try
            {
                // ✅ Get the logged-in user
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Unauthorized(new { error = "User not found or not authenticated." });
                }

                // ✅ Get the roles assigned to the user
                var userRoles = await _userManager.GetRolesAsync(user);

                if (userRoles.Count == 1 && userRoles.Contains("HRUser"))
                {
                    return Unauthorized(new { error = "You are not allowed to view roles." });
                }

                // ✅ Fetch all roles
                var rolesQuery = _context.Roles.AsQueryable();

                // ✅ If user has "HRLeader" role, exclude "Admin" and "Moderator"
                if (userRoles.Contains("HRLeader"))
                {
                    rolesQuery = rolesQuery.Where(r => r.Name != "Admin" && r.Name != "Moderator");
                }

                var roles = await rolesQuery.Select(r => r.Name).ToListAsync();
                return Ok(roles);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = "An error occurred while fetching roles.", details = ex.Message });
            }
        }
    }
}
