namespace OeeNew.Application.Identity;

/// <summary>Thrown when the central Identity Provider cannot be reached to provision a new user's credential (AC #2).</summary>
public sealed class CredentialProvisioningException(string message) : Exception(message);
