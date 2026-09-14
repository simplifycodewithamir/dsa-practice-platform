using DsaPractice.DataAccess;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace DsaPractice.Api.IntegrationTests.Fixtures;

/// <summary>
/// Tests in this collection share one database and one broker, and a relay pass publishes every
/// pending outbox row it finds -- including rows another test left behind. Any test that asserts
/// "nothing was published" or counts what a pass did has to start from a clean slate.
/// </summary>
internal static class MessagingState
{
    public static async Task ResetAsync(ApiWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DsaPracticeDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);

        await PurgeQueueAsync(factory);
    }

    public static async Task PurgeQueueAsync(ApiWebApplicationFactory factory)
    {
        await using var channel = await factory.RabbitMqConnection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            await channel.QueuePurgeAsync(ApiWebApplicationFactory.JudgeRequestQueue, TestContext.Current.CancellationToken);
        }
        catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == Constants.NotFound)
        {
            // The Api declares the topology on its first publish, so before then there is no queue
            // -- which is itself "nothing has been published".
        }
    }

    public static async Task<BasicGetResult?> TryGetMessageAsync(ApiWebApplicationFactory factory)
    {
        // A failed BasicGet takes the channel down with it, so each attempt gets its own.
        await using var channel = await factory.RabbitMqConnection.CreateChannelAsync(cancellationToken: TestContext.Current.CancellationToken);
        try
        {
            return await channel.BasicGetAsync(
                ApiWebApplicationFactory.JudgeRequestQueue,
                autoAck: true,
                cancellationToken: TestContext.Current.CancellationToken);
        }
        catch (OperationInterruptedException exception) when (exception.ShutdownReason?.ReplyCode == Constants.NotFound)
        {
            return null;
        }
    }
}
