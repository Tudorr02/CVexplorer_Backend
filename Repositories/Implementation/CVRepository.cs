using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives;
using SharpCompress.Common;

using System.IO.Compression;

using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using System.Text;

using System.Text.Json;
using iText.StyledXmlParser.Jsoup.Select;
using CVexplorer.Services.Interface;
using CVexplorer.Enums;
using CVexplorer.Models.Primitives;



namespace CVexplorer.Repositories.Implementation
{
    public class CVRepository(DataContext _context , ICVEvaluationRepository _evaluation , IRoundRepository _roundRepository , IRoundEntryRepository _rEntryRepository , IPositionRepository _posRepository) : ICVRepository
    { 

        public async Task<IEnumerable<CvListDTO>> GetAllCVsAsync(string? positionPublicId = null , int?departmentId= null)
        {
            var query = _context.CVs.AsQueryable();

            if (!string.IsNullOrWhiteSpace(positionPublicId))
            {
                query = query.Where(cv =>
                    cv.Position.PublicId == positionPublicId);
            }
            else if (departmentId.HasValue)
            {
                query = query.Where(cv =>
                    cv.Position.DepartmentId == departmentId.Value);
            }

            query = query.OrderByDescending(cv => cv.UploadedAt);
            return await query
                .Select(cv => new CvListDTO
                {
                    PublicId = cv.PublicId,
                    FileName = cv.FileName,
                    UploadedAt = cv.UploadedAt,
                    UploadedBy = cv.UserUploadedBy.UserName,
                    UploadMethod = string.IsNullOrEmpty(cv.UploadMethod) ? "Unknown" : cv.UploadMethod ,
                    Score = (short)cv.Score
                })
                .ToListAsync();
        }

        public async Task<bool> DeleteCVsAsync(List<Guid> cvPublicIds, string? positionPublicId = null, int? departmentId = null)
        {
            var query = _context.CVs.Include(cv => cv.RoundEntries).AsQueryable();

            if (!string.IsNullOrWhiteSpace(positionPublicId))
            {
                query = query.Where(cv =>
                    cv.Position.PublicId == positionPublicId);
            }
            else if (departmentId.HasValue)
            {
                query = query.Where(cv =>
                    cv.Position.DepartmentId == departmentId.Value);
            }

            query = query.Where(cv => cvPublicIds.Contains(cv.PublicId));

            var cvsToDelete = await query.ToListAsync();

            if (!cvsToDelete.Any())
                return false;

            _context.CVs.RemoveRange(cvsToDelete);
            await _context.SaveChangesAsync();

            return true;
        }

        public async Task<bool> UploadDocumentAsync(IFormFile file, string positionPublicId, int userId , int? roundId = null, string? uploadMethod = "Manual")
        {
            var position = await _context.Positions.Include(p => p.Weights).FirstOrDefaultAsync(p => p.PublicId == positionPublicId)
                ?? throw new ArgumentException("Position not found.");

            var extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
            if (extension != ".pdf")
                throw new InvalidOperationException("Only PDF files are supported.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            string cvText;
            ms.Position = 0;
            cvText = ExtractText(ms);
            var evaluation = await _evaluation.CreateAsync(cvText, position);

            var cv = new CV
            {
                PositionId = position.Id,
                FileName = Path.GetFileNameWithoutExtension(file.FileName ?? "Unnamed"),
                ContentType = file.ContentType,
                Data = ms.ToArray(),
                UserUploadedById = userId,
                Evaluation = evaluation,
                UploadMethod = uploadMethod,
                Score = CalculateScore(evaluation, position.Weights)
            };

            await _context.CVs.AddAsync(cv);
            await _context.SaveChangesAsync();

            if(roundId != null)
            {
               await _rEntryRepository.CreateAsync(roundId.Value, cv.Id);
            }
            return true;
        }

        public async Task<bool> UploadBulkArchiveAsync(IFormFile archiveFile,string positionPublicId,int userId)
        {
            var position = await _context.Positions
                            .Include(p => p.Weights)
                            .AsNoTracking()
                           .FirstOrDefaultAsync(p => p.PublicId == positionPublicId)
                           ?? throw new ArgumentException("Position not found.");

            await using var stream = archiveFile.OpenReadStream();
            using var archive = ArchiveFactory.Open(stream);

            var pdfEntries = archive.Entries
                                    .Where(e => !e.IsDirectory &&
                                                e.Key.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                                    .ToList();

            if (pdfEntries.Count == 0)
                throw new InvalidOperationException("Archive contains no PDF files.");

            var cvTexts = new List<string>(pdfEntries.Count);
            var rawFiles = new List<(byte[] data, string fileName)>(pdfEntries.Count);

            foreach (var entry in pdfEntries)
            {
                using var ms = new MemoryStream();
                entry.WriteTo(ms);
                rawFiles.Add((ms.ToArray(), Path.GetFileName(entry.Key)));

                ms.Position = 0;
                cvTexts.Add(ExtractText(ms));
            }

            var evals = await _evaluation.CreateBulkAsync(cvTexts, position);

            var round = await _roundRepository.CreateAsync(position.Id);

            var cvsToAdd = rawFiles.Select((file, index) => new CV
            {
                PositionId = position.Id,
               
                FileName = Path.GetFileNameWithoutExtension(file.fileName),
                ContentType = "application/pdf",
                Data = file.data,
                UserUploadedById = userId,
                Evaluation = evals[index],
                Score = CalculateScore(evals[index], position.Weights)
            }).ToList();

            await _context.CVs.AddRangeAsync(cvsToAdd);
            await _context.SaveChangesAsync();

            var roundEntries = cvsToAdd.Select(cv => new RoundEntry
            {
                RoundId = round.Id,
                CvId = cv.Id
            }).ToList();
            

            await _context.RoundEntries.AddRangeAsync(roundEntries);
            await _context.SaveChangesAsync();

            return true;
        }



       

        private string ExtractText(Stream pdfStream)
        {
            using var reader = new PdfReader(pdfStream);
            using var pdf = new PdfDocument(reader);

            var sb = new StringBuilder();
            for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
            {
                var page = pdf.GetPage(i);
                var strategy = new LocationTextExtractionStrategy();
                string text = PdfTextExtractor.GetTextFromPage(page, strategy);
                sb.AppendLine(text);
            }
            return sb.ToString();
        }

        public double CalculateScore(CvEvaluationResult eval , ScoreWeights weights)
        {
            return Math.Round(
            ((weights.RequiredSkills/100) * eval.RequiredSkills.Score +
            (weights.NiceToHave / 100) * eval.NiceToHave.Score +
            (weights.Languages /100) * eval.Languages.Score +
            (weights.Certification /100) * eval.Certifications.Score +
            (weights.Responsibilities/100) * eval.Responsibilities.Score +
            (weights.ExperienceMonths/100) * eval.MinimumExperienceMonths.Score +
            (weights.Level /100) * eval.Level.Score +
            (weights.MinimumEducation/100) * eval.MinimumEducationLevel.Score ), 0, MidpointRounding.AwayFromZero);
        }


    }
}
