using DsaPractice.Api.DataAccess;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

builder.Services.AddDbContext<DsaPracticeDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DsaPractice")));

// TODO: register RabbitMQ publisher, FluentValidation, FeatureManagement, OpenTelemetry
// TODO: register global exception handler -> ProblemDetails mapping (see dotnet-production-code skill)

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

// Endpoint groups — one file per resource, per convention. Stubs below, implement in DsaPractice.Api/Endpoints/
// app.MapGroup("/api/v1/questions").MapQuestionsEndpoints();
// app.MapGroup("/api/v1/submissions").MapSubmissionsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
