using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OeeNew.Api.Errors;
using OeeNew.Application.Analytics;
using OeeNew.Application.Auth;
using OeeNew.Application.Identity;
using OeeNew.Application.MasterData;
using OeeNew.Application.Production;
using OeeNew.Application.Reports;
using OeeNew.Infrastructure.Identity;
using OeeNew.Infrastructure.Persistence;
using OeeNew.Infrastructure.Production;
using OeeNew.Infrastructure.RealTime;

// Constrained containers (e.g. Render's free tier) hit the OS inotify-instance limit from
// appsettings.json's FileSystemWatcher-based hot-reload, crashing WebApplication.CreateBuilder()
// itself. Config never changes at runtime in a container deploy, so disable reload-on-change —
// this must be set before CreateBuilder() runs, since that's when it's read.
Environment.SetEnvironmentVariable("DOTNET_hostBuilder__reloadConfigOnChange", "false");

var builder = WebApplication.CreateBuilder(args);

// Render (and similar PaaS hosts) assign the listen port via $PORT at runtime.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port))
{
    builder.WebHost.UseUrls($"http://+:{port}");
}

// AppMode: Site | Central (Architecture Spine AD-2) — same binary, different modules enabled.
var appMode = builder.Configuration.GetValue<string>("AppMode") ?? "Site";
builder.Services.AddSingleton(new AppModeInfo(appMode));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<BootstrapAdminOptions>(builder.Configuration.GetSection(BootstrapAdminOptions.SectionName));
builder.Services.Configure<ProductionOptions>(builder.Configuration.GetSection(ProductionOptions.SectionName));

// Central Identity Provider (AD-7): signing keys + token issuance + credential validation.
builder.Services.AddSingleton<IJwtSigningKeyProvider, RsaJwtSigningKeyProvider>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

// User management (Story 1.4): persisted multi-user store is the primary auth path; bootstrap
// Admin (Story 1.1) is kept as a fallback for when no User row exists yet / DB isn't reachable —
// see CompositeUserAuthenticator.
builder.Services.AddScoped<BootstrapUserAuthenticator>();
builder.Services.AddScoped<PersistedUserAuthenticator>();
builder.Services.AddScoped<IUserAuthenticator, CompositeUserAuthenticator>();
builder.Services.AddScoped<ICentralCredentialProvisioner, CentralCredentialProvisioner>();
builder.Services.AddScoped<LoginUseCase>();

// Master data (Story 1.2 — FR-011): EF Core + local Postgres (AD-2, one instance per Site/Central).
builder.Services.AddDbContext<OeeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<ISiteRepository, SiteRepository>();
builder.Services.AddScoped<ILineRepository, LineRepository>();
builder.Services.AddScoped<IMachineRepository, MachineRepository>();
builder.Services.AddScoped<IShiftScheduleRepository, ShiftScheduleRepository>();
builder.Services.AddScoped<IReasonCodeRepository, ReasonCodeRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<SiteManagementUseCase>();
builder.Services.AddScoped<LineManagementUseCase>();
builder.Services.AddScoped<MachineManagementUseCase>();
builder.Services.AddScoped<ShiftScheduleManagementUseCase>();
builder.Services.AddScoped<ReasonCodeManagementUseCase>();
builder.Services.AddScoped<UserManagementUseCase>();

// Production ingestion (Story 2.1 — FR-001/002/003, AD-3): same local Postgres, no Central dependency.
builder.Services.AddScoped<IMachineStateRepository, MachineStateRepository>();
builder.Services.AddScoped<IDowntimeEventRepository, DowntimeEventRepository>();
builder.Services.AddScoped<IQualityRejectRepository, QualityRejectRepository>();
builder.Services.AddScoped<IngestProductionReadingUseCase>();
builder.Services.AddScoped<MachineStatusQueryUseCase>();
builder.Services.AddScoped<RecordDowntimeReasonUseCase>();
builder.Services.AddScoped<RecordQualityRejectUseCase>();

// Opt-in (Production:SimulateSignal): fake a live PLC/gateway feed for demo/deploy environments so
// seeded machines don't all drift into no-signal a minute after boot — see db/init/02_seed.sql.
if (builder.Configuration.GetValue<bool>("Production:SimulateSignal"))
{
    builder.Services.AddHostedService<DemoSignalSimulatorHostedService>();
}

