using System.IdentityModel.Tokens.Jwt;
using System.Text;
using DsaPractice.Api.Auth;
using DsaPractice.Api.Configuration;
using DsaPractice.Api.DataAccess;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddDbContext<DsaPracticeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DsaPractice")));

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails(options =>
{
    // UseStatusCodePages-generated responses (routing misses, wrong verb, ...) never throw, so
    // GlobalExceptionHandler never sets a title for them -- the framework default is the plain
    // HTTP reason phrase ("Not Found"), inconsistent with the "api.error.*" convention every
    // thrown ApiException gets. Backfill it here, but only when nothing threw: GlobalExceptionHandler
    // already sets the correct title itself for the exception path, and it always passes the
    // triggering exception through, so Exception is only null for the no-throw status-code-page path.
    options.CustomizeProblemDetails = context =>
    {
        if (context.Exception is null)
        {
            context.ProblemDetails.Title = (context.ProblemDetails.Status ?? context.HttpContext.Response.StatusCode).ToApiErrorTitle();
        }
    };
});

builder.Services.AddSingleton(TimeProvider.System);
// Explicit registration, not AddValidatorsFromAssemblyContaining<Program>() --
// its assembly scan doesn't reliably discover internal IValidator<T> implementations.
builder.Services.AddScoped<IValidator<CreateSubmissionRequest>, CreateSubmissionRequestValidator>();
builder.Services.AddScoped<IValidator<CreateDevTokenRequest>, CreateDevTokenRequestValidator>();
builder.Services.AddOpenApi();

builder.Services.AddOptions<SubmissionsOptions>()
    .Bind(builder.Configuration.GetSection(SubmissionsOptions.SectionName))
    .Validate(o => o.SupportedLanguages.Length > 0, "Submissions:SupportedLanguages must list at least one language.")
    .ValidateOnStart();

builder.Services.AddScoped<IQuestionsService, QuestionsService>();
builder.Services.AddScoped<ISubmissionsService, SubmissionsService>();

// Resource-server JWT auth -- see the doc comment on AppRoles for the full architecture note
// (why this API signs its own dev tokens instead of validating against a real OAuth2 Authority).
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer), "Jwt:Issuer is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Audience), "Jwt:Audience is required.")
    .Validate(o => Encoding.UTF8.GetByteCount(o.SigningKey) >= 32, "Jwt:SigningKey is required and must be at least 32 bytes (256 bits) for HMAC-SHA256.")
    .ValidateOnStart();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearerOptions.MapInboundClaims = false; // keep short claim names ("sub", "role") as-is, no legacy XML-schema remap
        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = JwtRegisteredClaimNames.Sub,
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddAuthorization();

// TODO: register RabbitMQ publisher, FeatureManagement, OpenTelemetry

var app = builder.Build();

app.UseExceptionHandler();
// Route framework-generated status codes (e.g. a 404 from a failed {id:guid} route
// match, before any endpoint runs) through the same ProblemDetails body as thrown
// ApiExceptions get, instead of leaving them as an empty response.
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
    // Local dev-only token issuer -- never mapped outside Development. See the "auth
    // architecture" note on AppRoles for why this exists instead of a real OAuth2 Authority.
    app.MapGroup("/api/v1/auth").MapAuthEndpoints();
}

app.UseHttpsRedirection();

// Must sit between routing (implicit, before any Map* call below) and endpoint execution:
// UseAuthentication populates HttpContext.User from the request's Bearer token (if any, whether
// or not the matched endpoint requires it), UseAuthorization enforces RequireAuthorization() on
// endpoints that declare it. A failed challenge/forbid here never throws -- it's the same
// no-throw, framework-generated-status-code path UseStatusCodePages exists for, so 401/403 still
// get a consistent ProblemDetails body instead of an empty one.
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/v1/questions").MapQuestionsEndpoints();
app.MapGroup("/api/v1/submissions").MapSubmissionsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
