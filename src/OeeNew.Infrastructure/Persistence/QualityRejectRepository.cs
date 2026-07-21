using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.Persistence;

public sealed class QualityRejectRepository(OeeDbContext context) : IQualityRejectRepository
{
    public async Task<QualityReject> AddAsync(QualityReject qualityReject, CancellationToken cancellationToken = default)
    {
        context.QualityRejects.Add(qualityReject);
        await context.SaveChangesAsync(cancellationToken);
        return qualityReject;
    }
}
