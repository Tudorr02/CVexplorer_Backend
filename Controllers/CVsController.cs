using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;


namespace CVexplorer.Controllers
{
    [Authorize(Policy = "RequireHRUserRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class CVsController(DataContext _context, ICVRepository _cvRepository, UserManager<User> _userManager) : Controller
    {
        private async Task<bool> IsUserAuthorizedAsync(string? positionPublicId = null, Guid ?cvPublicId = null, int? departmentId = null)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.Users
                .Include(u => u.UserDepartmentAccesses)
                .FirstOrDefaultAsync(u => u.Id == (userId != null ? int.Parse(userId) : -1));

            if (user == null || user.CompanyId == null)
                return false;

            if (departmentId != null)
            {
                var department = await _context.Departments
                    .Include(d => d.Company)
                    .FirstOrDefaultAsync(d => d.Id == departmentId);
                if (department == null)
                    return false;
                if (department.CompanyId != user.CompanyId)
                    return false;

                if (User.IsInRole("HRUser"))
                {
                    bool hasAccess = user.UserDepartmentAccesses.Any(a => a.DepartmentId == department.Id);
                    if (!hasAccess)
                        return false;
                }

                return true;
            }
            if (cvPublicId != null)
            {
                var cv = await _context.CVs
                    .Include(c => c.Position)
                    .ThenInclude(p => p.Department)
                    .FirstOrDefaultAsync(c => c.PublicId == cvPublicId);

                if (cv == null)
                    return false;

                positionPublicId = cv.Position.PublicId;
            }

            var position = await _context.Positions
                .Include(p => p.Department)
                .FirstOrDefaultAsync(p => p.PublicId == positionPublicId);

            if (position == null)
                return false;

            if (position.Department.CompanyId != user.CompanyId)
                return false;

            if (User.IsInRole("HRUser"))
            {
                bool hasAccess = user.UserDepartmentAccesses.Any(a => a.DepartmentId == position.DepartmentId);
                if (!hasAccess)
                    return false;
            }

            return true;
        }

        [HttpPost("/api/Positions/{positionPublicId}/CVs")]
        public async Task<IActionResult> UploadCVs(string positionPublicId,[FromForm] IFormFile file)
        {
            if (!await IsUserAuthorizedAsync(positionPublicId))
                return Forbid();

            var userId = int.Parse(_userManager.GetUserId(User)!);

            var position = await _context.Positions.FirstOrDefaultAsync(p => p.PublicId == positionPublicId);
            if (position == null) return NotFound();

            var extension = System.IO.Path.GetExtension(file.FileName).ToLowerInvariant();

            return extension switch
            {
                ".pdf" => Ok(await _cvRepository.UploadDocumentAsync(file, positionPublicId, userId)),
                ".rar" => Ok(await _cvRepository.UploadBulkArchiveAsync(file, positionPublicId, userId)),
                ".zip" => Ok(await _cvRepository.UploadBulkArchiveAsync(file, positionPublicId, userId)),
                _ => BadRequest("Unsupported file type. Only .pdf, .zip, and .rar are allowed.")
            };
        }

        [HttpGet()]
        public async Task<ActionResult<IEnumerable<CvListDTO>>> GetAllCVs(string? positionPublicId,int? departmentId = null)
        {

            if (departmentId == null && positionPublicId == null)
                return Forbid();

            if(positionPublicId != null)
            {
                if (!await IsUserAuthorizedAsync(positionPublicId))
                    return Forbid();
            }
            else if (departmentId != null)
            {
                if (!await IsUserAuthorizedAsync(null, null, departmentId))
                    return Forbid();
            }
            

            var cvs = await _cvRepository.GetAllCVsAsync(positionPublicId, departmentId);
            return Ok(cvs);
        }

        [HttpGet("{cvPublicId:guid}")]
        public async Task<ActionResult<CvDTO>> GetCV(Guid cvPublicId)
        {
            if (!await IsUserAuthorizedAsync(null,cvPublicId))
                return Forbid();

            var cv = await _cvRepository.GetCVAsync(cvPublicId);

            if (cv == null)
                return NotFound();

            return Ok(cv);
        }


        [HttpDelete]
        public async Task<IActionResult> DeleteCV(List<Guid> cvPublicIds, string? positionPublicId, int? departmentId = null)
        {
            if (departmentId == null && positionPublicId == null)
                return Forbid();

            if (positionPublicId != null)
            {
                if (!await IsUserAuthorizedAsync(positionPublicId))
                    return Forbid();
            }
            else if (departmentId != null)
            {
                if (!await IsUserAuthorizedAsync(null, null, departmentId))
                    return Forbid();
            }
            
            return Ok(await _cvRepository.DeleteCVsAsync(cvPublicIds, positionPublicId, departmentId));
        }
        

    }
}
