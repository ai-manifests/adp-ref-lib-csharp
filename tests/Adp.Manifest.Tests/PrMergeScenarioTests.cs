using System.Collections.Immutable;
using Adp.Manifest;

namespace Adp.Manifest.Tests;

/// <summary>
/// Spec Section 8 — worked example as an executable test.
/// Three agents deliberate on auto-merging PR #4471.
/// Exercises: initial tally failure, belief-update round with falsification,
/// scanner revision to abstain, convergence with linter as margin,
/// and the counterfactual where the linter abstains triggering partial-commit.
/// </summary>
public class PrMergeScenarioTests
{
    private const string DeliberationId = "dlb_01HMXJ3E9R";

    // --- Agent identifiers ---
    private const string TestRunner = "did:adp:test-runner-v2";
    private const string Scanner = "did:adp:security-scanner-v3";
    private const string Linter = "did:adp:style-linter-v1";

    // --- Shared action ---
    private static readonly ProposalAction MergePr = new(
        Kind: "merge_pull_request",
        Target: "github.com/acme/api#4471",
        Parameters: ImmutableDictionary<string, string>.Empty.Add("strategy", "squash"));

    // --- Agent registrations with authority and calibration data ---
    private static readonly AgentRegistration[] Agents =
    [
        new(TestRunner, Authority: 0.90,
            new CalibrationScore(Value: 0.85, SampleSize: 312, Staleness: TimeSpan.FromDays(18)),
            DecisionClass: "code.correctness"),

        new(Scanner, Authority: 0.85,
            new CalibrationScore(Value: 0.83, SampleSize: 187, Staleness: TimeSpan.FromDays(12)),
            DecisionClass: "security.policy"),

        new(Linter, Authority: 0.30,
            new CalibrationScore(Value: 0.72, SampleSize: 89, Staleness: TimeSpan.FromDays(4)),
            DecisionClass: "code.style"),
    ];

    private static ImmutableDictionary<string, Proposal> BuildInitialProposals()
    {
        var testRunnerProposal = new Proposal(
            ProposalId: "prp_01HMXK4F7G",
            DeliberationId: DeliberationId,
            AgentId: TestRunner,
            Timestamp: DateTimeOffset.Parse("2026-04-11T14:32:09.221Z"),
            Action: MergePr,
            Vote: Vote.Approve,
            Confidence: 0.86,
            DomainClaim: new DomainClaim("code.correctness", "mcp-manifest:test-runner-v2#authorities"),
            ReversibilityTier: ReversibilityTier.PartiallyReversible,
            BlastRadius: new BlastRadius(
                ImmutableList.Create("service:api", "consumers:web,mobile"), 12000, 90),
            Justification: new Justification(
                "All 1,847 tests pass; coverage delta +0.3 pp; no flaky retries.",
                ImmutableList.Create(
                    "journal:dlb_01HMXJ.../evidence/test-run-9912",
                    "ci:github-actions/run/8821443")),
            Stake: new Stake("self", StakeMagnitude.High, CalibrationAtStake: true),
            DissentConditions: ImmutableList.Create(
                DissentCondition.Create("dc_tr_01", "if any test marked critical regresses"),
                DissentCondition.Create("dc_tr_02", "if coverage delta is negative")),
            Revisions: ImmutableList<VoteRevision>.Empty);

        var scannerProposal = new Proposal(
            ProposalId: "prp_01HMXK5A2B",
            DeliberationId: DeliberationId,
            AgentId: Scanner,
            Timestamp: DateTimeOffset.Parse("2026-04-11T14:32:11.443Z"),
            Action: MergePr,
            Vote: Vote.Reject,
            Confidence: 0.79,
            DomainClaim: new DomainClaim("security.policy", "mcp-manifest:security-scanner-v3#authorities"),
            ReversibilityTier: ReversibilityTier.PartiallyReversible,
            BlastRadius: new BlastRadius(
                ImmutableList.Create("service:api", "consumers:web,mobile"), 12000, 90),
            Justification: new Justification(
                "Auth module has 3 code paths not covered by security-focused tests.",
                ImmutableList.Create("scan:sast/run/4410")),
            Stake: new Stake("self", StakeMagnitude.High, CalibrationAtStake: true),
            DissentConditions: ImmutableList.Create(
                DissentCondition.Create("dc_ss_01", "if any code path in auth module remains untested"),
                DissentCondition.Create("dc_ss_02", "if no security-focused test covers the new token validation logic")),
            Revisions: ImmutableList<VoteRevision>.Empty);

        var linterProposal = new Proposal(
            ProposalId: "prp_01HMXK6C3D",
            DeliberationId: DeliberationId,
            AgentId: Linter,
            Timestamp: DateTimeOffset.Parse("2026-04-11T14:32:12.007Z"),
            Action: MergePr,
            Vote: Vote.Approve,
            Confidence: 0.62,
            DomainClaim: new DomainClaim("code.style", "mcp-manifest:style-linter-v1#authorities"),
            ReversibilityTier: ReversibilityTier.PartiallyReversible,
            BlastRadius: new BlastRadius(
                ImmutableList.Create("service:api"), 12000, 90),
            Justification: new Justification(
                "2 minor naming convention deviations, both in non-public internals. No blocking issues.",
                ImmutableList.Create("lint:eslint/run/7782")),
            Stake: new Stake("self", StakeMagnitude.Medium, CalibrationAtStake: true),
            DissentConditions: ImmutableList.Create(
                DissentCondition.Create("dc_sl_01", "if any public API name violates naming convention")),
            Revisions: ImmutableList<VoteRevision>.Empty);

        return ImmutableDictionary<string, Proposal>.Empty
            .Add(TestRunner, testRunnerProposal)
            .Add(Scanner, scannerProposal)
            .Add(Linter, linterProposal);
    }

