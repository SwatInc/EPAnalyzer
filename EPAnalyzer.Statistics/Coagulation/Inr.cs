using EPAnalyzer.Statistics.Descriptive;

namespace EPAnalyzer.Statistics.Coagulation;

/// <summary>
/// Coagulation / INR modules (INR-GEO, INR-CK, PT/INR method comparison):
/// the power-transform INR calculation, geometric mean of PT, permissible
/// limits and the manual INR check.
/// </summary>
public static class Inr
{
    /// <summary>INR = (PT / MNPT)^ISI.</summary>
    public static double Calculate(double pt, double mnpt, double isi)
    {
        if (pt <= 0 || mnpt <= 0 || isi <= 0) return double.NaN;
        return Math.Pow(pt / mnpt, isi);
    }

    /// <summary>Geometric mean (MNPT) = exp(mean(ln PT)).</summary>
    public static double GeometricMean(IEnumerable<double> ptValues)
    {
        double[] valid = ptValues.Where(v => v > 0).ToArray();
        return valid.Length == 0 ? double.NaN : Math.Exp(valid.Select(v => Math.Log(v)).Average());
    }

    /// <summary>Geometric standard deviation = exp(SD(ln PT)).</summary>
    public static double GeometricSd(IEnumerable<double> ptValues)
    {
        double[] logs = ptValues.Where(v => v > 0).Select(v => Math.Log(v)).ToArray();
        if (logs.Length < 2) return double.NaN;
        double mean = logs.Average();
        return Math.Exp(Math.Sqrt(logs.Sum(l => (l - mean) * (l - mean)) / (logs.Length - 1)));
    }

    /// <summary>PT limits on the log scale (geometric mean / x) SD: MNPT/SD .. MNPT*SD.</summary>
    public static (double Lower, double Upper) PtLimits(double mnpt, double geometricSd, double multiplier = 2.0)
        => (mnpt / Math.Pow(geometricSd, multiplier), mnpt * Math.Pow(geometricSd, multiplier));

    /// <summary>Percent of values outside the limits.</summary>
    public static double PercentOutside(IEnumerable<double> values, double lower, double upper)
    {
        double[] v = values.ToArray();
        if (v.Length == 0) return double.NaN;
        return 100.0 * v.Count(x => x < lower || x > upper) / v.Length;
    }

    /// <summary>INR-GEO passes when fewer than 10% of results are outside the limits.</summary>
    public static bool Passes(double percentOutside, double limit = 10.0) => percentOutside < limit;

    /// <summary>Manual INR check: |computed - entered| * 10^decimals &lt; 1.5.</summary>
    public static bool CheckPasses(double computedInr, double enteredInr, int decimals)
        => Math.Abs(computedInr - enteredInr) * Math.Pow(10, decimals) < 1.5;

    /// <summary>Minimum number of items for a preliminary/final INR-CK report.</summary>
    public const int MinimumCheckItems = 3;

    /// <summary>INR-CK overall result: pass (1) / fail (2) / preliminary (0).</summary>
    public static int PassFail(IReadOnlyList<bool> itemPasses, int minimumItems = MinimumCheckItems)
    {
        if (itemPasses.Count < minimumItems) return 0;
        return itemPasses.All(p => p) ? 1 : 2;
    }

    /// <summary>PT/INR calibration verification line fit helper: y = slope*x + intercept.</summary>
    public static double LineFit(double slope, double intercept, double x) => slope * x + intercept;

    /// <summary>Through-origin predicted value used by the band: y = slope*x.</summary>
    public static double PredictedValue(double slope, double x) => slope * x;

    /// <summary>Allowable-error delta AEDelta(x) = k*x*storedFactor.</summary>
    public static double AllowableErrorDelta(double x, double factor, double k = 1.0) => k * x * factor;

    /// <summary>INR-CR error index pass rule: |errorIndex| &lt;= 1.</summary>
    public static bool ErrorIndexPasses(double errorIndex) => Math.Abs(errorIndex) <= 1.0;

    /// <summary>Relative error index (PT/INR calibration verification).</summary>
    public static double ErrorIndex(double observed, double predicted, double allowableDelta)
        => allowableDelta != 0 ? (observed - predicted) / allowableDelta : double.NaN;
}
