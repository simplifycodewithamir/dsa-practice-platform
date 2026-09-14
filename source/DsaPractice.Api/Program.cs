using DsaPractice.Messaging;
using System.Text.Json.Serialization;
using DsaPractice.Api.Configuration;
using DsaPractice.DataAccess;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Exceptions;
using DsaPractice.Api.Messaging;
using DsaPractice.Api.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
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

// Enums go over the wire by name ("Medium", "WrongAnswer"), not ordinal -- readable for clients,
// and reordering enum members can't silently change what an existing value means.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSingleton(TimeProvider.System);
// Explicit registration, not AddValidatorsFromAssemblyContaining<Program>() --
// its assembly scan doesn't reliably discover internal IValidator<T> implementations.
builder.Services.AddScoped<IValidator<CreateSubmissionRequest>, CreateSubmissionRequestValidator>();
builder.Services.AddOpenApi();

builder.Services.AddOptions<SubmissionsOptions>()
    .Bind(builder.Configuration.GetSection(SubmissionsOptions.SectionName))
    .Validate(o => o.SupportedLanguages.Length > 0, "Submissions:SupportedLanguages must list at least one language.")
    .ValidateOnStart();

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.Uri, UriKind.Absolute, out _), "RabbitMq:Uri must be an absolute amqp:// URI.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.Exchange), "RabbitMq:Exchange is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.JudgeRequestedQueue), "RabbitMq:JudgeRequestedQueue is required.")
    .ValidateOnStart();

// One connection per process (opened on first publish), channels per publish.
builder.Services.AddSingleton(new RabbitMqClientName("dsa-practice-api"));
builder.Services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();

// Submissions are never published inline -- they are written to the outbox in the same
// transaction, and this relay publishes them (decision D6).
builder.Services.AddOptions<OutboxOptions>()
    .Bind(builder.Configuration.GetSection(OutboxOptions.SectionName))
    .Validate(o => o.PollInterval > TimeSpan.Zero, "Outbox:PollInterval must be greater than zero.")
    .Validate(o => o.BatchSize > 0, "Outbox:BatchSize must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddSingleton<IOutboxWriter, OutboxWriter>();
builder.Services.AddScoped<OutboxProcessor>();
builder.Services.AddHostedService<OutboxRelay>();

// Judge results come back asynchronously; this applies them to the submission.
builder.Services.AddScoped<JudgedResultRecorder>();
builder.Services.AddHostedService<JudgedResultConsumer>();

// The frontend is served from a different origin in production (Cloudflare Pages in front of an
// api. subdomain, decision D2). Allowed origins are configuration, and an empty list means no
// cross-origin caller is allowed at all -- locally the Vite dev server proxies /api instead, so
// the browser sees one origin and never asks.
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
    .WithMethods("GET", "POST")
    .WithHeaders("content-type", "authorization")));

builder.Services.AddScoped<IQuestionsService, QuestionsService>();
builder.Services.AddScoped<ISubmissionsService, SubmissionsService>();

// TODO: register FeatureManagement, OpenTelemetry

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
}

app.UseHttpsRedirection();
app.UseCors();

app.MapGroup("/api/v1/questions").MapQuestionsEndpoints();
app.MapGroup("/api/v1/submissions").MapSubmissionsEndpoints();

app.MapGet("/health", () => TypedResults.Ok(new HealthResponse("ok")));

app.Run();

public partial class Program;
