using EPAnalyzer.Statistics.Descriptive;
using EPAnalyzer.Statistics.Distributions;

namespace EPAnalyzer.Statistics.ReferenceIntervals;

/// <summary>Reference interval with its confidence limits.</summary>
public sealed class ReferenceIntervalResult
{
    public required double Lower { get; init; }
    public required double Upper { get; init; }
    public required double LowerCiLow { get; init; }
    public required double LowerCiHigh { get; init; }
    public required double UpperCiLow { get; init; }
    public required double UpperCiHigh { get; init; }
    public required int N { get; init; }
    public required string Method { get; init; }

    public double Width => Upper - Lower;

    /// <summary>
    /// Confidence-ratio quality measure: half of the sum of the two 90% CI
    /// widths over the reference-interval width; flagged when &gt; 0.30.
    /// </summary>
    public double ConfidenceRatio => Width > 0
        ? 0.5 * ((LowerCiHigh - LowerCiLow) + (UpperCiHigh - UpperCiLow)) / Width
        : double.NaN;

    public bool ConfidenceRatioAcceptable(double limit = 0.30) => ConfidenceRatio <= limit;
}

/// <summary>Verification of a proposed reference interval.</summary>
public sealed record ReferenceIntervalVerification(double PercentOutside, int Outside, int Total, bool Passes, bool Preliminary);

/// <summary>
/// Reference intervals (VRI / ERI / CLSI C28-A2-EP28-A3c):
/// non-parametric order-statistic limits and 90% CIs, parametric
/// (mean +/- 1.96 SD) limits, log-transformed limits, verification and
/// partitioning rules.
/// </summary>
public static class ReferenceIntervals
{
    public const int MinimumVerificationSamples = 20;
    public const int MinimumNonParametricSamples = 120;
    public const int MinimumParametricSamples = 20;

    /// <summary>Verification rule: pass when at most 10% of results fall outside the proposed interval.</summary>
    public static ReferenceIntervalVerification Verify(IReadOnlyList<double> values, double lower, double upper, double maxPercentOutside = 10.0)
    {
        int outside = values.Count(v => v < lower || v > upper);
        double pct = values.Count > 0 ? 100.0 * outside / values.Count : double.NaN;
        return new ReferenceIntervalVerification(
            pct,
            outside,
            values.Count,
            values.Count >= MinimumVerificationSamples && pct < maxPercentOutside,
            values.Count < MinimumVerificationSamples);
    }

    /// <summary>
    /// Non-parametric reference interval from the order statistics with
    /// 90% confidence limits on both limits (CLSI C28 exact formula option).
    /// </summary>
    public static ReferenceIntervalResult NonParametric(IReadOnlyList<double> values, double pLow = 2.5, double pHigh = 97.5, bool exactRanks = true)
    {
        var ordered = values.OrderBy(v => v).ToArray();
        int n = ordered.Length;
        if (n == 0) throw new ArgumentException("No values.", nameof(values));

        double lower = Percentile(ordered, pLow);
        double upper = Percentile(ordered, pHigh);

        var (rankLoLow, rankHiLow) = exactRanks
            ? StatUnitEx.PercentileConfidenceRanks(n, pLow / 100.0)
            : StatUnitEx.PercentileConfidenceRanksNormal(n, pLow / 100.0);
        var (rankLoHigh, rankHiHigh) = exactRanks
            ? StatUnitEx.PercentileConfidenceRanks(n, pHigh / 100.0)
            : StatUnitEx.PercentileConfidenceRanksNormal(n, pHigh / 100.0);

        return new ReferenceIntervalResult
        {
            Lower = lower,
            Upper = upper,
            LowerCiLow = Ordered(ordered, rankLoLow),
            LowerCiHigh = Ordered(ordered, rankHiLow),
            UpperCiLow = Ordered(ordered, rankLoHigh),
            UpperCiHigh = Ordered(ordered, rankHiHigh),
            N = n,
            Method = "Non-parametric",
        };
    }

    /// <summary>
    /// Parametric reference interval mean +/- z*SD with the C28 90% confidence
    /// interval of each limit: se = SD*sqrt(1/n + z^2/(2(n-1))).
    /// </summary>
    public static ReferenceIntervalResult Parametric(IReadOnlyList<double> values, double coverage = 0.95)
    {
        var unit = new StatUnit();
        foreach (double v in values) unit.Add(v);

        double z = coverage >= 0.99 ? StatDistributions.Z995
                 : coverage <= 0.90 ? StatDistributions.Z90
                 : StatDistributions.Z975;
        double mean = unit.Mean;
        double sd = unit.StandardDeviation;
        int n = unit.N;

        double lower = mean - z * sd;
        double upper = mean + z * sd;

        double seLimit = n > 1 ? sd * Math.Sqrt(1.0 / n + z * z / (2.0 * (n - 1))) : double.NaN;
        double ci = StatDistributions.TLevel(0.90, Math.Max(1, n - 1)) * seLimit;

        return new ReferenceIntervalResult
        {
            Lower = lower,
            Upper = upper,
            LowerCiLow = lower - ci,
            LowerCiHigh = lower + ci,
            UpperCiLow = upper - ci,
            UpperCiHigh = upper + ci,
            N = n,
            Method = "Parametric",
        };
    }

