namespace DsaPractice.Judge;

// TODO: inject IConnection (RabbitMQ), ISandboxExecutor
public sealed class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // TODO: consume "submission.judge-requested" queue
        // For each message:
        //   1. Resolve ICodeRunner for the submission's Language
        //   2. Run each test case inside a fresh, resource-capped Docker container (see /Sandbox)
        //   3. Aggregate TestCaseResult[] -> SubmissionJudged
        //   4. Publish "submission.judged", ack the original message

        logger.LogInformation("Judge worker started — TODO: wire up RabbitMQ consumer");
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}
