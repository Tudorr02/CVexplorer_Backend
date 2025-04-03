using CVexplorer.Data;
using CVexplorer.Models.Domain;
using CVexplorer.Models.DTO;
using CVexplorer.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using SharpCompress.Archives.Rar;
using SharpCompress.Archives;
using SharpCompress.Common;

using System.IO.Compression;

namespace CVexplorer.Repositories.Implementation
{
    public class CVRepository(DataContext _context) : ICVRepository
    { 

        public async Task<IEnumerable<CvListDTO>> GetAllCVsAsync(string positionPublicId)
        {
            return await _context.CVs
                .Where(cv => cv.PositionId == _context.Positions.FirstOrDefault(p => p.PublicId == positionPublicId).Id)
                .Select(cv => new CvListDTO
                {
                    PublicId = cv.PublicId,
                    FileName = cv.FileName,
                    UploadedAt = cv.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
                    UploadedBy = cv.UserUploadedBy.UserName
                }).ToListAsync();
        }

        public async Task<bool> UploadDocumentAsync(IFormFile file, string positionPublicId, int userId)
        {
            var position = await _context.Positions.FirstOrDefaultAsync(p => p.PublicId == positionPublicId)
                ?? throw new ArgumentException("Position not found.");

            var extension = Path.GetExtension(file.FileName ?? string.Empty).ToLowerInvariant();
            if (extension != ".pdf")
                throw new InvalidOperationException("Only PDF files are supported.");

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);

            var cv = new CV
            {
                PositionId = position.Id,
                Position = position,
                FileName = Path.GetFileNameWithoutExtension(file.FileName ?? "Unnamed"),
                ContentType = file.ContentType,
                Data = ms.ToArray(),
                UserUploadedById = userId
            };

            _context.CVs.Add(cv);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> UploadArchiveAsync(IFormFile archiveFile, string positionPublicId, int userId)
        {
            var position = await _context.Positions.FirstOrDefaultAsync(p => p.PublicId == positionPublicId)
                ?? throw new ArgumentException("Position not found.");

            
            using var stream = archiveFile.OpenReadStream();
            using var archive = ArchiveFactory.Open(stream);
            var allEntries = archive.Entries.Where(e => !e.IsDirectory).ToList();

            // ⚠️ Verificăm dacă există fișiere care NU sunt PDF
            var unsupportedFiles = allEntries
                .Where(e => !e.Key.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Key)
                .ToList();

            if (unsupportedFiles.Any())
            {
                // dacă există fișiere neacceptate => returnăm null sau aruncăm excepție
                throw new InvalidOperationException($"Unsupported file(s) found in archive: {string.Join(", ", unsupportedFiles)}");
            }

            foreach (var entry in allEntries)
            {        
                using var pdfStream = new MemoryStream();
                entry.WriteTo(pdfStream);

                var fileName = Path.GetFileName(entry.Key);

                var formFile = new FormFile(pdfStream, 0, pdfStream.Length, "file", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = archiveFile.ContentType
                };

                await UploadDocumentAsync(formFile, positionPublicId, userId);
                   
            }

            return true;
        }

        public async Task<CvDTO> GetCVAsync(Guid publicId)
        {
            var cv = await _context.CVs
                .Include(c => c.UserUploadedBy)
                .FirstOrDefaultAsync(c => c.PublicId == publicId);

            if (cv == null) return null;

            return new CvDTO
            {
                FileName = cv.FileName ?? "Unnamed",
                UploadedAt = cv.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
                UploadedBy = cv.UserUploadedBy?.UserName,
                FileData = cv.Data
            };
        }
    }
}
