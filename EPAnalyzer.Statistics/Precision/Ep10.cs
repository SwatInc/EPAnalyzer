namespace EPAnalyzer.Statistics.Precision;

/// <summary>A single EP10 run (up to 10 replicates per level).</summary>
public sealed class Ep10Run
{
    public required IReadOnlyList<double> Values { get; init; }
    public bool Valid => Values.Count > 0;

    public int Count => Values.Count;
    public double Mean => Values.Average();
    public double Variance => Values.Count > 1 ? Values.Sum(v => (v - Mean) * (v - Mean)) / (Values.Count - 1) : 0.0;
    public double Sd => Math.Sqrt(Variance);
    public double Cv => Mean != 0.0 ? 100.0 * Sd / Math.Abs(Mean) : 0.0;
}

/// <summary>EP10 grand summary for one level.</summary>
public sealed class Ep10LevelResult
{
    public required double GrandMean { get; init; }
    public required double GrandVariance { get; init; }
    public required double GrandSd { get; init; }
    public required double GrandCv { get; init; }
    public required double Assigned { get; init; }
    public required double Bias { get; init; }
    public required bool BiasPass { get; init; }
    public required bool CvPass { get; init; }
    public required int ValidRuns { get; init; }
}

/// <summary>
/// EP10 preliminary evaluation (port of <c>ep10cls.TEP10</c>):
/// grand means = mean of run means, grand variance = mean of run variances,
/// grand SD = sqrt(grand variance), grand CV = 100*SD/mean, bias = grand mean
/// minus assigned value, with bias/CV pass-fail rules.
/// </summary>
public static class Ep10
{
    /// <summary>
    /// Computes the grand statistics per level from the runs of that level.
    /// </summary>
    public static Ep10LevelResult Level(
        IReadOnlyList<Ep10Run> runs,
        double assigned,
        double? cvLimit = null,
        double? biasLimit = null,
        int minimumRuns = 5)
    {
        var valid = runs.Where(r => r.Valid).ToList();
        if (valid.Count == 0) throw new ArgumentException("No valid runs.", nameof(runs));

        double grandMean = valid.Average(r => r.Mean);
        double grandVariance = valid.Average(r => r.Variance);
        double grandSd = Math.Sqrt(Math.Max(0.0, grandVariance));
        double grandCv = grandMean != 0.0 ? 100.0 * grandSd / Math.Abs(grandMean) : 0.0;
        double bias = grandMean - assigned;

        bool biasPass = biasLimit is null || Math.Abs(bias) <= biasLimit.Value;
        bool cvPass = cvLimit is null || grandCv <= cvLimit.Value;

        return new Ep10LevelResult
        {
            GrandMean = grandMean,
            GrandVariance = grandVariance,
            GrandSd = grandSd,
            GrandCv = grandCv,
            Assigned = assigned,
            Bias = bias,
            BiasPass = biasPass,
            CvPass = cvPass,
            ValidRuns = valid.Count,
        };
    }

    /// <summary>
    /// Overall EP10 pass/fail: all levels and all per-run checks must pass,
    /// and the evaluation needs more than four runs; otherwise a run-level
    /// failure is reported.
    /// </summary>
    public static (bool Pass, bool RunFailed, bool Preliminary) Overall(
        IReadOnlyList<Ep10LevelResult> levels,
        bool anyRunInvalid,
        int minimumRuns = 5)
    {
        bool preliminary = levels.Count == 0 || levels.Any(l => l.ValidRuns < minimumRuns);
        bool pass = levels.Count > 0 && levels.All(l => l.BiasPass && l.CvPass) && !anyRunInvalid && !preliminary;
        return (pass, anyRunInvalid, preliminary);
    }

    /// <summary>Scale factor between two assigned values: assignedHigh - assignedLow.</summary>
    public static double ScaleFactor(double assignedLow, double assignedHigh) => assignedHigh - assignedLow;

    /// <summary>Normalised scale factor (ratio) between two assigned values.</summary>
    public static double NormalizedScaleFactor(double assignedLow, double assignedHigh) => assignedLow != 0.0 ? assignedHigh / assignedLow : 1.0;
}
