using EPAnalyzer.Statistics.Regression;

namespace EPAnalyzer.Statistics.Linearity;

/// <summary>A linearity specimen: assigned X, recovered value v1 and error estimate v2.</summary>
public readonly record struct LinearityPoint(double X, double V1, double V2);

/// <summary>Result of the wedge (clinical linearity) test.</summary>
public sealed record WedgeResult
{
    public required double Factor { get; init; }
    public required bool IsLinear { get; init; }
    public required double SlopeLow { get; init; }
    public required double SlopeHigh { get; init; }
    public required double Intercept { get; init; }
}

/// <summary>
/// Clinical linearity wedge test (port of <c>clinlin.TClinLin</c>):
/// a line is accepted when it passes through every error bar v1 +/- f*v2;
/// the smallest factor f is found by doubling + bisection.
/// </summary>
public static class ClinicalLinearity
{
    /// <summary>
    /// Checks whether a straight line exists within the factor-f error bars.
    /// slope_hi(i,j) = (u_i - l_j)/(X_j - X_i), slope_lo(i,j) = (l_i - u_j)/(X_j - X_i);
    /// linear iff max(slope_lo) &lt;= min(slope_hi).
    /// </summary>
    public static WedgeResult IsLinear(IReadOnlyList<LinearityPoint> points, double factor)
    {
        double maxLo = double.NegativeInfinity;
        double minHi = double.PositiveInfinity;

        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                double dx = points[j].X - points[i].X;
                if (dx == 0.0) continue;

                double ui = points[i].V1 + factor * points[i].V2;
                double li = points[i].V1 - factor * points[i].V2;
                double uj = points[j].V1 + factor * points[j].V2;
                double lj = points[j].V1 - factor * points[j].V2;

                // Feasible slope for the line through both error bars (i < j):
                // s >= (l_j - u_i)/dx and s <= (u_j - l_i)/dx.
                double slopeHi = (uj - li) / dx;
                double slopeLo = (lj - ui) / dx;

                if (slopeLo > maxLo) maxLo = slopeLo;
                if (slopeHi < minHi) minHi = slopeHi;
            }
        }

        if (double.IsNegativeInfinity(maxLo) || double.IsPositiveInfinity(minHi))
            return new WedgeResult { Factor = factor, IsLinear = false, SlopeLow = double.NaN, SlopeHigh = double.NaN, Intercept = double.NaN };

        bool linear = maxLo <= minHi;
        double slope = linear ? (maxLo + minHi) / 2.0 : double.NaN;

        double intercept = double.NaN;
        if (linear)
        {
            double loIntercept = double.NegativeInfinity;
            double hiIntercept = double.PositiveInfinity;
            foreach (var p in points)
            {
                loIntercept = Math.Max(loIntercept, p.V1 - factor * p.V2 - slope * p.X);
                hiIntercept = Math.Min(hiIntercept, p.V1 + factor * p.V2 - slope * p.X);
            }
            intercept = loIntercept <= hiIntercept
                ? (loIntercept + hiIntercept) / 2.0
                : Descriptive.RobustStatistics.Median(points.Select(p => p.V1 - slope * p.X));
        }

        return new WedgeResult
        {
            Factor = factor,
            IsLinear = linear,
            SlopeLow = maxLo,
            SlopeHigh = minHi,
            Intercept = intercept,
        };
    }

    /// <summary>
    /// Finds the smallest factor for which the data pass the wedge test:
    /// start at the supplied value, double up to 50 times, then bisect 50 times.
    /// </summary>
    public static WedgeResult FindFactor(IReadOnlyList<LinearityPoint> points, double initialFactor = 1e-6)
    {
        double factor = initialFactor <= 0 ? 1e-6 : initialFactor;

        var result = IsLinear(points, factor);
        if (result.IsLinear) return result;

        int doublings = 0;
        while (!result.IsLinear && doublings < 50)
        {
            factor *= 2.0;
            result = IsLinear(points, factor);
            doublings++;
        }

        if (!result.IsLinear)
            return result with { Factor = factor };

        double low = factor / 2.0;
        double high = factor;
        for (int i = 0; i < 50; i++)
        {
            double mid = (low + high) / 2.0;
            var midResult = IsLinear(points, mid);
            if (midResult.IsLinear) { high = mid; result = midResult; }
            else low = mid;
        }

        return result with { Factor = high };
    }

    /// <summary>Percent recovery 100*value/defined (100 when equal, 0 when defined &lt;= 0).</summary>
    public static double PercentRecovery(double value, double defined)
    {
        if (defined <= 0.0) return 0.0;
        if (value == defined) return 100.0;
        return 100.0 * value / defined;
    }

    /// <summary>Mean percent recovery for a replicate set.</summary>
    public static double MeanPercentRecovery(IReadOnlyList<double> values, double defined)
        => defined <= 0.0 ? 0.0 : 100.0 * values.Average() / defined;

    /// <summary>
    /// Allowed SD at a concentration from the control-level table by linear
    /// interpolation between the bracketing controls; one control clamps to its
    /// SD; the result is never negative.
    /// </summary>
    public static double InterpolateControlsForSd(double x, IReadOnlyList<(double X, double Sd)> controls)
    {
        if (controls.Count == 0) return 0.0;
        var ordered = controls.OrderBy(c => c.X).ToArray();
        if (ordered.Length == 1 || x <= ordered[0].X) return Math.Max(0.0, ordered[0].Sd);
        if (x >= ordered[^1].X) return Math.Max(0.0, ordered[^1].Sd);

        for (int i = 0; i < ordered.Length - 1; i++)
        {
            double x0 = ordered[i].X, x1 = ordered[i + 1].X;
            if (x >= x0 && x <= x1)
            {
                double s0 = ordered[i].Sd, s1 = ordered[i + 1].Sd;
                double sd = x1 == x0 ? s0 : (s1 - s0) * (x - x0) / (x1 - x0) + s0;
                return Math.Max(0.0, sd);
            }
        }

        return 0.0;
    }

    /// <summary>Proportional TE/SE/RE band: 0.01 * pct * x.</summary>
    public static (double Lower, double Upper) Band(double x, double percentLow, double percentHigh)
        => (0.01 * percentLow * x, 0.01 * percentHigh * x);

    /// <summary>Accuracy pass: |measured - defined| &lt;= SE limit + half a display unit.</summary>
    public static bool AccuracyPasses(double measured, double defined, double seLimit, int decimals = 2)
        => Math.Abs(measured - defined) <= seLimit + 0.5 * Math.Pow(10, -decimals);

    /// <summary>Precision pass: SD &lt;= RE limit.</summary>
    public static bool PrecisionPasses(double sd, double reLimit) => sd <= reLimit;

    /// <summary>Precision index SD/RE limit.</summary>
    public static double PrecisionIndex(double sd, double reLimit) => reLimit > 0 ? sd / reLimit : double.NaN;

    /// <summary>One-sided upper confidence limit of an SD via the chi-square helper.</summary>
    public static double SdUpperConfidenceLimit(double sd, int df, double alpha = 0.05)
        => df > 0 ? sd * Math.Sqrt(Distributions.StatDistributions.ChiLevel(1.0 - alpha, df) / df) : double.NaN;

    /// <summary>Defined concentration by model type (assigned, key sample × factor, mean × factor, low sample).</summary>
    public static double DefinedConcentration(int modelType, double assigned, double keySample, double factor, double mean, double lowSample)
        => modelType switch
        {
            0 or 1 => assigned,
            2 => keySample * factor,
            3 => mean * factor,
            4 => lowSample,
            _ => assigned,
        };
}
