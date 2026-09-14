namespace DsaPractice.Judge.Messaging;

public sealed class JudgeOptions
{
    public const string SectionName = "Judge";

    /// <summary>
    /// How many submissions the broker may have outstanding with this Judge at once. One by
    /// default: each submission will own a container, and over-fetching would just make messages
    /// wait on this instance while another sits idle.
    /// </summary>
    public ushort Prefetch { get; init; } = 1;

    /// <summary>Until item 11, nothing is actually executed -- see FakeSandboxExecutor.</summary>
    public bool UseFakeExecutor { get; init; } = true;

    /// <summary>Submissions remembered for redelivery detection.</summary>
    public int ProcessedCacheSize { get; init; } = 1000;
}
