using EPAnalyzer.Statistics.Descriptive;

namespace EPAnalyzer.Statistics.Precision;

/// <summary>EP5 variance component result.</summary>
public sealed class Ep5PrecisionResult
{
    public required double GrandMean { get; init; }
    public required double SdWithinRun { get; init; }
    public required double SdBetweenRun { get; init; }
    public required double SdBetweenDay { get; init; }
    public required double SdTotal { get; init; }
    public required double CvWithinRun { get; init; }
    public required double CvBetweenRun { get; init; }
    public required double CvBetweenDay { get; init; }
    public required double CvTotal { get; init; }
    public required double VarWithinRun { get; init; }
    public required double VarBetweenRun { get; init; }
    public required double VarBetweenDay { get; init; }
    public required double VarTotal { get; init; }
    public required int Days { get; init; }
    public required int Runs { get; init; }
    public required int Replicates { get; init; }
    public required double DfWithinRun { get; init; }
    public required double DfBetweenRun { get; init; }
    public required double DfBetweenDay { get; init; }
    public required double DfTotal { get; init; }
    public required bool IsPreliminary { get; init; }
}

/// <summary>One nested-ANOVA run: replicate values of a single run.</summary>
public sealed class Ep5Run
{
    public required int Day { get; init; }
    public required IReadOnlyList<double> Replicates { get; init; }
    public bool Excluded { get; set; }
    public double Mean => Replicates.Average();
    public double Range => Replicates.Count > 1 ? Replicates.Max() - Replicates.Min() : 0.0;
    public double Variance => Replicates.Count > 1 ? Replicates.Sum(v => (v - Mean) * (v - Mean)) / (Replicates.Count - 1) : 0.0;
    public double Sd => Math.Sqrt(Variance);
}

/// <summary>
/// Complex precision / CLSI EP5-A2 nested ANOVA (port of <c>varclass.TVarianceComp</c>):
/// within-run, between-run, between-day and total variance components with
/// SD_TOT^2 = SD_WR^2 + SD_BR^2 + SD_BD^2, the preliminary run-SD outlier rule,
/// and the one-sided verification values.
/// </summary>
public static class ComplexPrecision
{
    /// <summary>
    /// Computes the nested variance components for an unbalanced design:
    /// day i { run j { replicate k } }.
    /// </summary>
    public static Ep5PrecisionResult Calculate(IReadOnlyList<Ep5Run> allRuns, double outlierMultiplier = 5.5)
    {
        var runs = allRuns.Where(r => !r.Excluded).ToList();
        if (runs.Count == 0) throw new ArgumentException("No runs supplied.", nameof(allRuns));

        int days = runs.Select(r => r.Day).Distinct().Count();
        int replicates = (int)Math.Round(runs.Average(r => r.Replicates.Count));
        bool preliminary = days < 3 || runs.Count < 6;

        double grandMean = runs.SelectMany(r => r.Replicates).Average();
        double totalN = runs.Sum(r => r.Replicates.Count);

        double ssWithin = 0.0;
        double ssBetweenRuns = 0.0;
        double ssBetweenDays = 0.0;

        foreach (var group in runs.GroupBy(r => r.Day))
        {
            double dayMean = group.SelectMany(r => r.Replicates).Average();
            double dayN = group.Sum(r => r.Replicates.Count);
            ssBetweenDays += dayN * (dayMean - grandMean) * (dayMean - grandMean);
            foreach (var run in group)
            {
                double runMean = run.Mean;
                ssWithin += run.Replicates.Sum(v => (v - runMean) * (v - runMean));
                ssBetweenRuns += run.Replicates.Count * (runMean - dayMean) * (runMean - dayMean);
            }
        }

        int dfWithin = (int)totalN - runs.Count;
        int dfRuns = runs.Count - days;
        int dfDays = days - 1;

        double msWithin = dfWithin > 0 ? ssWithin / dfWithin : 0.0;
        double msRuns = dfRuns > 0 ? ssBetweenRuns / dfRuns : msWithin;
        double msDays = dfDays > 0 ? ssBetweenDays / dfDays : msRuns;

        // Average replicate count per run and average run count per day.
        double k = runs.Average(r => r.Replicates.Count);
        double r = days > 0 ? (double)runs.Count / days : 1.0;

        double varWithin = msWithin;
        double varBetweenRun = Math.Max(0.0, (msRuns - msWithin) / Math.Max(1e-12, k));
        double varBetweenDay = Math.Max(0.0, (msDays - msRuns) / Math.Max(1e-12, k * r));
        double varTotal = varWithin + varBetweenRun + varBetweenDay;

        double sd(double variance) => Math.Sqrt(Math.Max(0.0, variance));
        double cv(double sdv) => grandMean != 0.0 ? 100.0 * sdv / Math.Abs(grandMean) : double.NaN;

        double wr = sd(varWithin), br = sd(varBetweenRun), bd = sd(varBetweenDay), tot = sd(varTotal);

        return new Ep5PrecisionResult
        {
            GrandMean = grandMean,
            SdWithinRun = wr,
            SdBetweenRun = br,
            SdBetweenDay = bd,
            SdTotal = tot,
            CvWithinRun = cv(wr),
            CvBetweenRun = cv(br),
            CvBetweenDay = cv(bd),
            CvTotal = cv(tot),
            VarWithinRun = varWithin,
            VarBetweenRun = varBetweenRun,
            VarBetweenDay = varBetweenDay,
            VarTotal = varTotal,
            Days = days,
            Runs = runs.Count,
            Replicates = replicates,
            DfWithinRun = dfWithin,
            DfBetweenRun = dfRuns,
            DfBetweenDay = dfDays,
            DfTotal = dfWithin + dfRuns + dfDays,
            IsPreliminary = preliminary,
        };
    }