    [Fact]
    public void Weights_match_spec_section_8_1()
    {
        var orchestrator = new DeliberationOrchestrator();
        var proposals = BuildInitialProposals();
        var weights = orchestrator.ComputeWeights(Agents, proposals.Values);

        // Spec Section 8.1: test-runner ≈ 0.71, scanner ≈ 0.64, linter ≈ 0.18
        Assert.InRange(weights[TestRunner], 0.70, 0.72);
        Assert.InRange(weights[Scanner], 0.63, 0.65);
        Assert.InRange(weights[Linter], 0.17, 0.19);
    }

    [Fact]
    public void Round_0_tally_fails_threshold()
    {
        var orchestrator = new DeliberationOrchestrator();
        var proposals = BuildInitialProposals();
        var weights = orchestrator.ComputeWeights(Agents, proposals.Values);

        var tally = orchestrator.Tally(proposals, weights, ReversibilityTier.PartiallyReversible);

        // Approve: 0.71 + 0.18 = 0.89, Reject: 0.64
        // Approval fraction: 0.89 / 1.53 ≈ 58.2% < 60%
        Assert.False(tally.Converged, "Round 0 should not converge — 58.2% < 60% threshold.");
        Assert.True(tally.ParticipationFloorMet, "All agents voted — floor should be met.");
        Assert.False(tally.ThresholdMet, "Approval fraction should be below 60%.");
        Assert.InRange(tally.ApprovalFraction, 0.57, 0.60);
    }

