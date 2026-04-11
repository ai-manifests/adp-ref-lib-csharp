using System.Collections.Immutable;

namespace Adp.Manifest;

/// <summary>
/// Configuration for a deliberation session.
/// </summary>
public sealed record DeliberationConfig(
    int MaxRounds = 3,
    double ParticipationFloor = 0.50,
    double DomainAuthorityVetoThreshold = 0.80,
    double IrreversibleMinAuthority = 0.70,
    Dictionary<string, TimeSpan>? HalfLifeOverrides = null
);

/// <summary>
/// An agent's registration in a deliberation, providing the data
/// needed to compute weight.
/// </summary>
public sealed record AgentRegistration(
    string AgentId,
    double Authority,
    CalibrationScore Calibration,
    string DecisionClass
);

/// <summary>
/// A snapshot of deliberation state at a given round.
/// </summary>
public sealed record DeliberationSnapshot(
    int Round,
    ImmutableDictionary<string, Proposal> Proposals,
    ImmutableDictionary<string, double> Weights,
    TallyResult Tally,
    TerminationState? Termination
);

/// <summary>
/// Orchestrates a deliberation from proposals through belief-update rounds
/// to a terminal state. Functional core — returns new state at each step,
/// never mutates.
/// </summary>
public sealed class DeliberationOrchestrator
{
    private readonly DeliberationConfig _config;

    public DeliberationOrchestrator(DeliberationConfig? config = null)
    {
        _config = config ?? new DeliberationConfig();
    }

    /// <summary>
    /// Computes the weight for each registered agent.
    /// </summary>
    public ImmutableDictionary<string, double> ComputeWeights(
        IEnumerable<AgentRegistration> agents,
        IEnumerable<Proposal> proposals)
    {
        var proposalMap = proposals.ToImmutableDictionary(p => p.AgentId);
        var builder = ImmutableDictionary.CreateBuilder<string, double>();

        foreach (var agent in agents)
        {
            if (!proposalMap.TryGetValue(agent.AgentId, out var proposal))
                continue;

            var weight = WeightingFunction.ComputeWeight(
                agent.Authority,
                agent.Calibration,
                agent.DecisionClass,
                proposal.Stake.Magnitude,
                _config.HalfLifeOverrides);

            builder[agent.AgentId] = weight;
        }

        return builder.ToImmutable();
    }

    /// <summary>
    /// Computes a tally from the current proposal states and weights.
    /// </summary>
    public TallyResult Tally(
        ImmutableDictionary<string, Proposal> proposals,
        ImmutableDictionary<string, double> weights,
        ReversibilityTier tier)
    {
        double approveWeight = 0, rejectWeight = 0, abstainWeight = 0;

        foreach (var (agentId, proposal) in proposals)
        {
            if (!weights.TryGetValue(agentId, out var weight))
                continue;

            switch (proposal.CurrentVote)
            {
                case Vote.Approve:
                    approveWeight += weight;
                    break;
                case Vote.Reject:
                    rejectWeight += weight;
                    break;
                case Vote.Abstain:
                    abstainWeight += weight;
                    break;
            }
        }

        var totalWeight = approveWeight + rejectWeight + abstainWeight;
        var nonAbstainingWeight = approveWeight + rejectWeight;

        var approvalFraction = nonAbstainingWeight > 0
            ? approveWeight / nonAbstainingWeight
            : 0;

        var participationFraction = totalWeight > 0
            ? nonAbstainingWeight / totalWeight
            : 0;

        var threshold = GetThreshold(tier);
        var thresholdMet = approvalFraction >= threshold;
        var participationFloorMet = participationFraction >= _config.ParticipationFloor;

        var domainVetoesClear = CheckDomainVetoes(proposals, weights, tier);

        var converged = thresholdMet && participationFloorMet && domainVetoesClear;

        return new TallyResult(
            ApproveWeight: approveWeight,
            RejectWeight: rejectWeight,
            AbstainWeight: abstainWeight,
            TotalDeliberationWeight: totalWeight,
            ApprovalFraction: approvalFraction,
            ParticipationFraction: participationFraction,
            ThresholdMet: thresholdMet,
            ParticipationFloorMet: participationFloorMet,
            DomainVetoesClear: domainVetoesClear,
            Converged: converged);
    }

    /// <summary>
    /// Determines the termination state when a tally does not converge
    /// and no rounds remain.
    /// </summary>
    public TerminationState DetermineTermination(TallyResult tally, bool hasReversibleSubset)
    {
        if (tally.Converged)
            return TerminationState.Converged;

        return hasReversibleSubset
            ? TerminationState.PartialCommit
            : TerminationState.Deadlocked;
    }

    /// <summary>
    /// Returns the approval threshold for a reversibility tier (spec Section 5.1).
    /// </summary>
    public static double GetThreshold(ReversibilityTier tier) => tier switch
    {
        ReversibilityTier.Reversible => 0.50 + double.Epsilon, // strictly > 50%
        ReversibilityTier.PartiallyReversible => 0.60,
        ReversibilityTier.Irreversible => 2.0 / 3.0,
        _ => 0.50 + double.Epsilon,
    };

    /// <summary>
    /// Checks that no domain authority above the veto threshold is rejecting
    /// (required for partially_reversible and irreversible tiers).
    /// </summary>
    private bool CheckDomainVetoes(
        ImmutableDictionary<string, Proposal> proposals,
        ImmutableDictionary<string, double> weights,
        ReversibilityTier tier)
    {
        if (tier == ReversibilityTier.Reversible)
            return true;

        var vetoThreshold = tier == ReversibilityTier.Irreversible
            ? _config.IrreversibleMinAuthority
            : _config.DomainAuthorityVetoThreshold;

        foreach (var (agentId, proposal) in proposals)
        {
            if (!weights.TryGetValue(agentId, out var weight))
                continue;

            // Agents with weight above the veto threshold cannot be rejecting
            if (weight >= vetoThreshold && proposal.CurrentVote == Vote.Reject)
                return false;
        }

        return true;
    }
}
