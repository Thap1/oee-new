using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Domain.Tests.MasterData;

public class ShiftScheduleTests
{
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid LineId = Guid.NewGuid();

    [Fact]
    public void Constructor_WithValidData_SetsProperties()
    {
        var shift = new ShiftSchedule(Guid.Empty, SiteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.Equal(SiteId, shift.SiteId);
        Assert.Null(shift.LineId);
        Assert.Equal("Day Shift", shift.Name);
        Assert.Equal(new TimeOnly(8, 0), shift.StartTime);
        Assert.Equal(new TimeOnly(16, 0), shift.EndTime);
    }

    [Fact]
    public void Constructor_WithEmptySiteId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ShiftSchedule(Guid.Empty, Guid.Empty, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.Throws<MasterDataValidationException>(() => new ShiftSchedule(Guid.Empty, SiteId, null, name, new TimeOnly(8, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public void Constructor_WithNameOverMaxLength_Throws()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<MasterDataValidationException>(() => new ShiftSchedule(Guid.Empty, SiteId, null, tooLong, new TimeOnly(8, 0), new TimeOnly(16, 0)));
    }

    [Fact]
    public void Constructor_WithEqualStartAndEndTime_Throws()
    {
        Assert.Throws<MasterDataValidationException>(() => new ShiftSchedule(Guid.Empty, SiteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(8, 0)));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var shift = new ShiftSchedule(Guid.Empty, SiteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        shift.Rename("Morning Shift");

        Assert.Equal("Morning Shift", shift.Name);
    }

    [Fact]
    public void Reschedule_WithValidTimes_UpdatesTimes()
    {
        var shift = new ShiftSchedule(Guid.Empty, SiteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        shift.Reschedule(new TimeOnly(9, 0), new TimeOnly(17, 0));

        Assert.Equal(new TimeOnly(9, 0), shift.StartTime);
        Assert.Equal(new TimeOnly(17, 0), shift.EndTime);
    }

    [Fact]
    public void Reschedule_WithEqualStartAndEndTime_Throws()
    {
        var shift = new ShiftSchedule(Guid.Empty, SiteId, null, "Day Shift", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.Throws<MasterDataValidationException>(() => shift.Reschedule(new TimeOnly(9, 0), new TimeOnly(9, 0)));
    }

    [Fact]
    public void OverlapsWith_AdjacentButNotOverlapping_ReturnsFalse()
    {
        var morning = new ShiftSchedule(Guid.Empty, SiteId, null, "Morning", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var afternoon = new ShiftSchedule(Guid.Empty, SiteId, null, "Afternoon", new TimeOnly(12, 0), new TimeOnly(16, 0));

        Assert.False(morning.OverlapsWith(afternoon));
        Assert.False(afternoon.OverlapsWith(morning));
    }

    [Fact]
    public void OverlapsWith_PartialOverlap_ReturnsTrue()
    {
        var a = new ShiftSchedule(Guid.Empty, SiteId, null, "A", new TimeOnly(8, 0), new TimeOnly(12, 0));
        var b = new ShiftSchedule(Guid.Empty, SiteId, null, "B", new TimeOnly(10, 0), new TimeOnly(14, 0));

        Assert.True(a.OverlapsWith(b));
        Assert.True(b.OverlapsWith(a));
    }

    [Fact]
    public void OverlapsWith_FullContainment_ReturnsTrue()
    {
        var outer = new ShiftSchedule(Guid.Empty, SiteId, null, "Outer", new TimeOnly(8, 0), new TimeOnly(18, 0));
        var inner = new ShiftSchedule(Guid.Empty, SiteId, null, "Inner", new TimeOnly(10, 0), new TimeOnly(12, 0));

        Assert.True(outer.OverlapsWith(inner));
        Assert.True(inner.OverlapsWith(outer));
    }

    [Fact]
    public void OverlapsWith_OvernightShiftOverlapsEarlyMorningShift_ReturnsTrue()
    {
        var night = new ShiftSchedule(Guid.Empty, SiteId, null, "Night", new TimeOnly(22, 0), new TimeOnly(6, 0));
        var earlyMorning = new ShiftSchedule(Guid.Empty, SiteId, null, "Early", new TimeOnly(5, 0), new TimeOnly(7, 0));

        Assert.True(night.OverlapsWith(earlyMorning));
        Assert.True(earlyMorning.OverlapsWith(night));
    }

    [Fact]
    public void OverlapsWith_OvernightShiftAdjacentToDayShift_ReturnsFalse()
    {
        var night = new ShiftSchedule(Guid.Empty, SiteId, null, "Night", new TimeOnly(22, 0), new TimeOnly(6, 0));
        var day = new ShiftSchedule(Guid.Empty, SiteId, null, "Day", new TimeOnly(6, 0), new TimeOnly(22, 0));

        Assert.False(night.OverlapsWith(day));
        Assert.False(day.OverlapsWith(night));
    }

    [Fact]
    public void OverlapsWith_DifferentSite_ReturnsFalse()
    {
        var a = new ShiftSchedule(Guid.Empty, SiteId, null, "A", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var b = new ShiftSchedule(Guid.Empty, Guid.NewGuid(), null, "B", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.False(a.OverlapsWith(b));
    }

    [Fact]
    public void OverlapsWith_SameSiteDifferentLineScope_ReturnsFalse()
    {
        var siteWide = new ShiftSchedule(Guid.Empty, SiteId, null, "Site-wide", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var lineScoped = new ShiftSchedule(Guid.Empty, SiteId, LineId, "Line-scoped", new TimeOnly(8, 0), new TimeOnly(16, 0));

        Assert.False(siteWide.OverlapsWith(lineScoped));
    }

    [Fact]
    public void OverlapsWith_SameSiteAndLineOverlapping_ReturnsTrue()
    {
        var a = new ShiftSchedule(Guid.Empty, SiteId, LineId, "A", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var b = new ShiftSchedule(Guid.Empty, SiteId, LineId, "B", new TimeOnly(9, 0), new TimeOnly(17, 0));

        Assert.True(a.OverlapsWith(b));
    }
}
