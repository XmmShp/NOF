namespace NOF.Sample;

public record WatchSampleStreamRequest
{
    public string Topic { get; set; } = "NOF Streaming Demo";

    public int Count { get; set; } = 5;

    public int IntervalMilliseconds { get; set; } = 1000;
}

public record SampleStreamEvent
{
    public required string Topic { get; set; }

    public required string Message { get; set; }

    public int Index { get; set; }

    public DateTimeOffset Timestamp { get; set; }
}
