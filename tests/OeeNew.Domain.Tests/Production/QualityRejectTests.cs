using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;
using Xunit;

namespace OeeNew.Domain.Tests.Production;

public class QualityRejectTests
{
    [Fact]
    public void Constructor_WithPositiveQuantity_SetsProperties()
    {
        var machineId = Guid.NewGuid();
        var recordedAt = DateTimeOffset.UtcNow;

        var reject = new QualityReject(Guid.Empty, machineId, 5, recordedAt);

        Assert.Equal(machineId, reject.MachineId);
        Assert.Equal(5, reject.Quantity);
        Assert.Equal(recordedAt, reject.RecordedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithNonPositiveQuantity_Throws(int quantity)
    {
        Assert.Throws<MasterDataValidationException>(() =>
            new QualityReject(Guid.Empty, Guid.NewGuid(), quantity, DateTimeOffset.UtcNow));
    }
}
