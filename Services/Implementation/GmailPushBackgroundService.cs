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

            var sub = await _context.IntegrationSubscriptions
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
                _logger.LogError("Relevant history record: {HistoryId}", histId);
            }

            // 3. Actualizează SyncToken
            
            sub.SyncToken = history.HistoryId.ToString();
            sub.UpdatedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(ct);
            
         
        }


    }
}
