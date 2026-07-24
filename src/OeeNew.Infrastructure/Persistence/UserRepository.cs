using Microsoft.EntityFrameworkCore;
using Npgsql;
using OeeNew.Application.Identity;
using OeeNew.Domain.Identity;

namespace OeeNew.Infrastructure.Persistence;

public sealed class UserRepository(OeeDbContext context) : IUserRepository
{
    public async Task<User> AddAsync(User user, CancellationToken cancellationToken = default)
    {
        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // A concurrent CreateAsync for the same username won the uniqueness-check race (Story
            // 1.4 review) — surface it as the same exception the pre-check throws, not a generic
            // DbUpdateException, so ApiExceptionHandler maps it to USERNAME_TAKEN instead of CONFLICT.
            throw new UsernameAlreadyTakenException(user.Username);
        }

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
