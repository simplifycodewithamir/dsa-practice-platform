using System.Collections.Concurrent;

namespace DsaPractice.Judge.Messaging;

/// <summary>
/// Best-effort guard against re-running a submission the broker delivered twice, which the outbox's
/// at-least-once delivery makes normal rather than exceptional.
///
/// Deliberately only best-effort: it is in memory, so it forgets on restart, and a second Judge
/// instance has its own. Re-running a submission is wasteful, not wrong -- the real idempotency
/// gate is the Api's result consumer, which owns the database.
/// </summary>
public sealed class ProcessedSubmissions(int capacity = 1000)
{
    private readonly ConcurrentDictionary<Guid, byte> _seen = new();
    private readonly ConcurrentQueue<Guid> _order = new();

    /// <summary>True the first time a submission is seen, false for a redelivery.</summary>
    public bool TryMarkProcessed(Guid submissionId)
    {
        if (!_seen.TryAdd(submissionId, 0))
        {
            return false;
        }

        _order.Enqueue(submissionId);

        // Bounded so a long-running Judge can't grow this without limit.
        while (_order.Count > capacity && _order.TryDequeue(out var oldest))
        {
            _seen.TryRemove(oldest, out _);
        }

        return true;
    }
}
