namespace OeeNew.Infrastructure.Persistence;

/// <summary>
/// Single-row (Id always 1) bookkeeping table for the Site-side sync push loop (Story 5.1) — pure
/// infrastructure runtime state, not a Domain entity: no business rule lives here beyond "the last
/// instant we successfully pushed," so a plain EF-mapped class is enough (unlike <c>MachineState</c>,
/// which needed its own out-of-order-drop invariant). Always empty on a Central instance.
/// </summary>
public sealed class SyncCursorRow
{
    public short Id { get; set; }
    public DateTimeOffset? LastPushedAt { get; set; }
}
