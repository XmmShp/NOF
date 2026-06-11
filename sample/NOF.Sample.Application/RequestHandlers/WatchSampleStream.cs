using NOF.Contract;
using System.Runtime.CompilerServices;
using NOF.Abstraction;
using NOF.Application;

namespace NOF.Sample.Application.RequestHandlers;

public sealed class WatchSampleStream : NOFSampleService.WatchSampleStream
{
    public override Task<RpcResult<StreamingResult<SampleStreamEvent>>> HandleAsync(WatchSampleStreamRequest request, NOFContext context, CancellationToken cancellationToken)
    {
        var count = Math.Clamp(request.Count, 1, 20);
        var intervalMilliseconds = Math.Clamp(request.IntervalMilliseconds, 100, 5000);
        var topic = string.IsNullOrWhiteSpace(request.Topic) ? "NOF Streaming Demo" : request.Topic.Trim();

        return Task.FromResult(Success(Result.Stream(Stream(topic, count, intervalMilliseconds, cancellationToken))));
    }

    private static async IAsyncEnumerable<SampleStreamEvent> Stream(
        string topic,
        int count,
        int intervalMilliseconds,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new SampleStreamEvent
        {
            Topic = topic,
            Index = 0,
            Timestamp = DateTimeOffset.UtcNow,
            Message = "stream-started"
        };

        for (var i = 1; i <= count; i++)
        {
            await Task.Delay(intervalMilliseconds, cancellationToken);

            yield return new SampleStreamEvent
            {
                Topic = topic,
                Index = i,
                Timestamp = DateTimeOffset.UtcNow,
                Message = $"tick-{i}"
            };
        }

        yield return new SampleStreamEvent
        {
            Topic = topic,
            Index = count + 1,
            Timestamp = DateTimeOffset.UtcNow,
            Message = "stream-completed"
        };
    }
}
