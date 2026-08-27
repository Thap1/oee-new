using OeeNew.Application.Production;
using OeeNew.Domain.Production;

namespace OeeNew.Infrastructure.Production;

/// <summary>
/// Wraps a real <see cref="IMachineStatusNotifier"/> and holds individual status-change calls in
/// memory instead of sending them immediately, so a caller that changes many machines within the
/// same logical batch (the demo signal simulator's tick) can flush them as one grouped broadcast —
/// clients then do one re-render per tick instead of one per machine. Safe to call concurrently
/// (the simulator ingests machines with bounded parallelism); <see cref="FlushAsync"/> is not
/// meant to run concurrently with in-flight <see cref="NotifyMachineStatusChangedAsync"/> calls.
/// </summary>
public sealed class BufferingMachineStatusNotifier(IMachineStatusNotifier inner) : IMachineStatusNotifier
{
    private readonly object gate = new();
    private readonly List<MachineStatusChange> buffered = [];

    public Task NotifyMachineStatusChangedAsync(Guid machineId, MachineStatus status, long counter, DateTimeOffset reportedAt, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            buffered.Add(new MachineStatusChange(machineId, status, counter, reportedAt));
        }

        return Task.CompletedTask;
    }

    public Task NotifyMachineStatusesChangedAsync(IReadOnlyList<MachineStatusChange> changes, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            buffered.AddRange(changes);
        }

        return Task.CompletedTask;
    }

    public Task NotifyDowntimeReasonRecordedAsync(Guid machineId, Guid reasonCodeId, CancellationToken cancellationToken = default) =>
        inner.NotifyDowntimeReasonRecordedAsync(machineId, reasonCodeId, cancellationToken);

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        List<MachineStatusChange> toSend;
        lock (gate)
        {
            if (buffered.Count == 0)
            {
                return;
            }

            toSend = [.. buffered];
            buffered.Clear();
        }

        await inner.NotifyMachineStatusesChangedAsync(toSend, cancellationToken);
    }
}
