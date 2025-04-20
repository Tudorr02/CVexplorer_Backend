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
    public class CVRepository(DataContext _context , ICvEvaluationService evaluator) : ICVRepository
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

            string cvText;
            ms.Position = 0; // Reset the stream position to the beginning
            cvText = ExtractText(ms);

            CvEvaluationResultDTO evalDto = await evaluator.EvaluateAsync(cvText, position);

            await CreateEvaluationAsync(cv, evalDto);

            return true;
        }


        private async Task CreateEvaluationAsync(CV cv, CvEvaluationResultDTO evalDto)
        {
            
            var evaluation = new CvEvaluationResult
            {
                CvId = cv.Id,
                CandidateName = evalDto.CandidateName,

                RequiredSkills = new CvScoreScrapedField<List<string>>
                {
                    Scraped = evalDto.RequiredSkills.Scraped.ToList(),
                    Score = evalDto.RequiredSkills.Score
                },
                NiceToHave = new CvScoreScrapedField<List<string>>
                {
                    Scraped = evalDto.NiceToHave.Scraped.ToList(),
                    Score = evalDto.NiceToHave.Score
                },
                Certifications = new CvScoreScrapedField<List<string>>
                {
                    Scraped = evalDto.Certifications.Scraped.ToList(),
                    Score = evalDto.Certifications.Score
                },
                Responsibilities = new CvScoreScrapedField<List<string>>
                {
                    Scraped = evalDto.Responsibilities.Scraped.ToList(),
                    Score = evalDto.Responsibilities.Score
                },

                Languages = new CvScoreValueField<List<string>>
                {
                    Value = evalDto.Languages.Value.ToList(),
                    Score = evalDto.Languages.Score
                },
                MinimumExperienceMonths = new CvScoreValueField<double>
                {
                    Value = evalDto.MinimumExperienceMonths.Value,
                    Score = evalDto.MinimumExperienceMonths.Score
                },
                Level = new CvScoreValueField<PositionLevel>
                {
                    Value = evalDto.Level.Value,
                    Score = evalDto.Level.Score
                },
                MinimumEducationLevel = new CvScoreValueField<EducationLevel>
                {
                    Value = evalDto.MinimumEducationLevel.Value,
                    Score = evalDto.MinimumEducationLevel.Score
                }
            };

            _context.CvEvaluationResults.Add(evaluation);
            await _context.SaveChangesAsync();

            // you can optionally hook the FK back if you need it on cv.Navigation
            cv.EvaluationId = evaluation.Id;
            _context.CVs.Update(cv);
            await _context.SaveChangesAsync();
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
                .Include(c => c.Evaluation)
                .FirstOrDefaultAsync(c => c.PublicId == publicId);

            if (cv == null) return null;

            return new CvDTO
            {
                FileName = cv.FileName ?? "Unnamed",
                UploadedAt = cv.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
                UploadedBy = cv.UserUploadedBy?.UserName,
                FileData = cv.Data,
                Evaluation = new CvEvaluationResultDTO
                {
                    CandidateName = cv.Evaluation.CandidateName,
                    RequiredSkills = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = cv.Evaluation.RequiredSkills.Scraped.ToList(),
                        Score = cv.Evaluation.RequiredSkills.Score
                    },
                    NiceToHave = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = cv.Evaluation.NiceToHave.Scraped.ToList(),
                        Score = cv.Evaluation.NiceToHave.Score
                    },
                    Certifications = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = cv.Evaluation.Certifications.Scraped.ToList(),
                        Score = cv.Evaluation.Certifications.Score
                    },
                    Responsibilities = new CvScoreScrapedField<IList<string>>
                    {
                        Scraped = cv.Evaluation.Responsibilities.Scraped.ToList(),
                        Score = cv.Evaluation.Responsibilities.Score
                    },
                    Languages = new CvScoreValueField<IList<string>>
                    {
                        Value = cv.Evaluation.Languages.Value.ToList(),
                        Score = cv.Evaluation.Languages.Score
                    },
                    MinimumExperienceMonths = new CvScoreValueField<double>
                    {
                        Value = cv.Evaluation.MinimumExperienceMonths.Value,
                        Score = cv.Evaluation.MinimumExperienceMonths.Score
                    },
                    Level = new CvScoreValueField<PositionLevel>
                    {
                        Value = cv.Evaluation.Level.Value,
                        Score = cv.Evaluation.Level.Score
                    },
                    MinimumEducationLevel = new CvScoreValueField<EducationLevel>
                    {
                        Value = cv.Evaluation.MinimumEducationLevel.Value,
                        Score = cv.Evaluation.MinimumEducationLevel.Score
                    }
                }
            };
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

        public async Task<CvEvaluationResultDTO> UpdateEvaluationAsync(Guid cvPublicId, CvEvaluationResultDTO editDto)
        {
            // Load the evaluation entity
            var eval = await _context.CvEvaluationResults
                .Include(e => e.Cv)
                .FirstOrDefaultAsync(e => e.Cv.PublicId == cvPublicId);

            if (eval == null)
                throw new KeyNotFoundException($"Evaluation for CV '{cvPublicId}' not found.");

            eval.CandidateName = editDto.CandidateName;
           
            eval.RequiredSkills.Scraped = editDto.RequiredSkills.Scraped.ToList();
            eval.RequiredSkills.Score = editDto.RequiredSkills.Score;
               
            eval.NiceToHave.Scraped = editDto.NiceToHave.Scraped.ToList();
            eval.NiceToHave.Score = editDto.NiceToHave.Score;
       
            eval.Certifications.Scraped = editDto.Certifications.Scraped.ToList();
            eval.Certifications.Score = editDto.Certifications.Score;
         
            eval.Responsibilities.Scraped = editDto.Responsibilities.Scraped.ToList();
            eval.Responsibilities.Score = editDto.Responsibilities.Score;
           
            eval.Languages.Value = editDto.Languages.Value.ToList();
            eval.Languages.Score = editDto.Languages.Score;
           
            eval.MinimumExperienceMonths.Value = editDto.MinimumExperienceMonths.Value;
            eval.MinimumExperienceMonths.Score = editDto.MinimumExperienceMonths.Score;
            
            eval.Level.Value = editDto.Level.Value;
            eval.Level.Score = editDto.Level.Score;
        
            eval.MinimumEducationLevel.Value = editDto.MinimumEducationLevel.Value;
            eval.MinimumEducationLevel.Score = editDto.MinimumEducationLevel.Score;
          
            _context.CvEvaluationResults.Update(eval);
            await _context.SaveChangesAsync();
            

            // Return updated DTO
            return new CvEvaluationResultDTO
            {
                CandidateName = eval.CandidateName,
                RequiredSkills = new CvScoreScrapedField<IList<string>> { Scraped = eval.RequiredSkills.Scraped, Score = eval.RequiredSkills.Score },
                NiceToHave = new CvScoreScrapedField<IList<string>> { Scraped = eval.NiceToHave.Scraped, Score = eval.NiceToHave.Score },
                Certifications = new CvScoreScrapedField<IList<string>> { Scraped = eval.Certifications.Scraped, Score = eval.Certifications.Score },
                Responsibilities = new CvScoreScrapedField<IList<string>> { Scraped = eval.Responsibilities.Scraped, Score = eval.Responsibilities.Score },
                Languages = new CvScoreValueField<IList<string>> { Value = eval.Languages.Value, Score = eval.Languages.Score },
                MinimumExperienceMonths = new CvScoreValueField<double> { Value = eval.MinimumExperienceMonths.Value, Score = eval.MinimumExperienceMonths.Score },
                Level = new CvScoreValueField<PositionLevel> { Value = eval.Level.Value, Score = eval.Level.Score },
                MinimumEducationLevel = new CvScoreValueField<EducationLevel> { Value = eval.MinimumEducationLevel.Value, Score = eval.MinimumEducationLevel.Score }
                
            };
        }
    }
}