    [Fact]
    public void After_belief_update_scanner_abstains_and_deliberation_converges()
    {
        var orchestrator = new DeliberationOrchestrator();
        var proposals = BuildInitialProposals();
        var weights = orchestrator.ComputeWeights(Agents, proposals.Values);

        // --- Belief-update round 1 ---
        // Test-runner falsifies scanner's dissent conditions dc_ss_01 and dc_ss_02.
        // Scanner acknowledges both, revises from reject → abstain.
        var updatedScanner = proposals[Scanner]
            .WithDissentCondition("dc_ss_01", dc => dc.Falsify(round: 1, testedBy: TestRunner))
            .WithDissentCondition("dc_ss_02", dc => dc.Falsify(round: 1, testedBy: TestRunner))
            .Revise(
                round: 1,
                newVote: Vote.Abstain,
                newConfidence: null,
                reason: "Both dissent conditions (dc_ss_01, dc_ss_02) falsified by test-runner evidence.");

        var updatedProposals = proposals.SetItem(Scanner, updatedScanner);

        // Verify dissent conditions are falsified and append-only
        Assert.Equal(DissentConditionStatus.Falsified, updatedScanner.DissentConditions[0].Status);
        Assert.Equal(DissentConditionStatus.Falsified, updatedScanner.DissentConditions[1].Status);
        Assert.Equal(1, updatedScanner.DissentConditions[0].TestedInRound);
        Assert.Equal(TestRunner, updatedScanner.DissentConditions[0].TestedBy);

        // Verify revision is recorded
        Assert.Single(updatedScanner.Revisions);
        Assert.Equal(Vote.Reject, updatedScanner.Revisions[0].PriorVote);
        Assert.Equal(Vote.Abstain, updatedScanner.Revisions[0].NewVote);
        Assert.Equal(Vote.Abstain, updatedScanner.CurrentVote);

        // --- Round 1 tally ---
        var tally = orchestrator.Tally(updatedProposals, weights, ReversibilityTier.PartiallyReversible);

        // Approve: 0.71 + 0.18 = 0.89, Abstain: 0.64
        // Non-abstaining: 0.89, Total: 1.53
        // Participation: 0.89 / 1.53 ≈ 58.2% ≥ 50% ✓
        // Approval: 0.89 / 0.89 = 100% ≥ 60% ✓
        Assert.True(tally.Converged, "Deliberation should converge after scanner abstains.");
        Assert.True(tally.ParticipationFloorMet);
        Assert.True(tally.ThresholdMet);
        Assert.True(tally.DomainVetoesClear);
        Assert.Equal(1.0, tally.ApprovalFraction, precision: 2);
        Assert.InRange(tally.ParticipationFraction, 0.57, 0.60);
    }

    [Fact]
    public void Counterfactual_linter_abstains_participation_floor_fails()
    {
        var orchestrator = new DeliberationOrchestrator();
        var proposals = BuildInitialProposals();
        var weights = orchestrator.ComputeWeights(Agents, proposals.Values);

        // Scanner abstains (same as main scenario)
        var updatedScanner = proposals[Scanner]
            .WithDissentCondition("dc_ss_01", dc => dc.Falsify(round: 1, testedBy: TestRunner))
            .WithDissentCondition("dc_ss_02", dc => dc.Falsify(round: 1, testedBy: TestRunner))
            .Revise(1, Vote.Abstain, null, "Dissent conditions falsified.");

        // Linter ALSO abstains
        var updatedLinter = proposals[Linter]
            .Revise(1, Vote.Abstain, null, "Deferring to higher-authority agents.");

        var updatedProposals = proposals
            .SetItem(Scanner, updatedScanner)
            .SetItem(Linter, updatedLinter);

        var tally = orchestrator.Tally(updatedProposals, weights, ReversibilityTier.PartiallyReversible);

        // Approve: 0.71, Abstain: 0.64 + 0.18 = 0.82
        // Non-abstaining: 0.71, Total: 1.53
        // Participation: 0.71 / 1.53 ≈ 46.4% < 50% ✗
        Assert.False(tally.Converged, "Should not converge — participation floor not met.");
        Assert.False(tally.ParticipationFloorMet, "46.4% < 50% participation floor.");
        Assert.True(tally.ThresholdMet, "Approval fraction is 100% among voters.");
        Assert.InRange(tally.ParticipationFraction, 0.45, 0.48);

        // Orchestrator should determine partial-commit or deadlock
        var termination = orchestrator.DetermineTermination(tally, hasReversibleSubset: true);
        Assert.Equal(TerminationState.PartialCommit, termination);

        var terminationNoSubset = orchestrator.DetermineTermination(tally, hasReversibleSubset: false);
        Assert.Equal(TerminationState.Deadlocked, terminationNoSubset);
    }

