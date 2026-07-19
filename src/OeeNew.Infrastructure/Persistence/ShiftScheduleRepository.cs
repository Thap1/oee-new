using Microsoft.EntityFrameworkCore;
using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Infrastructure.Persistence;

public sealed class ShiftScheduleRepository(OeeDbContext context) : IShiftScheduleRepository
{
    public async Task<ShiftSchedule> AddAsync(ShiftSchedule shift, CancellationToken cancellationToken = default)
    {
        context.ShiftSchedules.Add(shift);
        await context.SaveChangesAsync(cancellationToken);
        return shift;
    }

    public Task<ShiftSchedule?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.ShiftSchedules.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ShiftSchedule>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default) =>
        await context.ShiftSchedules.Where(s => s.SiteId == siteId).OrderBy(s => s.Name).ToListAsync(cancellationToken);

    public Task UpdateAsync(ShiftSchedule shift, CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);

    public Task DeleteAsync(ShiftSchedule shift, CancellationToken cancellationToken = default)
    {
        context.ShiftSchedules.Remove(shift);
        return context.SaveChangesAsync(cancellationToken);
    }
}
