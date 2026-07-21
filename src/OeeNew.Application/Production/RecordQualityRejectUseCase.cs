using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Production;

/// <summary>Records basic reject/scrap quantity for a Machine (Story 2.6, FR-010).</summary>
public sealed class RecordQualityRejectUseCase(IMachineRepository machines, ILineRepository lines, IQualityRejectRepository qualityRejects)
{
    public async Task RecordAsync(CallerScope scope, string? callerRole, Guid machineId, int quantity, CancellationToken cancellationToken = default)
    {
        if (callerRole is not ("Operator" or "Admin"))
        {
            throw new MasterDataForbiddenException();
        }

        var machine = await machines.GetAsync(machineId, cancellationToken)
            ?? throw new MasterDataParentNotFoundException("Machine", machineId);

        if (!scope.IsGlobal)
        {
            var line = await lines.GetAsync(machine.LineId, cancellationToken);
            if (line is null || !scope.AllowsSite(line.SiteId) || !scope.AllowsLine(machine.LineId))
            {
                throw new MasterDataForbiddenException();
            }
        }

        await qualityRejects.AddAsync(new QualityReject(Guid.Empty, machineId, quantity, DateTimeOffset.UtcNow), cancellationToken);
    }
}