    /// <summary>Log-transformed (log-normal) parametric reference interval.</summary>
    public static ReferenceIntervalResult LogTransformed(IReadOnlyList<double> values, double coverage = 0.95)
    {
        var positive = values.Where(v => v > 0).ToArray();
        if (positive.Length == 0) throw new ArgumentException("No positive values.", nameof(values));

        var logs = positive.Select(v => Math.Log(v)).ToArray();
        var unit = new StatUnit();
        foreach (double v in logs) unit.Add(v);

        double z = coverage >= 0.99 ? StatDistributions.Z995 : StatDistributions.Z975;
        double mean = unit.Mean;
        double sd = unit.StandardDeviation;
        int n = unit.N;

        double lower = Math.Exp(mean - z * sd);
        double upper = Math.Exp(mean + z * sd);
        double seLimit = n > 1 ? sd * Math.Sqrt(1.0 / n + z * z / (2.0 * (n - 1))) : double.NaN;
        double ci = StatDistributions.TLevel(0.90, Math.Max(1, n - 1)) * seLimit;

        return new ReferenceIntervalResult
        {
            Lower = lower,
            Upper = upper,
            LowerCiLow = Math.Exp(mean - z * sd - ci),
            LowerCiHigh = Math.Exp(mean - z * sd + ci),
            UpperCiLow = Math.Exp(mean + z * sd - ci),
            UpperCiHigh = Math.Exp(mean + z * sd + ci),
            N = n,
            Method = "Log-transformed",
        };
    }

    /// <summary>Interpolated percentile over a sorted array.</summary>
    public static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 0) return double.NaN;
        if (sorted.Count == 1) return sorted[0];
        double rank = p / 100.0 * (sorted.Count - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        return lo == hi ? sorted[lo] : sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
    }

    private static double Ordered(IReadOnlyList<double> sorted, int rank)
    {
        int index = Math.Clamp(rank - 1, 0, sorted.Count - 1);
        return sorted[index];
    }

    /// <summary>
    /// Harris-Boyd partitioning test: partition when the subgroup means are
    /// separated by z &gt; 3 AND the difference exceeds 0.25 of the total
    /// reference interval (standard two-part rule).
    /// </summary>
    public static (double Z, bool Partition, bool SignificantDifference, bool PracticalDifference) HarrisBoydPartition(
        IReadOnlyList<double> groupA,
        IReadOnlyList<double> groupB,
        double zCritical = 3.0,
        double widthFraction = 0.25)
    {
        var ua = new StatUnit();
        foreach (double v in groupA) ua.Add(v);
        var ub = new StatUnit();
        foreach (double v in groupB) ub.Add(v);

        double se = Math.Sqrt(ua.Variance / ua.N + ub.Variance / ub.N);
        double z = se > 0 ? Math.Abs(ua.Mean - ub.Mean) / se : 0.0;
        bool significant = z > zCritical;

        var all = new StatUnit();
        foreach (double v in groupA) all.Add(v);
        foreach (double v in groupB) all.Add(v);
        double width = 2.0 * StatDistributions.Z975 * all.StandardDeviation;
        bool practical = width > 0 && Math.Abs(ua.Mean - ub.Mean) > widthFraction * width;

        return (z, significant && practical, significant, practical);
    }

    /// <summary>
    /// Reed/Dixon outlier test: Q = gap/range with the classic critical values
    /// for n = 3..30; the extreme point is an outlier when Q exceeds the limit.
    /// </summary>
    public static (double Q, bool IsOutlier) DixonOutlier(IReadOnlyList<double> values, bool testHigh = true, double alpha = 0.05)
    {
        var ordered = values.OrderBy(v => v).ToArray();
        int n = ordered.Length;
        if (n < 3 || n > 30) return (double.NaN, false);

        double range = ordered[^1] - ordered[0];
        if (range <= 0) return (0.0, false);

        double gap = testHigh ? ordered[^1] - ordered[^2] : ordered[1] - ordered[0];
        double q = gap / range;

        double critical = DixonCritical(n, alpha);
        return (q, q > critical);
    }

    private static double DixonCritical(int n, double alpha)
    {
        // Classic Dixon table (alpha = 0.05, two-sided critical values for r10-like gaps).
        double[] table =
        [
            0.0, 0.0, 0.941, 0.765, 0.642, 0.560, 0.507, 0.468, 0.437, 0.412,
            0.392, 0.376, 0.361, 0.349, 0.338, 0.329, 0.320, 0.313, 0.306, 0.300,
            0.295, 0.290, 0.285, 0.281, 0.277, 0.273, 0.269, 0.266, 0.263, 0.260
        ];
        return n <= 30 ? table[n] : 0.260;
    }
}
