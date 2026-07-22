using OeeNew.Application.Auth;
using OeeNew.Application.MasterData;

namespace OeeNew.Application.Analytics;

/// <summary>
/// Production Areas (Lines) the caller may pick in Story 3.1's "view by Area" dropdown (FR-020, NFR-5).
/// Scope-safe by construction, the same "filtering IS the enforcement" reasoning as Story 2.4: every
/// Line here comes from an already-scoped Machine (the same scoped-machines-to-distinct-lines trick as
/// <c>OeeNew.Application.Production.MachineStatusQueryUseCase</c>), so there is no separate check to add.
/// </summary>
public sealed class LossAreaOptionsQueryUseCase(IMachineRepository machines, ILineRepository lines)
{
    public async Task<IReadOnlyList<LossAreaOption>> ListAsync(CallerScope scope, CancellationToken cancellationToken = default)
    {
        var scopedMachines = await machines.ListByScopeAsync(scope, cancellationToken);
        if (scopedMachines.Count == 0)
        {
            return [];
        }

        var lineIds = scopedMachines.Select(m => m.LineId).Distinct().ToList();
        var scopedLines = await lines.ListByIdsAsync(lineIds, cancellationToken);

        return scopedLines
            .Select(l => new LossAreaOption(l.Id, l.Name, l.SiteId))
            .OrderBy(a => a.LineName)
            .ToList();
    }
}
