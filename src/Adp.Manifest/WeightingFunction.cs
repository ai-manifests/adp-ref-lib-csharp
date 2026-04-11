namespace Adp.Manifest;

/// <summary>
/// Spec Section 4: weight(agent, decision_class) = authority × calibration × decay × stake_factor.
/// All methods are pure — no side effects, no mutation.
/// </summary>
public static class WeightingFunction
{
    /// <summary>
    /// Default half-lives per decision class (spec Section 4.3).
    /// </summary>
    private static readonly Dictionary<string, TimeSpan> DefaultHalfLives = new()
    {
        ["code.correctness"] = TimeSpan.FromDays(180),
        ["security.policy"] = TimeSpan.FromDays(90),
        ["api.compatibility"] = TimeSpan.FromDays(30),
        ["code.style"] = TimeSpan.FromDays(365),
    };

    /// <summary>
    /// Fallback half-life when the decision class is not in the defaults table.
    /// </summary>
    private static readonly TimeSpan FallbackHalfLife = TimeSpan.FromDays(90);

    /// <summary>
    /// Computes an agent's weight for a deliberation.
    /// </summary>
    public static double ComputeWeight(
        double authority,
        CalibrationScore calibration,
        string decisionClass,
        StakeMagnitude stakeMagnitude,
        Dictionary<string, TimeSpan>? halfLifeOverrides = null)
    {
        var effectiveCalibration = ApplySampleSizeDiscount(calibration.Value, calibration.SampleSize);
        var decay = ComputeDecay(calibration.Staleness, decisionClass, halfLifeOverrides);
        var stake = StakeFactor(stakeMagnitude);

        return authority * effectiveCalibration * decay * stake;
    }

    /// <summary>
    /// Exponential decay: 2^(−staleness_days / half_life_days).
    /// </summary>
    public static double ComputeDecay(
        TimeSpan staleness,
        string decisionClass,
        Dictionary<string, TimeSpan>? halfLifeOverrides = null)
    {
        var halfLife = GetHalfLife(decisionClass, halfLifeOverrides);
        if (halfLife.TotalDays <= 0) return 1.0;
        return Math.Pow(2.0, -staleness.TotalDays / halfLife.TotalDays);
    }

    /// <summary>
    /// Sample-size discount: value × (1 − 1/(1 + sample_size)).
    /// A high value from few samples is discounted; many samples converge to the raw value.
    /// </summary>
    public static double ApplySampleSizeDiscount(double value, int sampleSize) =>
        value * (1.0 - 1.0 / (1.0 + sampleSize));

    /// <summary>
    /// Maps stake magnitude to a numeric factor (spec Section 4.1).
    /// </summary>
    public static double StakeFactor(StakeMagnitude magnitude) => magnitude switch
    {
        StakeMagnitude.High => 1.00,
        StakeMagnitude.Medium => 0.85,
        StakeMagnitude.Low => 0.50,
        _ => 0.50,
    };

    private static TimeSpan GetHalfLife(
        string decisionClass,
        Dictionary<string, TimeSpan>? overrides)
    {
        if (overrides is not null && overrides.TryGetValue(decisionClass, out var hl))
            return hl;
        return DefaultHalfLives.GetValueOrDefault(decisionClass, FallbackHalfLife);
    }
}
