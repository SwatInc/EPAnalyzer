using EPAnalyzer.Statistics.Descriptive;
using EPAnalyzer.Statistics.Distributions;

namespace EPAnalyzer.Statistics.Precision;

/// <summary>Precision verification goal modes (SP module).</summary>
public enum PrecisionGoalMode
{
    None,
    TeaBased,
    VendorSdBased,
    VendorRaeBased,
}

/// <summary>Simple-precision pass/fail/uncertain classification.</summary>
public enum VerificationOutcome
{
    Pass,
    Fail,
    Uncertain,
    Preliminary,
}

/// <summary>Simple precision options.</summary>
public sealed class SimplePrecisionOptions
{
    public PrecisionGoalMode GoalMode { get; set; } = PrecisionGoalMode.None;
    public double AllowableTotalErrorConcentration { get; set; }
    public double AllowableTotalErrorPercent { get; set; }
    public double PercentRandomErrorBudget { get; set; } = 50.0;
    public double VendorSd { get; set; }
    public double VendorRaeConcentration { get; set; }
    public double VendorRaePercent { get; set; }
    public double Confidence { get; set; } = 0.95;
    public int MinimumN { get; set; } = 3;
}

/// <summary>Simple precision result.</summary>
public sealed class SimplePrecisionResult
{
    public required IReadOnlyList<double> Measurements { get; init; }
    public required double Mean { get; init; }
    public required double Sd { get; init; }
    public required double Cv { get; init; }
    public required int DegreesOfFreedom { get; init; }
    public required (double Lower, double Upper) SdConfidenceInterval { get; init; }
    public required double SdGoal { get; init; }
    public required double TargetCv { get; init; }
    public required double AllowableRandomError { get; init; }
    public required VerificationOutcome Outcome { get; init; }
    public required bool IsPreliminary { get; init; }
    public required double? VerificationValue95 { get; init; }
    public required double? VerificationValue99 { get; init; }
    public required IReadOnlyList<double> SdiValues { get; init; }
}

/// <summary>
/// Simple precision (port of the SP module over <c>dgrs1.TStatUnit</c>):
/// mean, SD, CV, chi-square CI of the SD, SDI, goal comparison and the
/// pass/fail/uncertain classification.
/// </summary>
public static class SimplePrecision
{
    public static SimplePrecisionResult Calculate(IReadOnlyList<double> measurements, SimplePrecisionOptions options)
    {
        var unit = new StatUnit();
        foreach (double v in measurements) unit.Add(v);

        double sd = unit.StandardDeviation;
        double mean = unit.Mean;
        double cv = unit.Cv;
        int df = Math.Max(0, unit.N - 1);

        double allowableRandomError = options.GoalMode switch
        {
            PrecisionGoalMode.TeaBased => options.AllowableTotalErrorPercent > 0
                ? options.PercentRandomErrorBudget / 100.0 * options.AllowableTotalErrorPercent
                : options.PercentRandomErrorBudget / 100.0 * options.AllowableTotalErrorConcentration,
            PrecisionGoalMode.VendorSdBased => options.VendorSd,
            PrecisionGoalMode.VendorRaeBased => options.VendorRaeConcentration > 0
                ? options.VendorRaeConcentration
                : options.VendorRaePercent / 100.0 * Math.Abs(mean),
            _ => 0.0,
        };

        double sdGoal = options.GoalMode switch
        {
            PrecisionGoalMode.TeaBased when options.AllowableTotalErrorConcentration > 0
                => options.PercentRandomErrorBudget / 100.0 * options.AllowableTotalErrorConcentration,
            PrecisionGoalMode.TeaBased when options.AllowableTotalErrorPercent > 0
                => options.PercentRandomErrorBudget / 100.0 * options.AllowableTotalErrorPercent / 100.0 * Math.Abs(mean),
            PrecisionGoalMode.VendorSdBased => options.VendorSd,
            PrecisionGoalMode.VendorRaeBased when options.VendorRaeConcentration > 0 => options.VendorRaeConcentration,
            PrecisionGoalMode.VendorRaeBased => options.VendorRaePercent / 100.0 * Math.Abs(mean),
            _ => 0.0,
        };

        double targetCv = mean != 0.0 && sdGoal > 0 ? 100.0 * sdGoal / Math.Abs(mean) : 0.0;
        var ci = unit.SdConfidenceInterval(1.0 - options.Confidence);
        bool preliminary = unit.N < options.MinimumN;

        VerificationOutcome outcome = VerificationOutcome.Preliminary;
        if (!preliminary && options.GoalMode != PrecisionGoalMode.None)
        {
            if (ci.Upper < sdGoal) outcome = VerificationOutcome.Pass;
            else if (ci.Lower > sdGoal) outcome = VerificationOutcome.Fail;
            else outcome = VerificationOutcome.Uncertain;
        }
        else if (!preliminary)
        {
            outcome = VerificationOutcome.Pass;
        }

        return new SimplePrecisionResult
        {
            Measurements = measurements,
            Mean = mean,
            Sd = sd,
            Cv = cv,
            DegreesOfFreedom = df,
            SdConfidenceInterval = ci,
            SdGoal = sdGoal,
            TargetCv = targetCv,
            AllowableRandomError = allowableRandomError,
            Outcome = outcome,
            IsPreliminary = preliminary,
            VerificationValue95 = df > 0 ? unit.VerificationValue(0.95) : null,
            VerificationValue99 = df > 0 ? unit.VerificationValue(0.99) : null,
            SdiValues = measurements.Select(unit.Sdi).ToArray(),
        };
    }

    /// <summary>Standard deviation index (x - mean)/SD.</summary>
    public static double Sdi(double value, double mean, double sd) => sd > 0 ? (value - mean) / sd : double.NaN;

    /// <summary>Upper one-sided confidence limit of an SD (verification value).</summary>
    public static double VerificationValue(double sd, int df, double confidence = 0.95)
        => df > 0 ? sd * Math.Sqrt(StatDistributions.ChiLevel(confidence, df) / df) : double.NaN;
}
