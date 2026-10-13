using EPAnalyzer.Statistics.Descriptive;
using EPAnalyzer.Statistics.Distributions;
using EPAnalyzer.Statistics.Regression;

namespace EPAnalyzer.Statistics.Trueness;

/// <summary>Trueness / measurement-uncertainty result.</summary>
public sealed class TruenessResult
{
    public required double N { get; init; }
    public required double MeanX { get; init; }
    public required double MeanY { get; init; }
    public required double Bias { get; init; }
    public required double Sd { get; init; }
    public required double R { get; init; }
    public required double Slope { get; init; }
    public required double Intercept { get; init; }
    public required double Uncertainty { get; init; }
    public required double PercentUncertainty { get; init; }
    public required double RelativeBias { get; init; }
    public required bool IsPreliminary { get; init; }
}

/// <summary>Quality goal set derived from biological variation (Ricos/EFLM).</summary>
public sealed record BiologicalVariationGoals(double CvAnalytical, double Bias, double TotalError, string Level);

/// <summary>
/// Trueness / uncertainty (port of <c>Tru_Class.TTruStats</c>):
/// EQA/reference comparison regression, correlation, bias, measurement
/// uncertainty (2 SD, k = 2) and the Ricos biological-variation goals.
/// </summary>
public static class Trueness
{
    /// <summary>Minimum used points per level before a trueness result is final.</summary>
    public const int MinimumPointsPerLevel = 20;

    public static TruenessResult Calculate(
        IReadOnlyList<DataPoint> points,
        double? targetSd = null,
        int minimumPoints = MinimumPointsPerLevel,
        double coverageFactor = 2.0)
    {
        var acc = new WeightedRegressionAccumulator();
        foreach (var p in points) acc.Add(p.X, p.Y);
        var (slope, intercept) = RegressionMath.Ols(acc);
        var unit = new StatUnit();
        foreach (var p in points) unit.Add(p.Y);

        double sd = targetSd ?? unit.StandardDeviation;
        double uncertainty = coverageFactor * sd;
        double mean = unit.Mean;

        return new TruenessResult
        {
            N = acc.N,
            MeanX = acc.MeanX,
            MeanY = acc.MeanY,
            Bias = acc.Bias,
            Sd = sd,
            R = acc.R,
            Slope = slope,
            Intercept = intercept,
            Uncertainty = uncertainty,
            PercentUncertainty = mean != 0.0 ? 100.0 * uncertainty / Math.Abs(mean) : double.NaN,
            RelativeBias = acc.MeanX != 0.0 ? 100.0 * acc.Bias / Math.Abs(acc.MeanX) : double.NaN,
            IsPreliminary = acc.N < minimumPoints,
        };
    }

    /// <summary>Standard uncertainty from a target SD and a coverage factor k (1 or 2).</summary>
    public static double StandardUncertainty(double sd, double coverageFactor = 2.0) => coverageFactor * sd;

    /// <summary>
    /// Ricos goals: CVa = F*0.25*CVi, Ba = F*0.125*sqrt(CVi^2 + CVg^2),
    /// TEa = Z*CVa + Ba. F = 1 (optimal), 2 (desirable), 3 (minimal);
    /// Z = 1.65 (95%) or 2.33 (99%).
    /// </summary>
    public static BiologicalVariationGoals BiologicalVariation(double cvi, double cvg, double f = 2.0, double z = 1.65)
    {
        double cvAnalytical = f * 0.25 * cvi;
        double bias = f * 0.125 * Math.Sqrt(cvi * cvi + cvg * cvg);
        double totalError = z * cvAnalytical + bias;
        string level = f switch
        {
            <= 1.0 => "Optimal",
            <= 2.0 => "Desirable",
            _ => "Minimal",
        };
        return new BiologicalVariationGoals(cvAnalytical, bias, totalError, level);
    }

    /// <summary>Six Sigma metric: sigma = (TEa - |bias|)/CV.</summary>
    public static double SigmaMetric(double totalErrorAllowable, double bias, double cv)
        => cv > 0 ? (totalErrorAllowable - Math.Abs(bias)) / cv : double.NaN;

    /// <summary>Random-error factor budget: (TEa - systematic error)/random error.</summary>
    public static double RandomErrorFactor(double totalErrorAllowable, double systematicError, double randomError)
        => randomError > 0 ? (totalErrorAllowable - Math.Abs(systematicError)) / randomError : double.NaN;

    /// <summary>Westgard sigma classification.</summary>
    public static string SigmaClassification(double sigma) => sigma switch
    {
        >= 6.0 => "Excellent",
        >= 4.0 => "Good",
        >= 3.0 => "Fair",
        >= 2.0 => "Marginal",
        _ => "Unacceptable",
    };

    /// <summary>Bias confidence interval from the paired differences.</summary>
    public static (double Lower, double Upper) BiasConfidenceInterval(IReadOnlyList<DataPoint> points, double confidence = 0.95)
    {
        var acc = new WeightedRegressionAccumulator();
        foreach (var p in points) acc.Add(p.X, p.Y);
        double t = StatDistributions.TLevel(confidence, Math.Max(1, acc.N - 1));
        double half = t * acc.SeDiff;
        return (acc.Bias - half, acc.Bias + half);
    }
}
