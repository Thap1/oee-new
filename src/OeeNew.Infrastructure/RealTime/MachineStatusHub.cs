using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace OeeNew.Infrastructure.RealTime;

/// <summary>
/// One hub per site instance (AD-8) — push-only from the server for the MVP, so there are no
/// client-invokable methods. Lives in Infrastructure per the Architecture Spine's Design Paradigm
/// section, which lists "SignalR hub" alongside EF Core/ingestion adapters as Infrastructure's job.
/// </summary>
[Authorize]
public sealed class MachineStatusHub : Hub;
