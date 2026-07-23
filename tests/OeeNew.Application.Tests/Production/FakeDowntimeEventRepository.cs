using OeeNew.Application.Production;
using OeeNew.Domain.MasterData;
using OeeNew.Domain.Production;

namespace OeeNew.Application.Tests.Production;

internal sealed class FakeDowntimeEventRepository : IDowntimeEventRepository
{
    private readonly Dictionary<Guid, DowntimeEvent> _events = new();

    // Sidesteps modeling the real repository's DowntimeEvent->ReasonCode join (Story 3.1) — same
    // simplification FakeMachineRepository already makes for Machine->Line->Site (_machineSiteIds).
    private readonly List<(Guid MachineId, Guid? ReasonCodeId, LossCategory? LossCategory, long DurationSeconds, DateTimeOffset StartedAt)> _closedSlices = [];

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

    public Task<bool> TryAssignReasonToOpenEventAsync(Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default)
    {
        var openEvent = _events.Values.FirstOrDefault(e => e.MachineId == machineId && e.IsOpen);
        if (openEvent is null)
        {
            return Task.FromResult(false);
        }

        openEvent.AssignReason(reasonCodeId);
        return Task.FromResult(true);
    }

    public Task<bool> ExistsForReasonCodeAsync(Guid reasonCodeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_events.Values.Any(e => e.ReasonCodeId == reasonCodeId));

    public Task<IReadOnlyList<ClosedDowntimeSlice>> ListClosedSlicesAsync(
        IReadOnlyList<Guid> machineIds, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var query = _closedSlices.Where(s => machineIds.Contains(s.MachineId));
        if (date is { } d)
        {
            query = query.Where(s => DateOnly.FromDateTime(s.StartedAt.UtcDateTime) == d);
        }

        return Task.FromResult<IReadOnlyList<ClosedDowntimeSlice>>(
            query.Select(s => new ClosedDowntimeSlice(s.MachineId, s.ReasonCodeId, s.LossCategory, s.DurationSeconds)).ToList());
    }

    public Task<IReadOnlyList<ClosedDowntimeSlice>> ListClosedSlicesInRangeAsync(
        IReadOnlyList<Guid> machineIds, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
    {
        var query = _closedSlices.Where(s => machineIds.Contains(s.MachineId) && s.StartedAt >= start && s.StartedAt < end);

        return Task.FromResult<IReadOnlyList<ClosedDowntimeSlice>>(
            query.Select(s => new ClosedDowntimeSlice(s.MachineId, s.ReasonCodeId, s.LossCategory, s.DurationSeconds)).ToList());
    }

    /// <summary>Seeds a closed event's aggregation-relevant shape directly (Story 3.1 tests) — bypasses the open/close/assign-reason lifecycle above, which isn't what these tests exercise. Auto-generates a distinct ReasonCodeId when a category is given, preserving the "category non-null iff reason non-null" invariant for tests that don't care which specific reason code it is.</summary>
    public void SeedClosed(Guid machineId, LossCategory? lossCategory, long durationSeconds, DateTimeOffset startedAt) =>
        SeedClosed(machineId, lossCategory is null ? null : Guid.NewGuid(), lossCategory, durationSeconds, startedAt);

    /// <summary>Seeds a closed event with an explicit ReasonCodeId (Story 3.2 tests — grouping by reason needs a stable, caller-controlled id, unlike Story 3.1's category-only tests).</summary>
    public void SeedClosed(Guid machineId, Guid? reasonCodeId, LossCategory? lossCategory, long durationSeconds, DateTimeOffset startedAt) =>
        _closedSlices.Add((machineId, reasonCodeId, lossCategory, durationSeconds, startedAt));

    public Task<IReadOnlyList<DowntimeEvent>> ListClosedSince(
        DateTimeOffset since, DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DowntimeEvent>>(
            _events.Values.Where(e => e.EndedAt is { } ended && ended > since && ended <= asOf).ToList());

    public IReadOnlyList<DowntimeEvent> All => _events.Values.ToList();

    /// <summary>Directly inserts a fully-formed (e.g. already-closed) event, bypassing the open/close lifecycle above — used by Sync tests that need entities <see cref="ListClosedSince"/> can find.</summary>
    public void SeedEvent(DowntimeEvent downtimeEvent) => _events[downtimeEvent.Id] = downtimeEvent;
}
