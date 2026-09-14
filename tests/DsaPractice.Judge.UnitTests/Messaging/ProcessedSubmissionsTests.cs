using DsaPractice.Judge.Messaging;
using Xunit;

namespace DsaPractice.Judge.UnitTests.Messaging;

public class ProcessedSubmissionsTests
{
    [Fact]
    public void TryMarkProcessed_FirstTime_IsTrue()
    {
        var processed = new ProcessedSubmissions();

        Assert.True(processed.TryMarkProcessed(Guid.NewGuid()));
    }

    [Fact]
    public void TryMarkProcessed_SameSubmissionTwice_IsFalseTheSecondTime()
    {
        var processed = new ProcessedSubmissions();
        var submissionId = Guid.NewGuid();

        Assert.True(processed.TryMarkProcessed(submissionId));
        Assert.False(processed.TryMarkProcessed(submissionId)); // a redelivery
    }

    [Fact]
    public void TryMarkProcessed_BeyondCapacity_ForgetsTheOldest()
    {
        var processed = new ProcessedSubmissions(capacity: 2);
        var oldest = Guid.NewGuid();

        processed.TryMarkProcessed(oldest);
        processed.TryMarkProcessed(Guid.NewGuid());
        processed.TryMarkProcessed(Guid.NewGuid());

        // Bounded memory is worth more than perfect recall: this guard is best-effort, and the
        // Api's consumer is the real idempotency gate.
        Assert.True(processed.TryMarkProcessed(oldest));
    }
}