    [Fact]
    public void Linter_participation_is_the_margin()
    {
        // This test makes explicit what Section 8.5 demonstrates:
        // the linter's 0.18 weight is what tips participation from 46.4% to 58.2%.
        var orchestrator = new DeliberationOrchestrator();
        var proposals = BuildInitialProposals();
        var weights = orchestrator.ComputeWeights(Agents, proposals.Values);

        var scannerAbstains = proposals[Scanner]
            .Revise(1, Vote.Abstain, null, "Conditions falsified.");
        var withLinter = proposals.SetItem(Scanner, scannerAbstains);
        var withoutLinter = withLinter.SetItem(Linter,
            proposals[Linter].Revise(1, Vote.Abstain, null, "Deferred."));

        var tallyWith = orchestrator.Tally(withLinter, weights, ReversibilityTier.PartiallyReversible);
        var tallyWithout = orchestrator.Tally(withoutLinter, weights, ReversibilityTier.PartiallyReversible);

        Assert.True(tallyWith.ParticipationFloorMet, "With linter: above floor.");
        Assert.False(tallyWithout.ParticipationFloorMet, "Without linter: below floor.");
        Assert.True(tallyWith.Converged, "With linter: converges.");
        Assert.False(tallyWithout.Converged, "Without linter: does not converge.");
    }

    [Fact]
    public void Dissent_condition_amendment_is_append_only()
    {
        var proposals = BuildInitialProposals();
        var scanner = proposals[Scanner];

        // Amend dc_ss_01 instead of falsifying
        var amended = scanner.WithDissentCondition("dc_ss_01",
            dc => dc.Amend(
                round: 1,
                newCondition: "if any critical code path in auth module remains untested",
                reason: "Non-critical helper paths excluded after test-runner evidence.",
                triggeredBy: TestRunner));

        var condition = amended.DissentConditions[0];
        Assert.Equal(DissentConditionStatus.Amended, condition.Status);
        Assert.Single(condition.Amendments);
        Assert.Equal("if any code path in auth module remains untested", condition.Condition);
        Assert.Equal("if any critical code path in auth module remains untested",
            condition.Amendments[0].NewCondition);
    }

    [Fact]
    public void Tier_escalation_raises_threshold()
    {
        // If tier escalates from partially_reversible to irreversible,
        // threshold rises from 60% to 66.7%.
        var threshold1 = DeliberationOrchestrator.GetThreshold(ReversibilityTier.PartiallyReversible);
        var threshold2 = DeliberationOrchestrator.GetThreshold(ReversibilityTier.Irreversible);

        Assert.Equal(0.60, threshold1, precision: 2);
        Assert.Equal(0.667, threshold2, precision: 2);
        Assert.True(threshold2 > threshold1,
            "Irreversible threshold must be higher than partially_reversible.");
    }

    [Fact]
    public void Bootstrap_agent_has_near_zero_weight()
    {
        // A new agent with getDefault calibration (0.5, sample_size 0)
        // should have near-zero effective weight due to sample-size discount.
        var calibration = new CalibrationScore(0.5, SampleSize: 0, Staleness: TimeSpan.Zero);
        var weight = WeightingFunction.ComputeWeight(
            authority: 0.90,
            calibration: calibration,
            decisionClass: "code.correctness",
            stakeMagnitude: StakeMagnitude.High);

        // effective_calibration = 0.5 × (1 - 1/(1+0)) = 0.5 × 0 = 0
        Assert.Equal(0.0, weight, precision: 10);
    }
}
