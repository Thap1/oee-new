namespace OeeNew.Domain.MasterData;

/// <summary>Thrown when a Site/Line/Machine name fails a domain invariant (blank, or exceeds the column's max length).</summary>
public sealed class MasterDataValidationException(string message) : Exception(message);
