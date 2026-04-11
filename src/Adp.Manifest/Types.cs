using System.Collections.Immutable;

namespace Adp.Manifest;

/// <summary>
/// A revision to a dissent condition made during a belief-update round.
/// Append-only — originals are never overwritten.
/// </summary>
public sealed record Amendment(
    int Round,
    string NewCondition,
    string Reason,
    string TriggeredBy
);

/// <summary>
/// A pre-declared condition under which an agent would change its vote.
/// Append-only history: status and amendments accumulate, nothing is deleted.
/// </summary>
public sealed record DissentCondition(
    string Id,
    string Condition,
    DissentConditionStatus Status,
    ImmutableList<Amendment> Amendments,
    int? TestedInRound,
    string? TestedBy
)
{
    public static DissentCondition Create(string id, string condition) =>
        new(id, condition, DissentConditionStatus.Active,
            ImmutableList<Amendment>.Empty, null, null);

    public DissentCondition Falsify(int round, string testedBy) =>
        this with
        {
            Status = DissentConditionStatus.Falsified,
            TestedInRound = round,
            TestedBy = testedBy
        };

    public DissentCondition Amend(int round, string newCondition, string reason, string triggeredBy) =>
        this with
        {
            Status = DissentConditionStatus.Amended,
            TestedInRound = round,
            TestedBy = triggeredBy,
            Amendments = Amendments.Add(new Amendment(round, newCondition, reason, triggeredBy))
        };

    public DissentCondition Withdraw() =>
        this with { Status = DissentConditionStatus.Withdrawn };
}

/// <summary>
/// A vote revision recorded during a belief-update round.
/// </summary>
public sealed record VoteRevision(
    int Round,
    Vote PriorVote,
    Vote NewVote,
    double? PriorConfidence,
    double? NewConfidence,
    string Reason,
    DateTimeOffset Timestamp
);

/// <summary>
/// The action being proposed for deliberation.
/// </summary>
public sealed record ProposalAction(
    string Kind,
    string Target,
    ImmutableDictionary<string, string> Parameters
);

/// <summary>
/// Structured blast radius for programmatic comparison.
/// </summary>
public sealed record BlastRadius(
    ImmutableList<string> Scope,
    int EstimatedUsersAffected,
    int RollbackCostSeconds
);

/// <summary>
/// Domain authority claim referencing mcp-manifest.
/// </summary>
public sealed record DomainClaim(
    string Domain,
    string AuthoritySource
);

/// <summary>
/// Justification with summary and evidence references.
/// </summary>
public sealed record Justification(
    string Summary,
    ImmutableList<string> EvidenceRefs
);

/// <summary>
/// Self-declared stake in the outcome.
/// </summary>
public sealed record Stake(
    string DeclaredBy,
    StakeMagnitude Magnitude,
    bool CalibrationAtStake
);

/// <summary>
/// The atomic unit of participation in a deliberation. Immutable once submitted;
/// epistemic movement is recorded in <see cref="DissentConditions"/> amendments
/// and <see cref="Revisions"/> entries, never as mutations of the original fields.
/// </summary>
public sealed record Proposal(
    string ProposalId,
    string DeliberationId,
    string AgentId,
    DateTimeOffset Timestamp,
    ProposalAction Action,
    Vote Vote,
    double Confidence,
    DomainClaim DomainClaim,
    ReversibilityTier ReversibilityTier,
    BlastRadius BlastRadius,
    Justification Justification,
    Stake Stake,
    ImmutableList<DissentCondition> DissentConditions,
    ImmutableList<VoteRevision> Revisions
)
{
    /// <summary>
    /// The agent's current vote, accounting for any revisions.
    /// </summary>
    public Vote CurrentVote =>
        Revisions.IsEmpty ? Vote : Revisions[^1].NewVote;

    /// <summary>
    /// The agent's current confidence, accounting for any revisions.
    /// </summary>
    public double? CurrentConfidence =>
        Revisions.IsEmpty ? Confidence : Revisions[^1].NewConfidence;

    /// <summary>
    /// Records a vote revision for a belief-update round. Returns a new
    /// Proposal with the revision appended — never mutates.
    /// </summary>
    public Proposal Revise(int round, Vote newVote, double? newConfidence, string reason) =>
        this with
        {
            Revisions = Revisions.Add(new VoteRevision(
                round, CurrentVote, newVote,
                CurrentConfidence, newConfidence,
                reason, DateTimeOffset.UtcNow))
        };

    /// <summary>
    /// Updates a specific dissent condition by id. Returns a new Proposal.
    /// </summary>
    public Proposal WithDissentCondition(string conditionId, Func<DissentCondition, DissentCondition> update)
    {
        var idx = DissentConditions.FindIndex(dc => dc.Id == conditionId);
        if (idx < 0)
            throw new ArgumentException($"Dissent condition '{conditionId}' not found.", nameof(conditionId));

        return this with
        {
            DissentConditions = DissentConditions.SetItem(idx, update(DissentConditions[idx]))
        };
    }
}

/// <summary>
/// Result of a single tally computation.
/// </summary>
public sealed record TallyResult(
    double ApproveWeight,
    double RejectWeight,
    double AbstainWeight,
    double TotalDeliberationWeight,
    double ApprovalFraction,
    double ParticipationFraction,
    bool ThresholdMet,
    bool ParticipationFloorMet,
    bool DomainVetoesClear,
    bool Converged
);
