using EPAnalyzer.Statistics.Distributions;
using EPAnalyzer.Statistics.Regression;

namespace EPAnalyzer.Statistics.Linearity;

/// <summary>Per-level statistics for the EP6 pooled analysis.</summary>
public sealed class Ep6Level
{
    public required double Concentration { get; init; }
    public required IReadOnlyList<double> Replicates { get; init; }
    public double Mean => Replicates.Average();
    public double Variance => Replicates.Count > 1 ? Replicates.Sum(v => (v - Mean) * (v - Mean)) / (Replicates.Count - 1) : 0.0;
    public double Sd => Math.Sqrt(Variance);
    public int Df => Math.Max(0, Replicates.Count - 1);
    public double Cv => Mean != 0.0 ? 100.0 * Sd / Math.Abs(Mean) : double.NaN;
    public double SquaredCv
    {
        get { double r = Mean != 0.0 ? Sd / Mean : 0.0; return r * r; }
    }
}

/// <summary>EP6 pooled result including the Bartlett homogeneity test.</summary>
public sealed class Ep6PooledResult
{
    public required double PooledSd { get; init; }
    public required double PooledCv { get; init; }
    public required double BartlettStatistic { get; init; }
    public required double BartlettCorrection { get; init; }
    public required double BartlettChiSquare { get; init; }
    public required double BartlettPValue { get; init; }
    public required bool Heterogeneous { get; init; }
    public required IReadOnlyList<double> LevelSds { get; init; }
    public required IReadOnlyList<double> LevelCvs { get; init; }
}

/// <summary>Deviation from linearity evaluated at the assigned levels.</summary>
public sealed record DeviationPoint(double X, double Deviation);

/// <summary>
/// EP6 linearity (port of <c>ep6classa.TEP6ClassA</c>):
/// pooled SD/CV with the Bartlett homogeneity test, constant-SD vs constant-CV
/// decision, weighted polynomial regression with w = 1/s^2, and the deviation
/// from linearity (polynomial minus straight line at the assigned levels).
/// </summary>
public static class Ep6Linearity
{
    /// <summary>Pooled SD = sqrt(Sigma df_i*s_i^2 / Sigma df_i) plus the Bartlett test on variances.</summary>
    public static Ep6PooledResult PooledStatistics(IReadOnlyList<Ep6Level> levels)
    {
        var valid = levels.Where(l => l.Df > 0).ToList();
        if (valid.Count == 0) throw new ArgumentException("At least one level with replication is required.", nameof(levels));

        double sumDf = valid.Sum(l => (double)l.Df);
        double pooledVariance = valid.Sum(l => l.Df * l.Variance) / sumDf;
        double pooledSd = Math.Sqrt(Math.Max(0.0, pooledVariance));

        double pooledCvVariance = valid.Sum(l => l.Df * (l.Mean != 0 ? Math.Pow(l.Sd / l.Mean, 2) : 0.0)) / sumDf;
        double pooledCv = 100.0 * Math.Sqrt(Math.Max(0.0, pooledCvVariance));

        // Bartlett: M = (Sigma df) ln(s_p^2) - Sigma df_i ln(s_i^2), C = 1 + (1/(3(k-1)))(Sigma 1/df_i - 1/Sigma df)
        double m = sumDf * Math.Log(pooledVariance);
        double sumReciprocalDf = 0.0;
        foreach (var level in valid)
        {
            if (level.Variance > 0) m -= level.Df * Math.Log(level.Variance);
            sumReciprocalDf += 1.0 / level.Df;
        }

        int k = valid.Count;
        double c = 1.0 + (1.0 / (3.0 * (k - 1))) * (sumReciprocalDf - 1.0 / sumDf);
        double chiSquare = c > 0 ? m / c : double.NaN;
        int bartlettDf = k - 1;
        double p = bartlettDf > 0 && !double.IsNaN(chiSquare) ? StatDistributions.ChiProb(chiSquare, bartlettDf) : double.NaN;

        return new Ep6PooledResult
        {
            PooledSd = pooledSd,
            PooledCv = pooledCv,
            BartlettStatistic = m,
            BartlettCorrection = c,
            BartlettChiSquare = chiSquare,
            BartlettPValue = p,
            Heterogeneous = !double.IsNaN(p) && p < 0.05,
            LevelSds = valid.Select(l => l.Sd).ToArray(),
            LevelCvs = valid.Select(l => l.Cv).ToArray(),
        };
    }

    /// <summary>
    /// Constant-SD vs constant-CV decision: Bartlett p &lt; 0.05 flags heterogeneity;
    /// if the pooled CV is more stable (smaller relative spread of CVs than of SDs)
    /// the weighted regression uses 1/s^2 weights, otherwise 1/CV^2.
    /// </summary>
    public static bool UseWeightedRegression(Ep6PooledResult pooled) => pooled.Heterogeneous;

    /// <summary>
    /// Weighted polynomial regression of the replicate observations with weights
    /// 1/s^2 (EP6 <c>Calculate</c> path through TPRegClass).
    /// </summary>
    public static PolynomialRegressionResult WeightedRegression(IReadOnlyList<Ep6Level> levels, int degree = 1, int maxDegree = 3)
    {
        var (points, groups, weight) = BuildProblem(levels);
        int d = Math.Min(degree, maxDegree);
        return PolynomialRegression.Fit(points, d, groups, weight);
    }

    /// <summary>Deviation from linearity = polynomial fit minus the straight line at each assigned level.</summary>
    public static IReadOnlyList<DeviationPoint> DeviationFromLinearity(
        IReadOnlyList<Ep6Level> levels,
        int degree = 2)
    {
        var (points, groups, weight) = BuildProblem(levels);
        var polynomial = PolynomialRegression.Fit(points, degree, groups, weight);
        return PolynomialRegression.DeviationFromLinearity(polynomial, points)
            .GroupBy(t => t.X)
            .Select(g => new DeviationPoint(g.Key, g.Average(t => t.Deviation)))
            .OrderBy(t => t.X)
            .ToArray();
    }

    /// <summary>Best polynomial degree by lack-of-fit p-value.</summary>
    public static (int Degree, PolynomialRegressionResult Result) BestDegree(IReadOnlyList<Ep6Level> levels, int maxDegree = 3)
    {
        var (points, groups, weight) = BuildProblem(levels);
        return PolynomialRegression.BestDegree(points, groups, maxDegree, weight);
    }

    private static (DataPoint[] Points, IReadOnlyList<IReadOnlyList<double>> Groups, Func<DataPoint, double> Weight) BuildProblem(
        IReadOnlyList<Ep6Level> levels)
    {
        var points = new List<DataPoint>();
        foreach (var level in levels)
        {
            foreach (double value in level.Replicates) points.Add(new DataPoint(level.Concentration, value));
        }

        var groups = levels.Select(l => (IReadOnlyList<double>)l.Replicates).ToArray();
        return (points.ToArray(), groups, p => WeightForLevel(levels, p.X));
    }

    private static double WeightForLevel(IReadOnlyList<Ep6Level> levels, double x)
    {
        var level = levels.OrderBy(l => Math.Abs(l.Concentration - x)).First();
        double variance = level.Variance;
        return variance > 0 ? 1.0 / variance : 1.0;
    }
}
