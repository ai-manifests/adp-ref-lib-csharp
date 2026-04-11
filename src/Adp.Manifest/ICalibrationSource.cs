namespace Adp.Manifest;

/// <summary>
/// Calibration score for an agent in a domain. Returned by
/// <see cref="ICalibrationSource"/>. The weighting function (spec Section 4)
/// applies decay to staleness; this interface returns raw values.
/// </summary>
public sealed record CalibrationScore(
    double Value,
    int SampleSize,
    TimeSpan Staleness
);

/// <summary>
/// Abstract contract for calibration data. Spec Section 4.2.
/// Concrete implementations are provided by the journal spec / reference lib.
/// </summary>
public interface ICalibrationSource
{
    /// <summary>
    /// Returns the calibration score for an agent in a specific domain.
    /// </summary>
    CalibrationScore GetScore(string agentId, string domain);

    /// <summary>
    /// Returns the bootstrap default for a domain. MUST return
    /// value=0.5, sampleSize=0, staleness=zero. New agents enter
    /// with neutral weight that is heavily discounted by sample size.
    /// </summary>
    CalibrationScore GetDefault(string domain) =>
        new(0.5, 0, TimeSpan.Zero);
}
