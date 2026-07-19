using Microsoft.EntityFrameworkCore;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Infrastructure.Persistence;

public sealed class SiteRepository(OeeDbContext context) : ISiteRepository
{
    public async Task<Site> AddAsync(Site site, CancellationToken cancellationToken = default)
    {
        context.Sites.Add(site);
        await context.SaveChangesAsync(cancellationToken);
        return site;
    }

    public Task<Site?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Sites.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.Sites.OrderBy(s => s.Name).ToListAsync(cancellationToken);

    public Task UpdateAsync(Site site, CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task DeleteAsync(Site site, CancellationToken cancellationToken = default)
    {
        context.Sites.Remove(site);
        return context.SaveChangesAsync(cancellationToken);
    }
}
