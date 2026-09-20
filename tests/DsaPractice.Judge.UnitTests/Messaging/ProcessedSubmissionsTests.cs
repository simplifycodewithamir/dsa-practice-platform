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

    [Fact]
    public void TryMarkProcessed_DifferentSubmissions_AreIndependent()
    {
        var processed = new ProcessedSubmissions();

        Assert.True(processed.TryMarkProcessed(Guid.NewGuid()));
        Assert.True(processed.TryMarkProcessed(Guid.NewGuid()));
    }

    [Fact]
    public void TryMarkProcessed_WithinCapacity_StillRemembersTheOldest()
    {
        // The counterpart to the eviction test: nothing is forgotten before the bound is reached.
        var processed = new ProcessedSubmissions(capacity: 3);
        var oldest = Guid.NewGuid();

        processed.TryMarkProcessed(oldest);
        processed.TryMarkProcessed(Guid.NewGuid());
        processed.TryMarkProcessed(Guid.NewGuid());

        Assert.False(processed.TryMarkProcessed(oldest));
    }

    [Fact]
    public void TryMarkProcessed_SameSubmissionFromManyThreads_SucceedsExactlyOnce()
    {
        // The broker can hand the same message to several consumer threads at once; exactly one of
        // them may go on to run a sandbox for it.
        var processed = new ProcessedSubmissions();
        var submissionId = Guid.NewGuid();
        var wonTheRace = 0;

        Parallel.For(0, 64, _ =>
        {
            if (processed.TryMarkProcessed(submissionId))
            {
                Interlocked.Increment(ref wonTheRace);
            }
        });

        Assert.Equal(1, wonTheRace);
    }

    [Fact]
    public void TryMarkProcessed_ManySubmissionsFromManyThreads_AdmitsEachOne()
    {
        // Eviction runs under the same lock-free bookkeeping; it must not lose an unrelated id.
        var processed = new ProcessedSubmissions(capacity: 16);
        var admitted = 0;

        Parallel.For(0, 256, _ =>
        {
            if (processed.TryMarkProcessed(Guid.NewGuid()))
            {
                Interlocked.Increment(ref admitted);
            }
        });

        Assert.Equal(256, admitted);
    }
}
