using FluentAssertions;
using NOF.Domain;
using Xunit;

namespace NOF.SourceGenerator.Tests;

public class SnowflakeIdGeneratorTests
{
    private static SnowflakeIdGenerator Default() =>
        new(new SnowflakeIdGeneratorOptions());

    // -----------------------------------------------------------------------
    // Basic generation
    // -----------------------------------------------------------------------

    [Fact]
    public void NextId_ReturnsPositiveLong()
    {
        var gen = Default();
        gen.NextId().Should().BePositive();
    }

    [Fact]
    public void NextId_CalledRepeatedly_ReturnsUniqueIds()
    {
        var gen = Default();
        var ids = Enumerable.Range(0, 1000).Select(_ => gen.NextId()).ToList();
        ids.Distinct().Should().HaveCount(1000);
    }

    [Fact]
    public void NextId_IsMonotonicallyIncreasing_WithinSameMs()
    {
        var gen = Default();
        var ids = Enumerable.Range(0, 100).Select(_ => gen.NextId()).ToList();
        ids.Should().BeInAscendingOrder();
    }

    // -----------------------------------------------------------------------
    // Options: MachineId embedded correctly
    // -----------------------------------------------------------------------

    [Fact]
    public void NextId_EmbedsMachineId_InCorrectBitPosition()
    {
        const int machineId = 7;
        var gen = new SnowflakeIdGenerator(new SnowflakeIdGeneratorOptions { MachineId = machineId });
        var id = gen.NextId();

        // Default layout: 12 sequence bits, 10 machine bits
        const int sequenceBits = 12;
        const int machineIdBits = 10;
        const long maxMachineId = (1L << machineIdBits) - 1;
        var extractedMachineId = (int)((id >> sequenceBits) & maxMachineId);

        extractedMachineId.Should().Be(machineId);
    }

    // -----------------------------------------------------------------------
    // Options: custom bit layout
    // -----------------------------------------------------------------------

    [Fact]
    public void CustomBitLayout_EmbedsMachineId_Correctly()
    {
        const int machineIdBits = 8;
        const int sequenceBits = 8;
        const int machineId = 5;

        var gen = new SnowflakeIdGenerator(new SnowflakeIdGeneratorOptions
        {
            MachineIdBits = machineIdBits,
            SequenceBits = sequenceBits,
            MachineId = machineId,
        });

        var id = gen.NextId();
        var maxMachineId = (1L << machineIdBits) - 1;
        var extracted = (int)((id >> sequenceBits) & maxMachineId);
        extracted.Should().Be(machineId);
    }

    [Fact]
    public void CustomBitLayout_StillProducesUniqueIds()
    {
        var gen = new SnowflakeIdGenerator(new SnowflakeIdGeneratorOptions
        {
            MachineIdBits = 8,
            SequenceBits = 8,
        });

        var ids = Enumerable.Range(0, 500).Select(_ => gen.NextId()).ToList();
        ids.Distinct().Should().HaveCount(500);
    }

    // -----------------------------------------------------------------------
    // Options: custom epoch
    // -----------------------------------------------------------------------

    [Fact]
    public void CustomEpoch_ProducesLargerTimestampComponent_ThanDefaultEpoch()
    {
        // A later epoch means smaller timestamp component (less time has elapsed)
        var laterEpoch = DateTimeOffset.UtcNow.AddSeconds(-10);
        var gen = new SnowflakeIdGenerator(new SnowflakeIdGeneratorOptions { Epoch = laterEpoch });
        gen.NextId().Should().BePositive();
    }

    // -----------------------------------------------------------------------
    // Options: validation
    // -----------------------------------------------------------------------

    [Fact]
    public void MachineId_OutOfRange_Throws()
    {
        var act = () => new SnowflakeIdGenerator(new SnowflakeIdGeneratorOptions { MachineId = 9999 });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void NegativeMachineId_Throws()
    {
        var act = () => new SnowflakeIdGenerator(new SnowflakeIdGeneratorOptions { MachineId = -1 });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void InvalidMachineIdBits_Throws()
    {
        var act = () => new SnowflakeIdGenerator(new SnowflakeIdGeneratorOptions { MachineIdBits = 0 });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BitLayoutOverflow_Throws()
    {
        var act = () => new SnowflakeIdGenerator(new SnowflakeIdGeneratorOptions
        {
            MachineIdBits = 32,
            SequenceBits = 32,
        });
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // -----------------------------------------------------------------------
    // IIdGenerator interface
    // -----------------------------------------------------------------------

    [Fact]
    public void ImplementsIIdGenerator()
    {
        IIdGenerator gen = Default();
        gen.NextId().Should().BePositive();
    }

    // -----------------------------------------------------------------------
    // IdGenerator.Current static accessor
    // -----------------------------------------------------------------------

    [Fact]
    public void IdGenerator_Current_ThrowsBeforeSet()
    {
        // Reset to null via reflection to isolate test
        var field = typeof(IdGenerator).GetField("_current",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var previous = field.GetValue(null);
        field.SetValue(null, null);

        try
        {
            var act = () => IdGenerator.Current;
            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*IdGenerator has not been initialized*");
        }
        finally
        {
            field.SetValue(null, previous); // restore
        }
    }

    [Fact]
    public void IdGenerator_SetCurrent_MakesCurrent_Available()
    {
        var gen = Default();
        IdGenerator.SetCurrent(gen);
        IdGenerator.Current.Should().BeSameAs(gen);
    }

    [Fact]
    public void IdGenerator_SetCurrent_Null_Throws()
    {
        var act = () => IdGenerator.SetCurrent(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
