using EPAnalyzer.Statistics.Distributions;

namespace EPAnalyzer.Statistics.Regression;

/// <summary>Passing-Bablok fit with rank-based confidence intervals.</summary>
public sealed class PassingBablokResult
{
    public required double Slope { get; init; }
    public required double Intercept { get; init; }
    public required double SlopeCiLow { get; init; }
    public required double SlopeCiHigh { get; init; }
    public required double InterceptCiLow { get; init; }
    public required double InterceptCiHigh { get; init; }
    public required double SlopeIqr { get; init; }
    public required int N { get; init; }
    public required IReadOnlyList<double> PairwiseSlopes { get; init; }
    public required IReadOnlyList<double> Intercepts { get; init; }

    public double Predict(double x) => Intercept + Slope * x;
}

/// <summary>
/// Passing-Bablok regression (port of <c>PBClass.TPassingBablokClass</c>):
/// the slope is the median of all pairwise slopes, the intercept the shifted
/// median of Y - slope*X, and the 95% CI is rank-based with the offset
/// 1.96*sqrt(n(n-1)(2n+5)/18).
/// </summary>
public static class PassingBablok
{
    public static PassingBablokResult Calculate(IReadOnlyList<DataPoint> points, double confidence = 0.95)
    {
        if (points.Count < 3) throw new ArgumentException("Passing-Bablok requires at least three points.", nameof(points));

        var (slopes, pairs) = PairwiseSlopes(points);
        if (slopes.Count == 0) throw new InvalidOperationException("All X values are equal; the slope is undefined.");

        double slope = Median(slopes);
        var intercepts = points.Select(p => p.Y - slope * p.X).ToArray();
        double intercept = Median(intercepts);

        // Rank-based slope CI (Passing-Bablok): k = z*sqrt(n(n-1)(2n+5)/18) with n = number
        // of data points, N = n(n-1)/2 pairwise slopes; bounds are s_(k+1) and s_(N-k) (1-based).
        int N = slopes.Count;
        int pointCount = points.Count;
        double offset = StatDistributions.NormalQuantile(0.5 + confidence / 2.0)
                        * Math.Sqrt(pointCount * (pointCount - 1.0) * (2.0 * pointCount + 5.0) / 18.0);
        int k = Math.Clamp((int)Math.Floor(offset), 0, Math.Max(0, N - 1));

        double[] sortedSlopes = slopes.OrderBy(s => s).ToArray();
        double slopeLow = sortedSlopes[k];
        double slopeHigh = sortedSlopes[N - 1 - k];

        // Intercept CI: median(Y - b*X) at the two slope CI bounds (monotone in b).
        double interceptLow = Median(points.Select(p => p.Y - slopeHigh * p.X).ToArray());
        double interceptHigh = Median(points.Select(p => p.Y - slopeLow * p.X).ToArray());

        // IQR of the pairwise slopes
        double p25 = Percentile(sortedSlopes, 25.0);
        double p75 = Percentile(sortedSlopes, 75.0);

        return new PassingBablokResult
        {
            Slope = slope,
            Intercept = intercept,
            SlopeCiLow = slopeLow,
            SlopeCiHigh = slopeHigh,
            InterceptCiLow = interceptLow,
            InterceptCiHigh = interceptHigh,
            SlopeIqr = p75 - p25,
            N = points.Count,
            PairwiseSlopes = slopes,
            Intercepts = intercepts,
        };
    }

    /// <summary>All pairwise slopes s_ij = (Yj - Yi)/(Xj - Xi) for Xi != Xj.</summary>
    public static (List<double> Slopes, int PairCount) PairwiseSlopes(IReadOnlyList<DataPoint> points)
    {
        var slopes = new List<double>();
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                double dx = points[j].X - points[i].X;
                if (dx == 0.0) continue;
                slopes.Add((points[j].Y - points[i].Y) / dx);
            }
        }

        return (slopes, slopes.Count);
    }

    public static double Median(IReadOnlyList<double> values)
    {
        if (values.Count == 0) return double.NaN;
        double[] sorted = values.OrderBy(v => v).ToArray();
        return sorted.Length % 2 == 1 ? sorted[sorted.Length / 2] : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2.0;
    }

    private static double Percentile(double[] sorted, double p)
    {
        if (sorted.Length == 0) return double.NaN;
        if (sorted.Length == 1) return sorted[0];
        double rank = p / 100.0 * (sorted.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        return lo == hi ? sorted[lo] : sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
    }
}
