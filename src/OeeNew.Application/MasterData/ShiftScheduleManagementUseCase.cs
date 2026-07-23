using OeeNew.Application;
using OeeNew.Application.Auth;
using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Create/reschedule/delete/list Shift Schedules under a Site (Story 1.3, AC #1, #2, #3 — FR-012; scope-filtered per Story 1.6, AC #2/#4). Admin-only for writes, re-checked here in addition to the API-layer policy.</summary>
public sealed class ShiftScheduleManagementUseCase(IShiftScheduleRepository shifts, ISiteRepository sites, ILineRepository lines, AppModeInfo appMode)
{
    public async Task<IReadOnlyList<ShiftSchedule>> ListBySiteAsync(CallerScope scope, Guid siteId, CancellationToken cancellationToken = default)
    {
        if (!scope.AllowsSite(siteId))
        {
            throw new MasterDataForbiddenException();
        }

        return await shifts.ListBySiteAsync(siteId, cancellationToken);
    }

    public async Task<ShiftSchedule> CreateAsync(
        string? callerRole, Guid siteId, Guid? lineId, string name, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureNotCentral(appMode);
        MasterDataAuthorization.EnsureAdmin(callerRole);
        await EnsureParentsExistAsync(siteId, lineId, cancellationToken);

        var shift = new ShiftSchedule(Guid.Empty, siteId, lineId, name, startTime, endTime);
        await EnsureNoOverlapAsync(shift, excludeId: null, cancellationToken);

        return await shifts.AddAsync(shift, cancellationToken);
    }

    public async Task<ShiftSchedule> RescheduleAsync(
        string? callerRole, Guid id, string name, TimeOnly startTime, TimeOnly endTime, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureNotCentral(appMode);
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var shift = await shifts.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("ShiftSchedule", id);

        shift.Rename(name);
        shift.Reschedule(startTime, endTime);
        await EnsureNoOverlapAsync(shift, id, cancellationToken);

        await shifts.UpdateAsync(shift, cancellationToken);
        return shift;
    }

    public async Task DeleteAsync(string? callerRole, Guid id, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureNotCentral(appMode);
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var shift = await shifts.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("ShiftSchedule", id);
        await shifts.DeleteAsync(shift, cancellationToken);
    }

    private async Task EnsureParentsExistAsync(Guid siteId, Guid? lineId, CancellationToken cancellationToken)
    {
        if (await sites.GetAsync(siteId, cancellationToken) is null)
        {
            throw new MasterDataParentNotFoundException("Site", siteId);
        }

        if (lineId is { } id)
        {
            var line = await lines.GetAsync(id, cancellationToken);
            if (line is null || line.SiteId != siteId)
            {
                throw new MasterDataParentNotFoundException("Line", id);
            }
        }
    }

    private async Task EnsureNoOverlapAsync(ShiftSchedule candidate, Guid? excludeId, CancellationToken cancellationToken)
    {
        var siblings = await shifts.ListBySiteAsync(candidate.SiteId, cancellationToken);
        var conflict = siblings.FirstOrDefault(s => s.Id != excludeId && s.OverlapsWith(candidate));
        if (conflict is not null)
        {
            throw new ShiftOverlapException(conflict.Name);
        }
    }
}
