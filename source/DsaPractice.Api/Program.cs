using DsaPractice.Api.Configuration;
using DsaPractice.Api.DataAccess;
using DsaPractice.Api.Endpoints;
using DsaPractice.Api.Exceptions;
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
builder.Services.AddProblemDetails();

builder.Services.AddSingleton(TimeProvider.System);
// Explicit registration, not AddValidatorsFromAssemblyContaining<Program>() --
// its assembly scan doesn't reliably discover internal IValidator<T> implementations.
builder.Services.AddScoped<IValidator<CreateSubmissionRequest>, CreateSubmissionRequestValidator>();
builder.Services.AddOpenApi();

builder.Services.AddOptions<SubmissionsOptions>()
    .Bind(builder.Configuration.GetSection(SubmissionsOptions.SectionName))
    .Validate(o => o.SupportedLanguages.Length > 0, "Submissions:SupportedLanguages must list at least one language.")
    .ValidateOnStart();

builder.Services.AddScoped<IQuestionsService, QuestionsService>();
builder.Services.AddScoped<ISubmissionsService, SubmissionsService>();

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
}

app.UseHttpsRedirection();

app.MapGroup("/api/v1/questions").MapQuestionsEndpoints();
app.MapGroup("/api/v1/submissions").MapSubmissionsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

public partial class Program;
