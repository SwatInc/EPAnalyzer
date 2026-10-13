using EPAnalyzer.Statistics.Regression;

namespace EPAnalyzer.Statistics.MethodComparison;

/// <summary>
/// EP9-A3 regression variants (port of <c>UMC1EP9A3</c>): weighted/unweighted
/// Deming, weighted OLS, jackknife confidence intervals, and the constant
/// SD/CV assumption checks.
/// </summary>
public static class Ep9A3Regressions
{
    /// <summary>
    /// Unweighted Deming (lambda = (SY/SX)^2 supplied by the caller through
    /// <paramref name="errorRatio"/>).
    /// </summary>
    public static LinearFit UnweightedDeming(IReadOnlyList<DataPoint> points, double errorRatio, double confidence = 0.95)
    {
        // errorRatio = lambda = (SD_y/SD_x)^2; recover SDs for the shared estimator.
        double sy = Math.Sqrt(Math.Max(1e-12, errorRatio));
        double sx = 1.0;
        return RegressionMath.Deming(points, sx, sy, confidence);
    }

    /// <summary>Weighted Deming with proportional-error weights w = 1/m^2.</summary>
    public static LinearFit WeightedDeming(IReadOnlyList<DataPoint> points, double errorRatio, double confidence = 0.95)
        => RegressionMath.WeightedDeming(points, errorRatio, confidence);

    /// <summary>Weighted OLS with w = 1/X^2 (points with X = 0 fall back to weight 1).</summary>
    public static LinearFit WeightedOls(IReadOnlyList<DataPoint> points, double confidence = 0.95)
        => RegressionMath.WeightedOls(points, p => p.X != 0 ? 1.0 / (p.X * p.X) : 1.0, confidence);

    /// <summary>Jackknife estimate and SE of the slope for the supplied fit function.</summary>
    public static (double Estimate, double StandardError) JackknifeSlope(
        IReadOnlyList<DataPoint> points,
        Func<IReadOnlyList<DataPoint>, double> slopeEstimator)
        => RegressionMath.Jackknife(points, slopeEstimator);

    /// <summary>Jackknife estimate and SE of the intercept for the supplied fit function.</summary>
    public static (double Estimate, double StandardError) JackknifeIntercept(
        IReadOnlyList<DataPoint> points,
        Func<IReadOnlyList<DataPoint>, double> interceptEstimator)
        => RegressionMath.Jackknife(points, interceptEstimator);

    /// <summary>t-based confidence half-width for a jackknife standard error (df = n-2).</summary>
    public static double JackknifeConfidenceHalfWidth(double standardError, int n, double confidence = 0.95)
        => n > 2 ? Distributions.StatDistributions.TLevel(confidence, n - 2) * standardError : double.NaN;

    /// <summary>
    /// Constant-SD check: split the X range into thirds and compare the residual
    /// dispersion of the low third with the high third. Pass when the ratio lies
    /// in [0.5, 2.0] (EP9-A3 <c>FlexiEvalConstantSD</c>).
    /// </summary>
    public static (double Ratio, bool Pass) ConstantSdCheck(IReadOnlyList<DataPoint> points, RegressionModel model = RegressionModel.Deming, double errorRatio = 1.0)
    {
        var (low, high) = SplitThirds(points);
        double lowSd = ResidualSd(low, model, errorRatio);
        double highSd = ResidualSd(high, model, errorRatio);
        double ratio = highSd > 0 ? lowSd / highSd : (lowSd > 0 ? double.PositiveInfinity : 1.0);
        bool pass = ratio >= 0.5 && ratio <= 2.0;
        return (ratio, pass);
    }

    /// <summary>
    /// Constant-CV check: same thirds ratio as constant SD, plus a flag when the
    /// ratio exceeds 4.0 (EP9-A3 <c>FlexiEvalConstantCV</c>).
    /// </summary>
    public static (double Ratio, bool Pass, bool ExtremeFlag) ConstantCvCheck(IReadOnlyList<DataPoint> points, RegressionModel model = RegressionModel.Deming, double errorRatio = 1.0)
    {
        var (low, high) = SplitThirds(points);
        double lowCv = Cv(low, model, errorRatio);
        double highCv = Cv(high, model, errorRatio);
        double ratio = highCv > 0 ? lowCv / highCv : (lowCv > 0 ? double.PositiveInfinity : 1.0);
        bool pass = ratio >= 0.5 && ratio <= 2.0;
        return (ratio, pass, ratio > 4.0);
    }

    /// <summary>Average bias by the median of the per-pair differences (EvalAvgBias).</summary>
    public static double MedianBias(IReadOnlyList<DataPoint> points)
        => Descriptive.RobustStatistics.Median(points.Select(p => p.Difference));

