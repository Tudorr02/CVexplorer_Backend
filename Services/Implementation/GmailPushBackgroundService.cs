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

            var relevantHistoryIds = (history.History ?? Enumerable.Empty<History>())
            .Where(h =>
                // any new messages that were created already in your label
                (h.MessagesAdded != null &&
                 h.MessagesAdded.Any(ma => ma.Message.LabelIds?.Contains(sub.LabelId) == true))
                ||
                // any existing messages that just had your label added
                (h.LabelsAdded != null &&
                 h.LabelsAdded.Any(la => la.LabelIds?.Contains(sub.LabelId) == true))
            )
            // 2) project to just the HistoryId
            .Select(h => h.Id)
            .ToList();

            foreach (var histId in relevantHistoryIds)
            {
                var histEvent = history.History
                    .First(h => h.Id == histId);

                var idsFromLabelsAdded = (histEvent.LabelsAdded ?? Enumerable.Empty<HistoryLabelAdded>())
                .Where(la => la.LabelIds?.Contains(sub.LabelId) == true)
                .Select(la => la.Message.Id);

                // 2. Din MessagesAdded: toate mesajele noi care au fost create deja cu eticheta sub.LabelId
                var idsFromMessagesAdded = (histEvent.MessagesAdded ?? Enumerable.Empty<HistoryMessageAdded>())
                    .Where(ma => ma.Message.LabelIds?.Contains(sub.LabelId) == true)
                    .Select(ma => ma.Message.Id);

                // 3. Unifici (dacă vrei) și elimini duplicatele:
                var allRelevantMessageIds = idsFromLabelsAdded
                    .Concat(idsFromMessagesAdded)
                    .Distinct();

                // Exemplu de log:
                foreach (var messageId in allRelevantMessageIds)
                {
                    var msgReq = gmail.Users.Messages.Get("me", messageId);
                    msgReq.Format = UsersResource.MessagesResource.GetRequest.FormatEnum.Full;
                    var fullMessage = await msgReq.ExecuteAsync(ct);

                    // 2. Extrage câteva informații de bază din header
                    string from = fullMessage.Payload.Headers
                        .FirstOrDefault(h => h.Name == "From")?.Value ?? "<unknown>";
                    string subject = fullMessage.Payload.Headers
                        .FirstOrDefault(h => h.Name == "Subject")?.Value ?? "<no subject>";

                    // 3. Verifică atașamentele PDF
                    bool hasPdf = fullMessage.Payload.Parts?
                        .Where(p => !string.IsNullOrEmpty(p.Filename))
                        .Any(p =>
                            p.Filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                            || p.MimeType == "application/pdf"
                        ) == true;

                    
                    // 4. Răspuns / log
                    if (hasPdf)
                    {
                        _logger.LogError(
                            "From: {From}, Subject: {Subject} — CONȚINE PDF",
                            from, subject);

                        var pdfParts = fullMessage.Payload.Parts?
                            .Where(p => !string.IsNullOrEmpty(p.Filename)
                         && (p.Filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
                        || p.MimeType == "application/pdf"))
                            .ToList() ?? new List<MessagePart>();

                        // 2) Extrage AttachmentId-urile
                        var attachmentIds = pdfParts
                            .Select(p => p.Body?.AttachmentId)
                            .Where(id => !string.IsNullOrEmpty(id))
                            .ToList();

                        var pId= _context.Positions.Where(p => p.Id == sub.PositionId)
                            .Select(p => p.PublicId)
                            .FirstOrDefault();
                        //_cvRepository.UploadDocumentAsync
                        foreach (var part in pdfParts)
                        {
                            var formFile = await GmailAttachmentHelper
                               .GetAttachmentAsFormFileAsync(
                                    gmail,               // instanța GmailService
                                    "me",                // userId
                                    messageId,           // id-ul mesajului curent
                                    part,
                                    ct
                               );

                            // Acum poți trimite `formFile` unui repository sau controller care așteaptă IFormFile:
                            await _cvRepository.UploadDocumentAsync(formFile, pId, sub.User.Id,sub.RoundId);
                        }

                    }
                    else
                    {
                        _logger.LogWarning(
                            "From: {From}, Subject: {Subject} — nu conține PDF",
                            from, subject);
                    }
                }
            }

            // 3. Actualizează SyncToken
            
            sub.SyncToken = history.HistoryId.ToString();
            sub.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
            
         
        }


    }

    public static class GmailAttachmentHelper
    {
        /// <summary>
        /// Descarcă un attachment din Gmail și îl transformă într-un IFormFile.
        /// </summary>
        public static async Task<IFormFile> GetAttachmentAsFormFileAsync(
            GmailService gmailSvc,
            string userId,
            string messageId,
            MessagePart attachmentPart,
            CancellationToken ct = default)
        {
            // 1) Extragere attachmentId
            var attachmentId = attachmentPart.Body?.AttachmentId;
            if (string.IsNullOrEmpty(attachmentId))
                throw new InvalidOperationException("Partea nu conține attachmentId.");

            // 2) Descarcă datele
            var attach = await gmailSvc.Users.Messages.Attachments
                .Get(userId, messageId, attachmentId)
                .ExecuteAsync(ct);

            // 3) Decode base64url
            var base64 = attach.Data;
            var bytes = Convert.FromBase64String(
                base64.Replace('-', '+').Replace('_', '/')
            );

            // 4) Creează un MemoryStream
            var ms = new MemoryStream(bytes);

            // 5) Construieste FormFile
            var fileName = attachmentPart.Filename ?? "attachment.pdf";
            var contentType = attachmentPart.MimeType ?? "application/pdf";

            var formFile = new FormFile(ms, 0, ms.Length,
                                        name: "file",
                                        fileName: fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };

            return formFile;
        }
    }
}
