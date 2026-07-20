using Microsoft.EntityFrameworkCore;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Infrastructure.Persistence;

public sealed class ReasonCodeRepository(OeeDbContext context) : IReasonCodeRepository
{
    public async Task<ReasonCode> AddAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default)
    {
        context.ReasonCodes.Add(reasonCode);
        await context.SaveChangesAsync(cancellationToken);
        return reasonCode;
    }

    public Task<ReasonCode?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ReasonCodes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ReasonCode>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default) =>
        await context.ReasonCodes.Where(r => r.SiteId == siteId).OrderBy(r => r.Name).ToListAsync(cancellationToken);

    public Task UpdateAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
