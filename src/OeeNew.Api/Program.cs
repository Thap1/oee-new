using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OeeNew.Api.Errors;
using OeeNew.Application.Auth;
using OeeNew.Application.Identity;
using OeeNew.Application.MasterData;
using OeeNew.Infrastructure.Identity;
using OeeNew.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// AppMode: Site | Central (Architecture Spine AD-2) — same binary, different modules enabled.
var appMode = builder.Configuration.GetValue<string>("AppMode") ?? "Site";
builder.Services.AddSingleton(new AppModeInfo(appMode));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<BootstrapAdminOptions>(builder.Configuration.GetSection(BootstrapAdminOptions.SectionName));

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
    .AddPolicy("AdminOnly", policy => policy.RequireClaim(OeeClaimTypes.Role, "Admin"));

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

app.UseExceptionHandler(_ => { });

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/.well-known/jwks.json", (IJwtSigningKeyProvider provider) =>
    JwksDocumentBuilder.Build(provider.GetValidationKeys())).AllowAnonymous();

app.Run();

public sealed record AppModeInfo(string Mode);

public partial class Program;
