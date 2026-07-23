namespace OeeNew.Application.MasterData;

/// <summary>Thrown when a write is attempted against Site/Line/Machine/ShiftSchedule/ReasonCode master data at a Central (AppMode=Central) instance — AD-4: master data is owned and writable only at its origin site, never proxied through Central even for a global-scope Admin JWT (Story 5.2, UX-DR5).</summary>
public sealed class CentralReadOnlyException() : Exception("This data is read-only at the Central instance. Make this change at the owning Site instance instead.");
