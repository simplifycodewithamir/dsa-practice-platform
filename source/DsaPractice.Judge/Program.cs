using DsaPractice.Judge.Execution;
using DsaPractice.Judge.Messaging;
using DsaPractice.Messaging;
using Microsoft.Extensions.Options;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, config) =>
    config.ReadFrom.Configuration(builder.Configuration).ReadFrom.Services(services));

builder.Services.AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .Validate(o => Uri.TryCreate(o.Uri, UriKind.Absolute, out _), "RabbitMq:Uri must be an absolute amqp:// URI.")
    .ValidateOnStart();

builder.Services.AddOptions<JudgeOptions>()
    .Bind(builder.Configuration.GetSection(JudgeOptions.SectionName))
    .Validate(o => o.Prefetch > 0, "Judge:Prefetch must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddSingleton(new RabbitMqClientName("dsa-practice-judge"));
builder.Services.AddSingleton<IRabbitMqConnection, RabbitMqConnection>();
builder.Services.AddSingleton<IMessagePublisher, RabbitMqPublisher>();
builder.Services.AddSingleton(sp =>
    new ProcessedSubmissions(sp.GetRequiredService<IOptions<JudgeOptions>>().Value.ProcessedCacheSize));

// Until item 11 there is no sandbox, so nothing is executed -- see FakeSandboxExecutor.
builder.Services.AddSingleton<ISandboxExecutor, FakeSandboxExecutor>();

builder.Services.AddHostedService<JudgeRequestConsumer>();

// TODO (item 11): replace FakeSandboxExecutor with the Docker.DotNet sandbox and per-language runners
// TODO (item 23): OpenTelemetry, trace context propagated from the message headers

var host = builder.Build();

if (host.Services.GetRequiredService<IOptions<JudgeOptions>>().Value.UseFakeExecutor)
{
    host.Services.GetRequiredService<ILogger<Program>>().LogWarning(
        "Judge is running with the FAKE executor: submissions are marked Accepted without being run.");
}

host.Run();

/// <summary>Exposed so the integration tests can host the Judge the way the app does.</summary>
public partial class Program;
