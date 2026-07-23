using OeeNew.Application;
using OeeNew.Application.Auth;
using OeeNew.Application.Production;
using OeeNew.Domain.MasterData;

namespace OeeNew.Application.MasterData;

/// <summary>Create/deactivate/delete/list downtime Reason Codes under a Site (Story 1.5, AC #1-#3 — FR-014; scope-filtered per Story 1.6, AC #2/#4; hard-delete guard added by Story 2.5, AC #5). Admin-only for writes, re-checked here in addition to the API-layer policy.</summary>
public sealed class ReasonCodeManagementUseCase(IReasonCodeRepository reasonCodes, ISiteRepository sites, IDowntimeEventRepository downtimeEvents, AppModeInfo appMode)
{
    public async Task<IReadOnlyList<ReasonCode>> ListBySiteAsync(CallerScope scope, Guid siteId, CancellationToken cancellationToken = default)
    {
        if (!scope.AllowsSite(siteId))
        {
            throw new MasterDataForbiddenException();
        }

        return await reasonCodes.ListBySiteAsync(siteId, cancellationToken);
    }

    public async Task<ReasonCode> CreateAsync(
        string? callerRole, Guid siteId, string name, LossCategory? lossCategory, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureNotCentral(appMode);
        MasterDataAuthorization.EnsureAdmin(callerRole);

        // A raw API call can omit lossCategory entirely (binds to null here); reject with a clean
        // envelope rather than letting a default enum value (or the DB NOT NULL constraint) do it (AC #1).
        if (lossCategory is null)
        {
            throw new MasterDataValidationException("LossCategory is required.");
        }

        if (await sites.GetAsync(siteId, cancellationToken) is null)
        {
            throw new MasterDataParentNotFoundException("Site", siteId);
        }

        var reasonCode = new ReasonCode(Guid.Empty, siteId, name, lossCategory.Value);
        return await reasonCodes.AddAsync(reasonCode, cancellationToken);
    }

    public async Task<ReasonCode> DeactivateAsync(string? callerRole, Guid id, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureNotCentral(appMode);
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var reasonCode = await reasonCodes.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("ReasonCode", id);
        reasonCode.Deactivate();
        await reasonCodes.UpdateAsync(reasonCode, cancellationToken);
        return reasonCode;
    }

    /// <summary>Hard-delete (Story 2.5, AC #5) — blocked once any DowntimeEvent references it, to preserve historical reporting; Admin's only other option is <see cref="DeactivateAsync"/>.</summary>
    public async Task DeleteAsync(string? callerRole, Guid id, CancellationToken cancellationToken = default)
    {
        MasterDataAuthorization.EnsureNotCentral(appMode);
        MasterDataAuthorization.EnsureAdmin(callerRole);
        var reasonCode = await reasonCodes.GetAsync(id, cancellationToken) ?? throw new MasterDataNotFoundException("ReasonCode", id);

        if (await downtimeEvents.ExistsForReasonCodeAsync(id, cancellationToken))
        {
            throw new MasterDataHasDependentsException("ReasonCode", id, ["existing downtime records"]);
        }

        await reasonCodes.DeleteAsync(reasonCode, cancellationToken);
    }
}
