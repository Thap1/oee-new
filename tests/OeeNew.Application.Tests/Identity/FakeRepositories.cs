using OeeNew.Application.Identity;
using OeeNew.Domain.Identity;

namespace OeeNew.Application.Tests.Identity;

internal sealed class FakeUserRepository : IUserRepository
{
    private readonly Dictionary<Guid, User> _users = new();

    public Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        var persisted = new User(Guid.NewGuid(), user.Username, user.Role, user.PasswordHash, user.SiteIds, user.LineIds);
        _users[persisted.Id] = persisted;
        return Task.FromResult(persisted);
    }

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.GetValueOrDefault(id));

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        Task.FromResult(_users.Values.FirstOrDefault(u => u.Username == username));

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<User>>(_users.Values.ToList());

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Guid Seed(string username, UserRole role, string passwordHash, Guid[] siteIds, Guid[] lineIds)
    {
        var user = new User(Guid.NewGuid(), username, role, passwordHash, siteIds, lineIds);
        _users[user.Id] = user;
        return user.Id;
    }
}

internal sealed class FakeCentralCredentialProvisioner : ICentralCredentialProvisioner
{
    private readonly bool _reachable;

    public FakeCentralCredentialProvisioner(bool reachable = true)
    {
        _reachable = reachable;
    }

    public Task<string> ProvisionAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        if (!_reachable)
        {
            throw new CredentialProvisioningException("Central Identity Provider is unreachable.");
        }

        return Task.FromResult($"hashed:{password}");
    }
}
