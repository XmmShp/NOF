namespace NOF.Domain;

/// <summary>
/// Configuration options for <see cref="SnowflakeIdGenerator"/>.
/// </summary>
public sealed class SnowflakeIdGeneratorOptions
{
    /// <summary>
    /// A unique machine/node identifier. Must fit within <see cref="MachineIdBits"/> bits.
    /// Defaults to <c>0</c>.
    /// </summary>
    public int MachineId { get; set; } = 0;

    /// <summary>
    /// Number of bits allocated to the machine/node ID portion.
    /// Defaults to <c>10</c> (supports up to 1023 nodes).
    /// </summary>
    public int MachineIdBits { get; set; } = 10;

    /// <summary>
    /// Number of bits allocated to the per-millisecond sequence counter.
    /// Defaults to <c>12</c> (supports up to 4095 IDs per ms per node).
    /// </summary>
    public int SequenceBits { get; set; } = 12;

    /// <summary>
    /// The epoch used as the timestamp origin.
    /// Defaults to <see cref="SnowflakeIdGenerator.DefaultEpoch"/> (2020-01-01 UTC).
    /// </summary>
    public DateTimeOffset Epoch { get; set; } = SnowflakeIdGenerator.DefaultEpoch;
}