    /// <summary>
    /// Outlier check: the maximum acceptable difference between replicates is
    /// the preliminary SD times the multiplier (5.5 in EP5-A2). A day whose run
    /// range exceeds the limit is excluded entirely.
    /// </summary>
    public static (double PreliminarySd, double MaxDifference, IReadOnlyList<Ep5Run> ExcludedRuns) DetectOutliers(
        IReadOnlyList<Ep5Run> runs,
        double multiplier = 5.5)
    {
        var duplicateDifferences = new List<double>();
        foreach (var run in runs)
        {
            if (run.Replicates.Count < 2) continue;
            for (int i = 0; i < run.Replicates.Count; i++)
                for (int j = i + 1; j < run.Replicates.Count; j++)
                    duplicateDifferences.Add(Math.Abs(run.Replicates[i] - run.Replicates[j]));
        }

        double preliminarySd = 0.0;
        if (duplicateDifferences.Count > 0)
        {
            double meanSq = duplicateDifferences.Sum(d => d * d) / duplicateDifferences.Count;
            preliminarySd = Math.Sqrt(meanSq / 2.0);
        }

        double limit = multiplier * preliminarySd;
        var excluded = new List<Ep5Run>();
        foreach (var run in runs)
        {
            if (run.Range > limit && limit > 0) excluded.Add(run);
        }

        return (preliminarySd, limit, excluded);
    }

    /// <summary>One-sided upper confidence limit for an SD (CLSI verification value).</summary>
    public static double VerificationValue(double sd, double df, double confidence = 0.95)
        => df > 0 ? sd * Math.Sqrt(Distributions.StatDistributions.ChiLevel(confidence, df) / df) : double.NaN;

    /// <summary>Satterthwaite effective degrees of freedom for the total variance.</summary>
    public static double SatterthwaiteDf(Ep5PrecisionResult result)
    {
        double numerator = Math.Pow(result.VarTotal, 2);
        double denominator = 0.0;
        if (result.DfWithinRun > 0) denominator += Math.Pow(result.VarWithinRun, 2) / result.DfWithinRun;
        if (result.DfBetweenRun > 0) denominator += Math.Pow(result.VarBetweenRun, 2) / result.DfBetweenRun;
        if (result.DfBetweenDay > 0) denominator += Math.Pow(result.VarBetweenDay, 2) / result.DfBetweenDay;
        return denominator > 0 ? numerator / denominator : double.NaN;
    }
}
