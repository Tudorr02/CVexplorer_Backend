using CVexplorer.Models.DTO;
using CVexplorer.Services.Interface;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace CVexplorer.Services.Implementation
{
    public class BackgroundTaskQueue : IBackgroundTaskQueue
    {
        private readonly Channel<GmailPushJobDTO> _channel = Channel.CreateUnbounded<GmailPushJobDTO>();

        public ValueTask EnqueueAsync(GmailPushJobDTO job) =>
            _channel.Writer.WriteAsync(job);

        public async IAsyncEnumerable<GmailPushJobDTO> DequeueAllAsync([EnumeratorCancellation] CancellationToken ct)
        {
            while (await _channel.Reader.WaitToReadAsync(ct))
            {
                while (_channel.Reader.TryRead(out var job))
                    yield return job;
            }
        }
    }
}
