using Microsoft.Extensions.Options;

namespace NOF.Domain;

/// <summary>
/// A thread-safe Twitter Snowflake-style 64-bit ID generator.
/// </summary>
/// <remarks>
/// Default bit layout (63 bits used, sign bit always 0):
/// <code>
/// [ 0 | 41-bit timestamp ms | 10-bit machine id | 12-bit sequence ]
/// </code>
/// Epoch defaults to 2020-01-01 UTC, giving ~69 years of range.
/// Bit allocation is configurable via <see cref="SnowflakeIdGeneratorOptions"/>.
/// </remarks>
public sealed class SnowflakeIdGenerator : IIdGenerator
{
    public static readonly DateTimeOffset DefaultEpoch = new(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly long _machineId;
    private readonly long _epochMs;
    private readonly long _maxSequence;
    private readonly int _machineIdShift;
    private readonly int _timestampShift;
    private readonly Lock _lock = new();

    private long _lastTimestamp = -1L;
    private long _sequence = 0L;

    /// <summary>
    /// Initializes a new instance from <see cref="SnowflakeIdGeneratorOptions"/>.
    /// </summary>
    public SnowflakeIdGenerator(IOptions<SnowflakeIdGeneratorOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var optionsValue = options.Value;
        ArgumentNullException.ThrowIfNull(optionsValue);

        var machineIdBits = optionsValue.MachineIdBits;
        var sequenceBits = optionsValue.SequenceBits;

        if (machineIdBits is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "MachineIdBits must be between 1 and 20.");
        }

        if (sequenceBits is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "SequenceBits must be between 1 and 20.");
        }

        var maxMachineId = (1L << machineIdBits) - 1;
        _maxSequence = (1L << sequenceBits) - 1;
        _machineIdShift = sequenceBits;
        _timestampShift = machineIdBits + sequenceBits;

        if (optionsValue.MachineId < 0 || optionsValue.MachineId > maxMachineId)
        {
            throw new ArgumentOutOfRangeException(nameof(options), $"MachineId must be in range [0, {maxMachineId}].");
        }

        _machineId = optionsValue.MachineId;
        _epochMs = optionsValue.Epoch.ToUnixTimeMilliseconds();
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
                 | (_machineId << _machineIdShift)
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
