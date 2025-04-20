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
    //[Authorize(Policy = "RequireHRUserRole")]
    [ApiController]
    [Route("api/[controller]")]
    public class CVsController(DataContext _context, ICVRepository _cvRepository, UserManager<User> _userManager) : Controller
    {
        private async Task<bool> IsUserAuthorizedAsync(string positionPublicId, Guid ?cvPublicId = null)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.Users
                .Include(u => u.UserDepartmentAccesses)
                .FirstOrDefaultAsync(u => u.Id == (userId != null ? int.Parse(userId) : -1));

            if (user == null || user.CompanyId == null)
                return false;

            
            if(cvPublicId != null)
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
        [Consumes("multipart/form-data")]
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
                ".rar" or ".zip" => Ok(await _cvRepository.UploadArchiveAsync(file, positionPublicId, userId)),
                _ => BadRequest("Unsupported file type. Only .pdf, .zip, and .rar are allowed.")
            };
        }

        [HttpGet()]
        public async Task<ActionResult<IEnumerable<CvListDTO>>> GetAllCVs(string positionPublicId,int? departmentId)
        {
            if (!await IsUserAuthorizedAsync(positionPublicId))
                return Forbid();

            var cvs = await _cvRepository.GetAllCVsAsync(positionPublicId);
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

        [HttpPost("extract-itext7")]
        public IActionResult ExtractTextWithIText7(IFormFile file)
        {
            if (file is null || file.Length == 0)
                return BadRequest("File is empty or null.");

            var textBuilder = new StringBuilder();

            // iText 7
            using (var stream = file.OpenReadStream())               // nu e nevoie să‑l copiem în alt MemoryStream
            using (var reader = new PdfReader(stream))
            using (var pdfDoc = new PdfDocument(reader))
            {
                for (int page = 1; page <= pdfDoc.GetNumberOfPages(); page++)
                {
                    var extractionStrategy = new LocationTextExtractionStrategy();
                    var pageText = PdfTextExtractor.GetTextFromPage(
                                       pdfDoc.GetPage(page),
                                       extractionStrategy);

                    textBuilder.AppendLine(pageText);
                }
            }

            return Ok(textBuilder.ToString());
        }

        [HttpPut("{cvPublicId:guid}/Evaluation")]
        public async Task<ActionResult<CvEvaluationResultDTO>> UpdateEvaluation(Guid cvPublicId,[FromBody] CvEvaluationResultDTO editDto)
        {
            if (!await IsUserAuthorizedAsync(null, cvPublicId))
                return Forbid();

            if (editDto == null)
                return BadRequest("Edit data is required.");

            try
            {
                var updated = await _cvRepository.UpdateEvaluationAsync(cvPublicId, editDto);
                return Ok(updated);
            }
            catch (KeyNotFoundException)
            {
                return NotFound();
            }
        }

    }
}