// Loss pie chart (Story 3.1 — FR-019/020/021): read-only aggregation over the same Production tables.
builder.Services.AddScoped<LossBreakdownQueryUseCase>();
builder.Services.AddScoped<LossAreaOptionsQueryUseCase>();

// OEE report (Story 4.1 — FR-016/017/018): aggregated Availability/Performance/Quality over Shift/Day/Week.
builder.Services.AddScoped<OeeReportQueryUseCase>();

// Real-time dashboard (Story 2.2 — FR-004, NFR-1, AD-8): one hub for this site instance.
builder.Services.AddSignalR();
builder.Services.AddScoped<IMachineStatusNotifier, SignalRMachineStatusNotifier>();

builder.Services.AddControllers()
    // UserRole (Story 1.4) as "Admin"/"Manager"/... in JSON, matching the JWT role claim's string form.
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();

builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    // 400s from model binding/validation also use the standard error envelope.
    o.InvalidModelStateResponseFactory = context =>
    {
        var details = context.ModelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        return new BadRequestObjectResult(new ApiErrorResponse
        {
            Code = "VALIDATION_ERROR",
            Message = "One or more fields are invalid.",
            Details = details,
        });
    };
});

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Keep our own claim type names ("role", "site_id", "line_id") intact instead of the
        // legacy WS-Federation remapping (e.g. "role" -> ClaimTypes.Role) applied when this is true.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            // ValidIssuer/ValidAudience/IssuerSigningKeyResolver are wired below via
            // AddOptions<JwtBearerOptions>().Configure<...>, since they need services
            // (IOptions<JwtOptions>, IJwtSigningKeyProvider) not yet resolvable at this point.
        };

        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                return ApiErrorWriter.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    "UNAUTHORIZED", "Authentication is required to access this resource.");
            },
            OnForbidden = context =>
                ApiErrorWriter.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                    "FORBIDDEN", "You do not have permission to access this resource."),
        };
    });

// Resolve the JWKS-backed signing key resolver after the container is built, so it can pull the
// live IJwtSigningKeyProvider singleton (current + previous key, AD-7) instead of a snapshot.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IJwtSigningKeyProvider, IOptions<JwtOptions>>((options, signingKeyProvider, jwtOptions) =>
    {
        options.TokenValidationParameters.ValidIssuer = jwtOptions.Value.Issuer;
        options.TokenValidationParameters.ValidAudience = jwtOptions.Value.Audience;
        options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, kid, _) =>
            signingKeyProvider.GetValidationKeys()
                .Where(k => k.KeyId == kid)
                .Select(k => (SecurityKey)new RsaSecurityKey(k.Rsa) { KeyId = k.KeyId });
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireClaim(OeeClaimTypes.Role, "Admin"))
    .AddPolicy("ReportsAccess", policy => policy.RequireClaim(OeeClaimTypes.Role, "Admin", "Manager", "Viewer"));

// Throttle login attempts against the bootstrap Admin account (brute-force mitigation).
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    });
    options.OnRejected = (context, cancellationToken) =>
        new ValueTask(ApiErrorWriter.WriteAsync(context.HttpContext, StatusCodes.Status429TooManyRequests,
            "TOO_MANY_REQUESTS", "Too many login attempts. Please try again later."));
});

var app = builder.Build();

// Opt-in: apply EF Core migrations at boot. There's no separate migration/job-runner step in this
// deployment (e.g. Render free tier), so this is how the schema gets created/updated in that environment.
if (builder.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var migrationScope = app.Services.CreateScope();
    migrationScope.ServiceProvider.GetRequiredService<OeeDbContext>().Database.Migrate();
}

app.UseExceptionHandler(_ => { });

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Render terminates TLS at the edge and forwards plain HTTP; without this, UseHttpsRedirection()
// can't see the original scheme and redirect-loops.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
});

app.UseHttpsRedirection();

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/.well-known/jwks.json", (IJwtSigningKeyProvider provider) =>
    JwksDocumentBuilder.Build(provider.GetValidationKeys())).AllowAnonymous();

app.MapHub<MachineStatusHub>("/hubs/machine-status").RequireAuthorization();

// SPA client-side routing fallback — only reached when no controller/static-file route matched.
app.MapFallbackToFile("index.html");

app.Run();

public sealed record AppModeInfo(string Mode);

public partial class Program;
