using EPAnalyzer.Statistics.Descriptive;
using EPAnalyzer.Statistics.Distributions;
using EPAnalyzer.Statistics.Regression;

namespace EPAnalyzer.Statistics.QualityControl;

/// <summary>Carryover result.</summary>
public sealed record CarryoverResult(double Carryover, double Limit, bool Passes, double SdLowLow, double PercentCarryover);

/// <summary>
/// Carryover, interference (EP7) and stability evaluations
/// (CO / INT / STABILITY modules).
/// </summary>
public static class QcEvaluations
{
    /// <summary>
    /// Carryover = mean(High-Low) - mean(Low-Low); the error limit defaults to
    /// 3 * SD(Low-Low) and the evaluation passes when carryover &lt;= limit.
    /// </summary>
    public static CarryoverResult Carryover(
        IReadOnlyList<double> highLow,
        IReadOnlyList<double> lowLow,
        double? limit = null,
        IReadOnlyList<double>? lowHigh = null)
    {
        var hl = new StatUnit();
        foreach (double v in highLow) hl.Add(v);
        var ll = new StatUnit();
        foreach (double v in lowLow) ll.Add(v);

        double carryover = hl.Mean - ll.Mean;
        double errorLimit = limit ?? 3.0 * ll.StandardDeviation;
        double? percent = lowHigh is { Count: > 0 }
            ? carryover / (lowHigh.Average() - ll.Mean) * 100.0
            : null;

        return new CarryoverResult(carryover, errorLimit, Math.Abs(carryover) <= errorLimit, ll.StandardDeviation, percent ?? double.NaN);
    }

    /// <summary>
    /// EP7 interference: paired difference mean(X) - mean(0) with the t-based
    /// 95% confidence interval; the effect is the fitted dose-response value at
    /// X minus the fitted value at 0.
    /// </summary>
    public static (double MeanDifference, double Lower, double Upper, bool Significant) InterferencePairedDifference(
        IReadOnlyList<double> withInterferent,
        IReadOnlyList<double> withoutInterferent,
        double confidence = 0.95)
    {
        if (withInterferent.Count != withoutInterferent.Count)
            throw new ArgumentException("Paired samples must have the same length.", nameof(withoutInterferent));

        var diffs = withInterferent.Zip(withoutInterferent, (a, b) => a - b).ToArray();
        var unit = new StatUnit();
        foreach (double d in diffs) unit.Add(d);

        double mean = unit.Mean;
        if (unit.N < 2) return (mean, mean, mean, false);

        double t = StatDistributions.TLevel(confidence, unit.N - 1);
        double half = t * unit.Sem;
        return (mean, mean - half, mean + half, Math.Sign(mean - half) == Math.Sign(mean + half) && mean != 0);
    }

    /// <summary>Fitted interference effect: fitted(X) - fitted(0) from a polynomial dose-response fit.</summary>
    public static double FittedInterferenceEffect(PolynomialFit fit, double x) => fit.Predict(x) - fit.Predict(0.0);

    /// <summary>Stability regression result: slope, intercept and the time at which the drift reaches the allowed change.</summary>
    public sealed record StabilityResult(double Slope, double Intercept, double SlopeCiLow, double SlopeCiHigh, bool SlopeSignificant, double? CrossingTime);

    /// <summary>
    /// Stability vs time: fit value = intercept + slope*time (or log(value) for
    /// first-order kinetics); the slope is significant when its 95% CI excludes
    /// zero, and the crossing time is the earliest time at which the fitted
    /// curve crosses the allowable drift.
    /// </summary>
    public static StabilityResult Stability(
        IReadOnlyList<(double Time, double Value)> measurements,
        double? allowableDrift = null,
        bool firstOrderKinetics = false,
        double confidence = 0.95)
    {
        var points = measurements.Select(m => new DataPoint(m.Time, firstOrderKinetics ? Math.Log(m.Value) : m.Value)).ToArray();
        var fit = RegressionMath.Ols(points, confidence);
        bool significant = fit.SlopeCiLow > 0 || fit.SlopeCiHigh < 0;

        double? crossing = null;
        if (allowableDrift is { } drift && fit.Slope != 0)
        {
            double baseline = measurements.MinBy(m => m.Time).Value;
            double target = firstOrderKinetics ? Math.Log(Math.Max(1e-12, baseline + drift)) : baseline + drift;
            double t = (target - fit.Intercept) / fit.Slope;
            crossing = t >= 0 ? t : null;
        }

        return new StabilityResult(fit.Slope, fit.Intercept, fit.SlopeCiLow, fit.SlopeCiHigh, significant, crossing);
    }

    /// <summary>One-way ANOVA over instrument groups (2IC / MIC evaluation).</summary>
    public static MethodComparison.OneWayAnovaResult InstrumentComparison(IReadOnlyList<IReadOnlyList<double>> instruments)
        => MethodComparison.Anova.OneWay(instruments);

    /// <summary>EP26 error index: EI = (Y - X)/rejectionLimit; the point fails when |EI| &gt; 1.</summary>
    public static double ErrorIndex(double value, double target, double rejectionLimit)
        => rejectionLimit != 0 ? (value - target) / rejectionLimit : double.NaN;

    /// <summary>EP26 / 2IC failure rule: one point &gt; 1 for N &lt; 20, or &gt; 5% of points for N &gt;= 20.</summary>
    public static bool ErrorIndexPasses(IReadOnlyList<double> errorIndices)
    {
        int n = errorIndices.Count;
        if (n == 0) return false;
        int failures = errorIndices.Count(ei => Math.Abs(ei) > 1.0);
        return n < 20 ? failures == 0 : failures / (double)n <= 0.05;
    }
}
