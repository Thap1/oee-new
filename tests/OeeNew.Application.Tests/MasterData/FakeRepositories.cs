using OeeNew.Application.MasterData;
using OeeNew.Domain.MasterData;

namespace OeeNew.Application.Tests.MasterData;

internal sealed class FakeSiteRepository : ISiteRepository
{
    private readonly Dictionary<Guid, Site> _sites = new();

    public Task<Site> AddAsync(Site site, CancellationToken cancellationToken = default)
    {
        var persisted = new Site(Guid.NewGuid(), site.Name);
        _sites[persisted.Id] = persisted;
        return Task.FromResult(persisted);
    }

    public Task<Site?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_sites.GetValueOrDefault(id));

    public Task<IReadOnlyList<Site>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Site>>(_sites.Values.ToList());

    public Task UpdateAsync(Site site, CancellationToken cancellationToken = default)
    {
        _sites[site.Id] = site;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Site site, CancellationToken cancellationToken = default)
    {
        _sites.Remove(site.Id);
        return Task.CompletedTask;
    }

    public Guid Seed(string name)
    {
        var site = new Site(Guid.NewGuid(), name);
        _sites[site.Id] = site;
        return site.Id;
    }
}

internal sealed class FakeLineRepository : ILineRepository
{
    private readonly Dictionary<Guid, Line> _lines = new();

    public Task<Line> AddAsync(Line line, CancellationToken cancellationToken = default)
    {
        var persisted = new Line(Guid.NewGuid(), line.Name, line.SiteId);
        _lines[persisted.Id] = persisted;
        return Task.FromResult(persisted);
    }

    public Task<Line?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_lines.GetValueOrDefault(id));

    public Task<IReadOnlyList<Line>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Line>>(_lines.Values.Where(l => l.SiteId == siteId).ToList());

    public Task UpdateAsync(Line line, CancellationToken cancellationToken = default)
    {
        _lines[line.Id] = line;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Line line, CancellationToken cancellationToken = default)
    {
        _lines.Remove(line.Id);
        return Task.CompletedTask;
    }

    public Guid Seed(string name, Guid siteId)
    {
        var line = new Line(Guid.NewGuid(), name, siteId);
        _lines[line.Id] = line;
        return line.Id;
    }
}

internal sealed class FakeMachineRepository : IMachineRepository
{
    private readonly Dictionary<Guid, Machine> _machines = new();

    public Task<Machine> AddAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        var persisted = new Machine(Guid.NewGuid(), machine.Name, machine.LineId);
        _machines[persisted.Id] = persisted;
        return Task.FromResult(persisted);
    }

    public Task<Machine?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_machines.GetValueOrDefault(id));

    public Task<IReadOnlyList<Machine>> ListByLineAsync(Guid lineId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Machine>>(_machines.Values.Where(m => m.LineId == lineId).ToList());

    public Task UpdateAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        _machines[machine.Id] = machine;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Machine machine, CancellationToken cancellationToken = default)
    {
        _machines.Remove(machine.Id);
        return Task.CompletedTask;
    }

    public Guid Seed(string name, Guid lineId)
    {
        var machine = new Machine(Guid.NewGuid(), name, lineId);
        _machines[machine.Id] = machine;
        return machine.Id;
    }
}

internal sealed class FakeShiftScheduleRepository : IShiftScheduleRepository
{
    private readonly Dictionary<Guid, ShiftSchedule> _shifts = new();

    public Task<ShiftSchedule> AddAsync(ShiftSchedule shift, CancellationToken cancellationToken = default)
    {
        var persisted = new ShiftSchedule(Guid.NewGuid(), shift.SiteId, shift.LineId, shift.Name, shift.StartTime, shift.EndTime);
        _shifts[persisted.Id] = persisted;
        return Task.FromResult(persisted);
    }

    public Task<ShiftSchedule?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_shifts.GetValueOrDefault(id));

    public Task<IReadOnlyList<ShiftSchedule>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ShiftSchedule>>(_shifts.Values.Where(s => s.SiteId == siteId).ToList());

    public Task UpdateAsync(ShiftSchedule shift, CancellationToken cancellationToken = default)
    {
        _shifts[shift.Id] = shift;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ShiftSchedule shift, CancellationToken cancellationToken = default)
    {
        _shifts.Remove(shift.Id);
        return Task.CompletedTask;
    }

    public Guid Seed(Guid siteId, Guid? lineId, string name, TimeOnly startTime, TimeOnly endTime)
    {
        var shift = new ShiftSchedule(Guid.NewGuid(), siteId, lineId, name, startTime, endTime);
        _shifts[shift.Id] = shift;
        return shift.Id;
    }
}

internal sealed class FakeReasonCodeRepository : IReasonCodeRepository
{
    private readonly Dictionary<Guid, ReasonCode> _reasonCodes = new();

    public Task<ReasonCode> AddAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default)
    {
        var persisted = new ReasonCode(Guid.NewGuid(), reasonCode.SiteId, reasonCode.Name, reasonCode.LossCategory);
        _reasonCodes[persisted.Id] = persisted;
        return Task.FromResult(persisted);
    }

    public Task<ReasonCode?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_reasonCodes.GetValueOrDefault(id));

    public Task<IReadOnlyList<ReasonCode>> ListBySiteAsync(Guid siteId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ReasonCode>>(_reasonCodes.Values.Where(r => r.SiteId == siteId).ToList());

    public Task UpdateAsync(ReasonCode reasonCode, CancellationToken cancellationToken = default)
    {
        _reasonCodes[reasonCode.Id] = reasonCode;
        return Task.CompletedTask;
    }

    public Guid Seed(Guid siteId, string name, LossCategory lossCategory)
    {
        var reasonCode = new ReasonCode(Guid.NewGuid(), siteId, name, lossCategory);
        _reasonCodes[reasonCode.Id] = reasonCode;
        return reasonCode.Id;
    }
}
