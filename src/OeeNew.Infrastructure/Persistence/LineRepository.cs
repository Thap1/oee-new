using Microsoft.EntityFrameworkCore;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Infrastructure.Persistence;

public sealed class LineRepository(OeeDbContext context) : ILineRepository
{
    public async Task<Line> AddAsync(Line line, CancellationToken cancellationToken = default)
    {
        context.Lines.Add(line);
        await context.SaveChangesAsync(cancellationToken);
        return line;
    }

    public Task<Line?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Lines.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Line>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default) =>
        await context.Lines.Where(l => l.SiteId == siteId).OrderBy(l => l.Name).ToListAsync(cancellationToken);

    public Task UpdateAsync(Line line, CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task DeleteAsync(Line line, CancellationToken cancellationToken = default)
    {
        context.Lines.Remove(line);
        return context.SaveChangesAsync(cancellationToken);
    }
}
