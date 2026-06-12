namespace NOF.Domain;

/// <summary>
/// A thread-safe Twitter Snowflake-style 64-bit ID generator.
/// </summary>
/// <remarks>
/// Default bit layout (63 bits used, sign bit always 0):
/// <code>
/// [ 0 | 41-bit timestamp ms | 8-bit application id | 6-bit instance id | 8-bit sequence ]
/// </code>
/// Epoch defaults to 2025-01-01 UTC, giving ~69 years of range.
/// Bit allocation is configurable via <see cref="SnowflakeIdGeneratorOptions"/>.
/// </remarks>
public sealed class SnowflakeIdGenerator : IIdGenerator
{
    public static readonly DateTimeOffset DefaultEpoch = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly long _applicationId;
    private readonly long _instanceId;
    private readonly long _epochMs;
    private readonly long _maxSequence;
    private readonly int _instanceIdShift;
    private readonly int _applicationIdShift;
    private readonly int _timestampShift;
    private readonly Lock _lock = new();

    private long _lastTimestamp = -1L;
    private long _sequence = 0L;

    /// <summary>
    /// Initializes a new instance with default options and deployment identifiers.
    /// </summary>
    public SnowflakeIdGenerator()
        : this(new SnowflakeIdGeneratorOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance from <see cref="SnowflakeIdGeneratorOptions"/>.
    /// </summary>
    public SnowflakeIdGenerator(
        SnowflakeIdGeneratorOptions options,
        uint applicationId = 0,
        uint instanceId = 1)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maxSequence = (1L << options.SequenceBits) - 1;
        _instanceIdShift = options.SequenceBits;
        _applicationIdShift = options.InstanceIdBits + options.SequenceBits;
        _timestampShift = options.ApplicationIdBits + options.InstanceIdBits + options.SequenceBits;

        if (_timestampShift >= 63)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The total number of ApplicationIdBits, InstanceIdBits, and SequenceBits must be less than 63.");
        }

        var maxApplicationId = (1L << options.ApplicationIdBits) - 1;
        var maxInstanceId = (1L << options.InstanceIdBits) - 1;
        if (applicationId > maxApplicationId)
        {
            throw new ArgumentOutOfRangeException(nameof(applicationId), $"ApplicationId must be in range [0, {maxApplicationId}].");
        }

        if (instanceId > maxInstanceId)
        {
            throw new ArgumentOutOfRangeException(nameof(instanceId), $"InstanceId must be in range [0, {maxInstanceId}].");
        }

        _applicationId = applicationId;
        _instanceId = instanceId;
        _epochMs = options.Epoch.ToUnixTimeMilliseconds();
    }

    /// <summary>Generates the next unique snowflake ID.</summary>
    /// <exception cref="InvalidOperationException">Thrown if the system clock moves backwards.</exception>
    public long NextId()
    {
        lock (_lock)
        {
            var now = CurrentTimestampMs();

            if (now < _lastTimestamp)
            {
                throw new InvalidOperationException(
                    $"Clock moved backwards. Refusing to generate ID for {_lastTimestamp - now} ms.");
            }

            if (now == _lastTimestamp)
            {
                _sequence = (_sequence + 1) & _maxSequence;
                if (_sequence == 0)
                {
                    now = WaitNextMillisecond(_lastTimestamp);
                }
            }
            else
            {
                _sequence = 0;
            }

            _lastTimestamp = now;

            return (now << _timestampShift)
                 | (_applicationId << _applicationIdShift)
                 | (_instanceId << _instanceIdShift)
                 | _sequence;
        }
    }

    private long CurrentTimestampMs()
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _epochMs;

    private long WaitNextMillisecond(long lastTimestamp)
    {
        var ts = CurrentTimestampMs();
        while (ts <= lastTimestamp)
        {
            ts = CurrentTimestampMs();
        }

        return ts;
    }
}
