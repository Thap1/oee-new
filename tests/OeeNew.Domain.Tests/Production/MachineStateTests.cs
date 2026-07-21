using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Domain.Tests.Production;

public class MachineStateTests
{
    private static readonly Guid MachineId = Guid.NewGuid();
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_SetsInitialReading()
    {
        var state = new MachineState(MachineId, MachineStatus.Running, counter: 100, lastReportedAt: BaseTime);

        Assert.Equal(MachineId, state.MachineId);
        Assert.Equal(MachineStatus.Running, state.Status);
        Assert.Equal(100, state.Counter);
        Assert.Equal(BaseTime, state.LastReportedAt);
    }

    [Fact]
    public void Apply_WithNewerReading_UpdatesStateAndReturnsTrue()
    {
        var state = new MachineState(MachineId, MachineStatus.Running, counter: 100, lastReportedAt: BaseTime);

        var applied = state.Apply(MachineStatus.Stopped, counter: 150, reportedAt: BaseTime.AddSeconds(10));

        Assert.True(applied);
        Assert.Equal(MachineStatus.Stopped, state.Status);
        Assert.Equal(150, state.Counter);
        Assert.Equal(BaseTime.AddSeconds(10), state.LastReportedAt);
    }

    [Fact]
    public void Apply_WithOlderReading_IsIgnoredAndReturnsFalse()
    {
        var state = new MachineState(MachineId, MachineStatus.Running, counter: 100, lastReportedAt: BaseTime);

        var applied = state.Apply(MachineStatus.Stopped, counter: 999, reportedAt: BaseTime.AddSeconds(-10));

        Assert.False(applied);
        Assert.Equal(MachineStatus.Running, state.Status);
        Assert.Equal(100, state.Counter);
        Assert.Equal(BaseTime, state.LastReportedAt);
    }

    [Fact]
    public void Apply_WithSameTimestamp_IsIgnoredAndReturnsFalse()
    {
        var state = new MachineState(MachineId, MachineStatus.Running, counter: 100, lastReportedAt: BaseTime);

        var applied = state.Apply(MachineStatus.Stopped, counter: 999, reportedAt: BaseTime);

        Assert.False(applied);
        Assert.Equal(MachineStatus.Running, state.Status);
        Assert.Equal(100, state.Counter);
    }
}
