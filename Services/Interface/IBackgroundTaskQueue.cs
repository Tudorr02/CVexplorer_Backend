using CVexplorer.Models.DTO;

namespace CVexplorer.Services.Interface
{
    public interface IBackgroundTaskQueue
    {
        ValueTask EnqueueAsync(GmailPushJobDTO job);
        IAsyncEnumerable<GmailPushJobDTO> DequeueAllAsync(CancellationToken ct);
    }
}
