using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Tests.Production;

internal sealed class FakeQualityRejectRepository : IQualityRejectRepository
{
    public List<QualityReject> Added { get; } = [];

    public Task<QualityReject> AddAsync(QualityReject qualityReject, CancellationToken cancellationToken = default)
    {
        Added.Add(qualityReject);
        return Task.FromResult(qualityReject);
    }
}
