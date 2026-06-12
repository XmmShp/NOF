using System.ComponentModel.DataAnnotations;

namespace NOF.Domain;

/// <summary>
/// Configuration options for <see cref="SnowflakeIdGenerator"/>.
/// </summary>
public sealed class SnowflakeIdGeneratorOptions
{
    /// <summary>
    /// Number of bits allocated to the application ID portion.
    /// Defaults to <c>8</c> (supports up to 255 applications).
    /// </summary>
    [Range(1, 20)]
    public int ApplicationIdBits { get; set; } = 8;

    /// <summary>
    /// Number of bits allocated to the instance ID portion.
    /// Defaults to <c>6</c> (supports up to 63 instances per application).
    /// </summary>
    [Range(1, 20)]
    public int InstanceIdBits { get; set; } = 6;

    /// <summary>
    /// Number of bits allocated to the per-millisecond sequence counter.
    /// Defaults to <c>8</c> (supports up to 255 IDs per ms per deployment instance).
    /// </summary>
    [Range(1, 20)]
    public int SequenceBits { get; set; } = 8;

    /// <summary>
    /// The epoch used as the timestamp origin.
    /// Defaults to <see cref="SnowflakeIdGenerator.DefaultEpoch"/> (2025-01-01 UTC).
    /// </summary>
    public DateTimeOffset Epoch { get; set; } = SnowflakeIdGenerator.DefaultEpoch;
}
