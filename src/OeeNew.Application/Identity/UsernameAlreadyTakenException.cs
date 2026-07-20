namespace OeeNew.Application.Identity;

public sealed class UsernameAlreadyTakenException(string username) : Exception($"Username '{username}' is already taken.")
{
    public string Username { get; } = username;
}
