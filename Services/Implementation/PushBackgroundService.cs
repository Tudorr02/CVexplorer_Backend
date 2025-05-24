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
    public class PushBackgroundService : BackgroundService
    {
        private readonly IBackgroundTaskQueue _queue;
        private ILogger<PushBackgroundService> _logger;
        private readonly IServiceProvider _sp;

        public PushBackgroundService(IBackgroundTaskQueue queue, IServiceProvider sp, ILogger<PushBackgroundService> logger)
        {
            _queue = queue;
            _logger = logger;
            _sp = sp; 
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
            {
                
                try
                {

                    using var scope = _sp.CreateScope();
                    if (job.Provider == "Gmail")
                    {
                        var gService = scope.ServiceProvider.GetRequiredService<IGmailService>();
                        var subscriptionId = long.Parse(job.SubscriptionId);
                        await gService.ProcessHistoryAsync(subscriptionId, stoppingToken);
                    }
                    else if (job.Provider == "Outlook")
                    {
                        //var outlookController = scope.ServiceProvider
                        //                             .GetRequiredService<OutlookController>();
                        //await outlookController.ProcessNewMessageAsync(
                        //    job.MessageId,
                        //    job.ResourceId
                        //);
                    }
                    else
                    {
                        _logger.LogWarning("Unknown provider {Provider}", job.Provider);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing job {Job}", job);

                }
            }
        }

        }


}

   
