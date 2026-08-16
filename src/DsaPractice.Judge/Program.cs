var builder = Host.CreateApplicationBuilder(args);

// TODO: register RabbitMQ consumer as a BackgroundService
// TODO: register ISandboxExecutor (Docker.DotNet-based), per-language ICodeRunner implementations
// TODO: Serilog + OpenTelemetry, same as Api

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
