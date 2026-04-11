namespace Adp.Manifest;

/// <summary>
/// An agent's vote on a proposed action.
/// </summary>
public enum Vote
{
    Approve,
    Reject,
    Abstain
}

/// <summary>
/// Contextual reversibility classification for a proposal.
/// Drives convergence thresholds (spec Section 5).
/// </summary>
public enum ReversibilityTier
{
    Reversible,
    PartiallyReversible,
    Irreversible
}

/// <summary>
/// Lifecycle status of a dissent condition.
/// </summary>
public enum DissentConditionStatus
{
    Active,
    Falsified,
    Amended,
    Withdrawn
}

/// <summary>
/// How a deliberation terminated.
/// </summary>
public enum TerminationState
{
    Converged,
    PartialCommit,
    Deadlocked
}

/// <summary>
/// Self-declared stake magnitude. Maps to a numeric factor
/// via <see cref="WeightingFunction.StakeFactor"/>.
/// </summary>
public enum StakeMagnitude
{
    Low,
    Medium,
    High
}
