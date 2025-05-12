using CVexplorer.Controllers;
using CVexplorer.Services.Interface;
using CVexplorer.Models.DTO;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using CVexplorer.Data;
using Microsoft.EntityFrameworkCore;
using CVexplorer.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Google.Apis.Requests;
using CVexplorer.Repositories.Interface;

namespace CVexplorer.Services.Implementation
{
    public class GmailPushBackgroundService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _queue;
        private readonly IServiceProvider _sp;
        private ILogger<GmailPushBackgroundService> _logger;

        public GmailPushBackgroundService(IBackgroundTaskQueue queue, IServiceProvider sp, ILogger<GmailPushBackgroundService> logger)
        {
            _queue = queue;
            _sp = sp;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("GmailPushBackgroundService running…");

            await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
            {
                // fiecare job are EmailAddress și StartHistoryId
                // creăm un scope ca să rezolvăm DbContext, UserManager etc.
                
                try
                {
                    await ProcessHistoryAsync(job.InterogationSubscriptionId, stoppingToken);
                }
                catch (Exception ex)
                {
                    // log & eventual retry / DLQ
                }
            }
        }

        // 4️⃣ metoda pe care o va apela BackgroundService:
        public async Task ProcessHistoryAsync(long subscriptionId, CancellationToken ct)
        {
            // atenție: aici faci toată logica de „delta sync”:

            using var scope = _sp.CreateScope();
            var _context = scope.ServiceProvider.GetRequiredService<DataContext>();
            var controller = scope.ServiceProvider
                                      .GetRequiredService<GmailController>();

            var _cvRepository = scope.ServiceProvider.GetRequiredService<ICVRepository>();


            var sub = await _context.IntegrationSubscriptions.Include(s => s.User).Include(s => s.Round)
                             .SingleAsync(s => s.Id == subscriptionId, ct);

            var cred = await controller.CheckTokensAsync(sub.UserId.ToString());
            var gmail = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = cred,
                ApplicationName = "CVexplorerWebClient"

            });

            // 1. History.list
            var histReq = gmail.Users.History.List("me");
            histReq.StartHistoryId = ulong.Parse(sub.SyncToken) + 1;
            histReq.LabelId = sub.LabelId;
            var history = await histReq.ExecuteAsync(ct);

            var messageIds = history.History?
                .SelectMany(h =>
                    (h.MessagesAdded ?? Enumerable.Empty<HistoryMessageAdded>())
                    .Where(ma => ma.Message.LabelIds?.Contains(sub.LabelId) == true)
                    .Select(ma => ma.Message.Id)
                    .Concat(
                    (h.LabelsAdded ?? Enumerable.Empty<HistoryLabelAdded>())
                    .Where(la => la.LabelIds?.Contains(sub.LabelId) == true)
                    .Select(la => la.Message.Id)
                    )
                )
                .Distinct()
                .ToList()
              ?? new List<string>();

            foreach (var msgId in messageIds)
            {
                var msgReq = gmail.Users.Messages.Get("me", msgId);
                msgReq.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
                var fullMsg = await msgReq.ExecuteAsync(ct);

                // extract headers once
                var hdrs = fullMsg.Payload.Headers;
                var from = hdrs.FirstOrDefault(h => h.Name == "From")?.Value ?? "<unknown>";
                var subject = hdrs.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "<no subject>";

                // 5. Descărcăm toate PDF-urile (filtrare + paralelizare internă)
                var pdfFiles = await GetPdfFormFilesAsync( gmail,"me",msgId,fullMsg.Payload.Parts ?? Enumerable.Empty<MessagePart>(),ct);

                if (!pdfFiles.Any())
                {
                    _logger.LogWarning("From: {From}, Subject: {Subject} — nu conține PDF", from, subject);
                    continue;
                }

                _logger.LogError("From: {From}, Subject: {Subject} — CONȚINE PDF", from, subject);

                // 6. Obținem publicId-ul poziției o singură dată
                var positionPublicId = await _context.Positions
                    .Where(p => p.Id == sub.PositionId)
                    .Select(p => p.PublicId)
                    .FirstOrDefaultAsync(ct);

                // 7. Upload
                foreach (var file in pdfFiles)
                {
                    await _cvRepository.UploadDocumentAsync(file,positionPublicId,sub.User.Id,sub.RoundId);
                }
            }



            sub.SyncToken = history.HistoryId.ToString();
            sub.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
            
         
        }

        private static async Task<List<IFormFile>> GetPdfFormFilesAsync(GmailService gmailSvc,string userId,string messageId,IEnumerable<MessagePart> parts,CancellationToken ct = default)
        {
            // 1. Filtrăm doar părțile care sunt PDF
            var pdfParts = parts
                .Where(p => !string.IsNullOrEmpty(p.Filename)
                            && (p.Filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                                || p.MimeType == "application/pdf"))
                .ToList();

            // 2. Pentru fiecare attachment, pornim un task de descărcare
            var downloadTasks = pdfParts.Select(async part =>
            {
                var attach = await gmailSvc.Users.Messages.Attachments
                    .Get(userId, messageId, part.Body.AttachmentId)
                    .ExecuteAsync(ct);

                // 3. Decodăm base64url
                var base64 = attach.Data;
                var bytes = Convert.FromBase64String(
                    base64.Replace('-', '+').Replace('_', '/'));

                // 4. Creăm MemoryStream și IFormFile
                var ms = new MemoryStream(bytes);
                return (IFormFile)new FormFile(ms, 0, ms.Length,
                                               name: "file",
                                               fileName: part.Filename)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = part.MimeType
                };
            });

            // 5. Așteptăm ca toate descărcările să se finalizeze
            return (await Task.WhenAll(downloadTasks)).ToList();
        }
    }


}

   
