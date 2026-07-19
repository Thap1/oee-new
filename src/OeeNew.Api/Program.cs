using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OeeNew.Api.Errors;
using OeeNew.Application.Auth;
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
builder.Services.AddSingleton<IUserAuthenticator, BootstrapUserAuthenticator>();
builder.Services.AddScoped<LoginUseCase>();

// Master data (Story 1.2 — FR-011): EF Core + local Postgres (AD-2, one instance per Site/Central).
builder.Services.AddDbContext<OeeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<ISiteRepository, SiteRepository>();
builder.Services.AddScoped<ILineRepository, LineRepository>();
builder.Services.AddScoped<IMachineRepository, MachineRepository>();
builder.Services.AddScoped<IShiftScheduleRepository, ShiftScheduleRepository>();
builder.Services.AddScoped<SiteManagementUseCase>();
builder.Services.AddScoped<LineManagementUseCase>();
builder.Services.AddScoped<MachineManagementUseCase>();
builder.Services.AddScoped<ShiftScheduleManagementUseCase>();

builder.Services.AddControllers();
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
        var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

        // Keep our own claim type names ("role", "site_id", "line_id") intact instead of the
        // legacy WS-Federation remapping (e.g. "role" -> ClaimTypes.Role) applied when this is true.
        options.MapInboundClaims = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            // IssuerSigningKeyResolver is wired below via AddOptions<JwtBearerOptions>().Configure<IJwtSigningKeyProvider>,
            // since it needs the live signing-key singleton (not yet resolvable at this point in the pipeline).
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
    .Configure<IJwtSigningKeyProvider>((options, signingKeyProvider) =>
    {
        options.TokenValidationParameters.IssuerSigningKeyResolver = (_, _, kid, _) =>
            signingKeyProvider.GetValidationKeys()
                .Where(k => k.KeyId == kid)
                .Select(k => (SecurityKey)new RsaSecurityKey(k.Rsa) { KeyId = k.KeyId });
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireClaim(OeeClaimTypes.Role, "Admin"));

var app = builder.Build();

app.UseExceptionHandler(_ => { });

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/.well-known/jwks.json", (IJwtSigningKeyProvider provider) =>
    JwksDocumentBuilder.Build(provider.GetValidationKeys())).AllowAnonymous();

app.Run();

public sealed record AppModeInfo(string Mode);

public partial class Program;
