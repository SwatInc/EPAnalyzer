using EPAnalyzer.Statistics.Descriptive;

namespace EPAnalyzer.Statistics.Detection;

/// <summary>
/// Limit of blank (LOB) and limit of detection (LOD, CLSI EP17).
/// EP Evaluator implements LOB directly and LOQ separately; LOD is provided
/// here as the standard EP17 combination for completeness.
/// </summary>
public static class LimitOfBlank
{
    /// <summary>LOB = mean(blank) + z*SD(blank), z = 1.645 for 95% (2.0 for the legacy "2 SD" rule).</summary>
    public static (double Lob, double Mean, double Sd, double Z) Calculate(IReadOnlyList<double> blankResponses, double z = 1.645)
    {
        var unit = new StatUnit();
        foreach (double v in blankResponses) unit.Add(v);
        double mean = unit.Mean;
        double sd = unit.StandardDeviation;
        return (mean + z * sd, mean, sd, z);
    }

    /// <summary>LOB with the legacy "mean + 2 SD" rule used by EP Evaluator's LOB module.</summary>
    public static double Calculate2Sd(IReadOnlyList<double> blankResponses)
    {
        var unit = new StatUnit();
        foreach (double v in blankResponses) unit.Add(v);
        return unit.Mean + 2.0 * unit.StandardDeviation;
    }

    /// <summary>
    /// LOD = LOB + z*SD(low sample), z = 1.645 for 95% detection (EP17).
    /// </summary>
    public static (double Lod, double Lob, double SdLow) CalculateLod(
        IReadOnlyList<double> blankResponses,
        IReadOnlyList<double> lowLevelResponses,
        double z = 1.645)
    {
        double lob = Calculate2SdOrZ(blankResponses, z);
        var unit = new StatUnit();
        foreach (double v in lowLevelResponses) unit.Add(v);
        return (lob + z * unit.StandardDeviation, lob, unit.StandardDeviation);
    }

    /// <summary>Probability of detection at a concentration given the LOB and SD of the low sample (normal assumption).</summary>
    public static double DetectionProbability(double concentration, double lod, double sdLow)
    {
        if (sdLow <= 0) return concentration >= lod ? 1.0 : 0.0;
        double z = (lod - concentration) / sdLow;
        return 1.0 - Distributions.StatDistributions.NormalCdf(z);
    }

    /// <summary>Minimum sample sizes recommended by EP17 (20 blank + 20 low responders).</summary>
    public const int MinimumBlankSamples = 20;
    public const int MinimumLowSamples = 20;

    private static double Calculate2SdOrZ(IReadOnlyList<double> blankResponses, double z)
    {
        var unit = new StatUnit();
        foreach (double v in blankResponses) unit.Add(v);
        return unit.Mean + z * unit.StandardDeviation;
    }
}
