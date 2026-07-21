using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Domain.Tests.Production;

public class DowntimeEventTests
{
    private static readonly Guid MachineId = Guid.NewGuid();
    private static readonly DateTimeOffset BaseTime = new(2026, 7, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_SetsInitialState()
    {
        var evt = new DowntimeEvent(Guid.Empty, MachineId, BaseTime);

        Assert.Equal(MachineId, evt.MachineId);
        Assert.Equal(BaseTime, evt.StartedAt);
        Assert.Null(evt.EndedAt);
        Assert.Null(evt.ReasonCodeId);
        Assert.True(evt.IsOpen);
    }

    [Fact]
    public void AssignReason_WhileOpen_SetsReasonCodeId()
    {
        var evt = new DowntimeEvent(Guid.Empty, MachineId, BaseTime);
        var reasonCodeId = Guid.NewGuid();

        evt.AssignReason(reasonCodeId);

        Assert.Equal(reasonCodeId, evt.ReasonCodeId);
    }

    [Fact]
    public void AssignReason_WhileOpen_CanBeOverwritten()
    {
        var evt = new DowntimeEvent(Guid.Empty, MachineId, BaseTime);
        evt.AssignReason(Guid.NewGuid());
        var correctedReasonCodeId = Guid.NewGuid();

        evt.AssignReason(correctedReasonCodeId);

        Assert.Equal(correctedReasonCodeId, evt.ReasonCodeId);
    }

    [Fact]
    public void AssignReason_AfterClose_Throws()
    {
        var evt = new DowntimeEvent(Guid.Empty, MachineId, BaseTime);
        evt.Close(BaseTime.AddMinutes(5));

        Assert.Throws<DowntimeEventNotOpenException>(() => evt.AssignReason(Guid.NewGuid()));
    }

    [Fact]
    public void Close_SetsEndedAtAndClosesEvent()
    {
        var evt = new DowntimeEvent(Guid.Empty, MachineId, BaseTime);
        var endedAt = BaseTime.AddMinutes(5);

        evt.Close(endedAt);

        Assert.Equal(endedAt, evt.EndedAt);
        Assert.False(evt.IsOpen);
    }

    [Fact]
    public void Close_CalledTwice_IsANoOpOnSecondCall()
    {
        var evt = new DowntimeEvent(Guid.Empty, MachineId, BaseTime);
        var firstEndedAt = BaseTime.AddMinutes(5);
        evt.Close(firstEndedAt);

        evt.Close(BaseTime.AddMinutes(99));

        Assert.Equal(firstEndedAt, evt.EndedAt);
    }
}
