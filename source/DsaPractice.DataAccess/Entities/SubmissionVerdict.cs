namespace DsaPractice.Api.DataAccess.Entities;

/// <summary>Outcome of a completed submission -- the first failing test case decides it.</summary>
public enum SubmissionVerdict
{
    Accepted,
    WrongAnswer,
    TimeLimitExceeded,
    MemoryLimitExceeded,
    RuntimeError,      // non-zero exit / unhandled exception
    CompilationError,  // never ran
    InternalError      // the Judge itself failed -- not the user's fault, safe to retry
}
