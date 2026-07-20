using Microsoft.EntityFrameworkCore;
using OeeNew.Application.Identity;
using OeeNew.Domain.Identity;

namespace OeeNew.Infrastructure.Persistence;

public sealed class UserRepository(OeeDbContext context) : IUserRepository
{
    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);
        return user;
    }

    public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default) =>
        context.Users.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) =>
        await context.Users.OrderBy(u => u.Username).ToListAsync(cancellationToken);

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default) =>
        context.SaveChangesAsync(cancellationToken);
}
