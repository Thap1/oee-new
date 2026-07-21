using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Tests.Production;

internal sealed class FakeDowntimeEventRepository : IDowntimeEventRepository
{
    private readonly Dictionary<Guid, DowntimeEvent> _events = new();

    public Task<DowntimeEvent> AddAsync(DowntimeEvent downtimeEvent, CancellationToken cancellationToken = default)
    {
        var persisted = new DowntimeEvent(Guid.NewGuid(), downtimeEvent.MachineId, downtimeEvent.StartedAt);
        _events[persisted.Id] = persisted;
        return Task.FromResult(persisted);
    }

    public Task<DowntimeEvent?> GetOpenByMachineIdAsync(Guid machineId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_events.Values.FirstOrDefault(e => e.MachineId == machineId && e.IsOpen));

    public Task UpdateAsync(DowntimeEvent downtimeEvent, CancellationToken cancellationToken = default)
    {
        _events[downtimeEvent.Id] = downtimeEvent;
        return Task.CompletedTask;
    }

    public Task<bool> ExistsForReasonCodeAsync(Guid reasonCodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_events.Values.Any(e => e.ReasonCodeId == reasonCodeId));

    public IReadOnlyList<DowntimeEvent> All => _events.Values.ToList();
}
