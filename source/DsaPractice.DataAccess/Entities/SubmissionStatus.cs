namespace DsaPractice.Api.DataAccess.Entities;

/// <summary>
/// Where a submission is in its lifecycle -- deliberately separate from <see cref="SubmissionVerdict"/>
/// (what the outcome was). Only moves forward: Pending -> Running -> Completed.
/// </summary>
public enum SubmissionStatus
{
    Pending,   // saved, waiting for the Judge
    Running,   // picked up by the Judge
    Completed  // finished; Verdict is set
}