    /// <summary>
    /// Median difference profile bias at one or more medical decision points,
    /// using the fitted regression: bias(x) = (a + b*x) - x.
    /// </summary>
    public static double BiasAt(LinearFit fit, double x) => fit.Intercept + (fit.Slope - 1.0) * x;

    /// <summary>
    /// Partitioned (grouped) bias for EP9-A3: mean difference per concentration
    /// partition, with the t-based confidence interval of each partition.
    /// </summary>
    public static IReadOnlyList<(double X, double MeanBias, double Lower, double Upper, int N)> PartitionedBias(
        IReadOnlyList<DataPoint> points,
        IReadOnlyList<double> partitionEdges,
        double confidence = 0.95)
    {
        if (partitionEdges.Count < 2) throw new ArgumentException("At least two partition edges are required.", nameof(partitionEdges));

        var result = new List<(double, double, double, double, int)>();
        for (int i = 0; i < partitionEdges.Count - 1; i++)
        {
            double lo = partitionEdges[i];
            double hi = partitionEdges[i + 1];
            var subset = points.Where(p => p.X >= lo && (i == partitionEdges.Count - 2 ? p.X <= hi : p.X < hi)).ToArray();
            if (subset.Length == 0) continue;
            double mean = subset.Average(p => p.Difference);
            double mid = (lo + hi) / 2.0;
            if (subset.Length < 2)
            {
                result.Add((mid, mean, mean, mean, subset.Length));
                continue;
            }

            double sd = Math.Sqrt(subset.Sum(p => (p.Difference - mean) * (p.Difference - mean)) / (subset.Length - 1));
            double se = sd / Math.Sqrt(subset.Length);
            double t = Distributions.StatDistributions.TLevel(confidence, subset.Length - 1);
            result.Add((mid, mean, mean - t * se, mean + t * se, subset.Length));
        }

        return result;
    }

    /// <summary>
    /// Precision ratio check from EP9-A3: the ratio of SDs (= sx/sy) should lie
    /// within [0.33, 3.0] for uniform scatter.
    /// </summary>
    public static (double Ratio, bool UniformScatter) PrecisionRatio(IReadOnlyList<DataPoint> points)
    {
        if (points.Count < 2) return (double.NaN, false);
        double sx = Math.Sqrt(points.Sum(p => (p.X - points.Average(q => q.X)) * (p.X - points.Average(q => q.X))) / (points.Count - 1));
        double sy = Math.Sqrt(points.Sum(p => (p.Y - points.Average(q => q.Y)) * (p.Y - points.Average(q => q.Y))) / (points.Count - 1));
        double ratio = sy > 0 ? sx / sy : double.NaN;
        return (ratio, ratio >= 0.33 && ratio <= 3.0);
    }

    /// <summary>Minimum 39 unexcluded pairs required before a report is final (EP9 rule).</summary>
    public static bool MeetsMinimumPairs(int usedPairs, int minimum = 39) => usedPairs >= minimum;

    private static (DataPoint[] Low, DataPoint[] High) SplitThirds(IReadOnlyList<DataPoint> points)
    {
        var sorted = points.OrderBy(p => p.X).ToArray();
        int third = Math.Max(1, sorted.Length / 3);
        var low = sorted.Take(third).ToArray();
        var high = sorted.Skip(sorted.Length - third).ToArray();
        return (low, high);
    }

    private static double ResidualSd(IReadOnlyList<DataPoint> points, RegressionModel model, double errorRatio)
    {
        if (points.Count < 3) return 0.0;
        var fit = model switch
        {
            RegressionModel.Ols => RegressionMath.Ols(points),
            RegressionModel.WeightedOls => WeightedOls(points),
            RegressionModel.WeightedDeming => WeightedDeming(points, errorRatio),
            _ => UnweightedDeming(points, errorRatio),
        };
        double ss = points.Sum(p => { double r = p.Y - fit.Predict(p.X); return r * r; });
        return Math.Sqrt(ss / (points.Count - 2));
    }

    private static double Cv(IReadOnlyList<DataPoint> points, RegressionModel model, double errorRatio)
    {
        if (points.Count < 3) return 0.0;
        var fit = model switch
        {
            RegressionModel.Ols => RegressionMath.Ols(points),
            RegressionModel.WeightedOls => WeightedOls(points),
            RegressionModel.WeightedDeming => WeightedDeming(points, errorRatio),
            _ => UnweightedDeming(points, errorRatio),
        };
        double mean = points.Average(p => p.Y);
        if (mean == 0) return 0.0;
        double ss = points.Sum(p => { double r = p.Y - fit.Predict(p.X); return r * r; });
        double sd = Math.Sqrt(ss / (points.Count - 2));
        return 100.0 * sd / Math.Abs(mean);
    }
}
