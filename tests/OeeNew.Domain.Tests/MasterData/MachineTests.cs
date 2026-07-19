using OeeNew.Domain.MasterData;
using Xunit;

namespace OeeNew.Domain.Tests.MasterData;

public class MachineTests
{
    [Fact]
    public void Constructor_WithValidLineId_SetsProperties()
    {
        var lineId = Guid.NewGuid();

        var machine = new Machine(Guid.Empty, "Machine A", lineId);

        Assert.Equal("Machine A", machine.Name);
        Assert.Equal(lineId, machine.LineId);
    }

    [Fact]
    public void Constructor_WithEmptyLineId_Throws()
    {
        Assert.Throws<ArgumentException>(() => new Machine(Guid.Empty, "Machine A", Guid.Empty));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.Throws<MasterDataValidationException>(() => new Machine(Guid.Empty, name, Guid.NewGuid()));
    }

    [Fact]
    public void Constructor_WithNameOverMaxLength_Throws()
    {
        var tooLong = new string('a', 201);

        Assert.Throws<MasterDataValidationException>(() => new Machine(Guid.Empty, tooLong, Guid.NewGuid()));
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var machine = new Machine(Guid.Empty, "Machine A", Guid.NewGuid());

        machine.Rename("Machine B");

        Assert.Equal("Machine B", machine.Name);
    }
}
