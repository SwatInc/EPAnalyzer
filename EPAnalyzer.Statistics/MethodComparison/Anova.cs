using EPAnalyzer.Statistics.Distributions;

namespace EPAnalyzer.Statistics.MethodComparison;

/// <summary>One-way ANOVA table (port of the core of <c>anvclass.TANOVA</c>).</summary>
public sealed class OneWayAnovaResult
{
    public required IReadOnlyList<double> GroupMeans { get; init; }
    public required IReadOnlyList<double> GroupStandardDeviations { get; init; }
    public required IReadOnlyList<int> GroupCounts { get; init; }
    public required double GrandMean { get; init; }
    public required double SsBetween { get; init; }
    public required double SsWithin { get; init; }
    public required double SsTotal { get; init; }
    public required int DfBetween { get; init; }
    public required int DfWithin { get; init; }
    public required double MsBetween { get; init; }
    public required double MsWithin { get; init; }
    public required double FStatistic { get; init; }
    public required double FPValue { get; init; }

    /// <summary>Pooled (within-group) standard deviation.</summary>
    public double PooledSd => Math.Sqrt(Math.Max(0.0, MsWithin));

    /// <summary>Pooled coefficient of variation against the grand mean.</summary>
    public double PooledCv => GrandMean != 0.0 ? 100.0 * PooledSd / Math.Abs(GrandMean) : double.NaN;
}

/// <summary>ANOVA and group-comparison helpers used by 2IC / MIC / EP26 evaluation.</summary>
public static class Anova
{
    /// <summary>One-way ANOVA over k groups of replicate values.</summary>
    public static OneWayAnovaResult OneWay(IReadOnlyList<IReadOnlyList<double>> groups)
    {
        var counts = groups.Select(g => g.Count).ToArray();
        int totalN = counts.Sum();
        if (groups.Count < 2 || totalN <= groups.Count)
            throw new ArgumentException("At least two groups with more than one observation in total are required.", nameof(groups));

        var means = groups.Select(g => g.Count > 0 ? g.Average() : double.NaN).ToArray();
        double grandMean = groups.SelectMany(g => g).Average();

        double ssBetween = 0.0;
        double ssWithin = 0.0;
        var sds = new double[groups.Count];
        for (int i = 0; i < groups.Count; i++)
        {
            var g = groups[i];
            if (g.Count == 0) { sds[i] = double.NaN; continue; }
            foreach (double v in g) ssWithin += (v - means[i]) * (v - means[i]);
            ssBetween += g.Count * (means[i] - grandMean) * (means[i] - grandMean);
            sds[i] = g.Count > 1 ? Math.Sqrt(g.Sum(v => (v - means[i]) * (v - means[i])) / (g.Count - 1)) : 0.0;
        }

        int dfBetween = groups.Count - 1;
        int dfWithin = totalN - groups.Count;
        double msBetween = ssBetween / dfBetween;
        double msWithin = dfWithin > 0 ? ssWithin / dfWithin : 0.0;
        double f = msWithin > 0 ? msBetween / msWithin : double.NaN;
        double p = double.IsNaN(f) ? double.NaN : StatDistributions.FProb(f, dfBetween, dfWithin);

        return new OneWayAnovaResult
        {
            GroupMeans = means,
            GroupStandardDeviations = sds,
            GroupCounts = counts,
            GrandMean = grandMean,
            SsBetween = ssBetween,
            SsWithin = ssWithin,
            SsTotal = ssBetween + ssWithin,
            DfBetween = dfBetween,
            DfWithin = dfWithin,
            MsBetween = msBetween,
            MsWithin = msWithin,
            FStatistic = f,
            FPValue = p,
        };
    }

    /// <summary>
    /// EP26-style critical mean difference for two groups:
    /// limit = z * sqrt(MSE*(1/n1 + 1/n2)); with the EP "3 sigma" default z and
    /// the small-sample sqrt(n/240) floor used for the comparison rule.
    /// </summary>
    public static double Compare2Mean(double meanA, double meanB, double mse, int nA, int nB, double z = 3.0)
    {
        double se = Math.Sqrt(mse * (1.0 / nA + 1.0 / nB));
        return z * se;
    }

    /// <summary>
    /// EP26-style F test for two standard deviations: F = s1^2/s2^2 with the
    /// pooled error degrees of freedom; significant when p &lt; alpha.
    /// </summary>
    public static (double F, double PValue, bool Significant) Compare2Sd(double s1, double s2, int df, double alpha = 0.05)
    {
        double f = s2 > 0 ? (s1 * s1) / (s2 * s2) : double.NaN;
        if (double.IsNaN(f)) return (double.NaN, double.NaN, false);
        double p = f >= 1.0 ? StatDistributions.FProb(f, df, df) : StatDistributions.FProb(1.0 / f, df, df);
        return (f, p, p < alpha);
    }

    /// <summary>Confidence interval of a group mean from the pooled MSE.</summary>
    public static (double Lower, double Upper) GroupMeanConfidenceInterval(double mean, double mse, int n, int df, double confidence = 0.95)
    {
        double t = StatDistributions.TLevel(confidence, df);
        double half = t * Math.Sqrt(mse / n);
        return (mean - half, mean + half);
    }
}
