using CVexplorer.Models.DTO;

namespace CVexplorer.Services.Interface
{
    public interface IBackgroundTaskQueue
    {
        ValueTask EnqueueAsync(PushJobDTO job);
        IAsyncEnumerable<PushJobDTO> DequeueAllAsync(CancellationToken ct);
    }
}
