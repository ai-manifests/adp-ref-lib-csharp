# Adp.Manifest

A .NET 10 reference implementation of the **Agent Deliberation Protocol (ADP)** specification — the consensus protocol that multi-agent systems use to reach calibrated, falsifiable decisions together. ADP defines proposals, weights, tallies, falsification, termination, and reversibility tiers.

This library is one of several reference implementations ([TypeScript](https://github.com/ai-manifests/adp-ref-lib-ts), [Python](https://github.com/ai-manifests/adp-ref-lib-py)) of the same spec. The spec itself is at [adp-manifest.dev](https://adp-manifest.dev) and is the source of truth; this library implements what the spec says.

Zero runtime dependencies beyond `System.Collections.Immutable`.

> **Looking for a runnable agent?** This library is the protocol core — data types, weighting math, and an in-memory orchestrator. For a full federation-ready agent runtime with HTTP endpoints, journal persistence, Ed25519 signing, signed calibration snapshots, ACB pricing, and MCP integration, install [`Adp.Agent`](https://github.com/ai-manifests/adp-agent-csharp) instead.

## Install

```bash
dotnet add package Adp.Manifest
```

Packages are published to the Gitea NuGet feed at `https://git.marketally.com/api/packages/ai-manifests/nuget/index.json`. Configure once in your project's `nuget.config`:

```xml
<configuration>
  <packageSources>
    <add key="ai-manifests" value="https://git.marketally.com/api/packages/ai-manifests/nuget/index.json" />
  </packageSources>
</configuration>
```

Or clone and build from source:

```bash
git clone https://github.com/ai-manifests/adp-ref-lib-csharp.git
cd adp-ref-lib-csharp
dotnet build
```

## Quick example

```csharp
using Adp.Manifest;

var proposal = new Proposal(
    AgentId: "did:adp:test-runner-v1",
    Domain: "code.correctness",
    Vote: Vote.Approve,
    Confidence: 0.82,
    Stake: new Stake(Magnitude: StakeMagnitude.Medium, Domain: "code.correctness"),
    Justification: new Justification(Rationale: "all tests pass", EvidenceRefs: []),
    DissentConditions: []
);

var calibration = new CalibrationScore(Value: 0.78, SampleSize: 42);
double weight = WeightingFunction.ComputeWeight(proposal, calibration);
// weight ≈ 0.82 × 0.78 × StakeFactor(Medium) × ApplySampleSizeDiscount(42)
```

## API

All public types live in the `Adp.Manifest` namespace.

### Enums

`Vote`, `ReversibilityTier`, `DissentConditionStatus`, `TerminationState`, `StakeMagnitude`

### Records

`Amendment`, `DissentCondition`, `VoteRevision`, `ProposalAction`, `BlastRadius`, `DomainClaim`, `Justification`, `Stake`, `Proposal`, `TallyResult`

### Weighting

`WeightingFunction` — static class with:
- `ComputeWeight(proposal, calibration)` — canonical proposal weight per ADP §4.2
- `ComputeDecay(age, halfLife)` — time decay of calibration evidence
- `StakeFactor(magnitude)` — maps `StakeMagnitude` to its numeric factor
- `ApplySampleSizeDiscount(value, sampleSize)` — Wilson-interval sample-size discount

### Orchestration

- `DeliberationConfig` — record describing a deliberation's rules (thresholds, participants, tiers)
- `AgentRegistration` — record for a participating agent
- `DeliberationSnapshot` — record for a point-in-time state of a running deliberation
- `DeliberationOrchestrator` — in-memory state machine that runs a deliberation through proposal → tally → falsification → termination. Intended for prototypes, tests, and embedded-in-process use. For production distributed deliberation, see [`Adp.Agent`](https://github.com/ai-manifests/adp-agent-csharp).

### Calibration source

- `ICalibrationSource` — interface the orchestrator uses to look up per-agent calibration scores. Implementations can back this with in-memory state, a local journal, or a remote registry.

## Testing

```bash
dotnet test
```

## Spec

This library implements the Agent Deliberation Protocol specification. Read the spec at [adp-manifest.dev](https://adp-manifest.dev). If the spec and this library disagree, the spec is correct and this is a bug.

## License

Apache-2.0 — see [`LICENSE`](LICENSE) for the full license text and [`NOTICE`](NOTICE) for attribution.
