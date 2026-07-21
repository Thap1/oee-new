using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

public interface IQualityRejectRepository
{
    Task<QualityReject> AddAsync(QualityReject qualityReject, CancellationToken cancellationToken = default);
}
