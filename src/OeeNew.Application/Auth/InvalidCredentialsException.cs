namespace OeeNew.Application.Auth;

/// <summary>Thrown when login credentials do not match the central Identity Provider's records.</summary>
public sealed class InvalidCredentialsException() : Exception("Invalid username or password.");
