using EPAnalyzer.Statistics.Descriptive;
using EPAnalyzer.Statistics.Distributions;
using EPAnalyzer.Statistics.Regression;

namespace EPAnalyzer.Statistics.Linearity;

/// <summary>One LOQ material (concentration level) with its replicates.</summary>
public sealed class LoqMaterial
{
    public required double Concentration { get; init; }
    public required IReadOnlyList<double> Replicates { get; init; }
    public double Mean => Replicates.Average();
    public double Sd => Replicates.Count > 1 ? Math.Sqrt(Replicates.Sum(v => (v - Mean) * (v - Mean)) / (Replicates.Count - 1)) : 0.0;
    public double Cv => Mean != 0.0 ? 100.0 * Sd / Math.Abs(Mean) : double.NaN;
    public int N => Replicates.Count;
}

/// <summary>LOQ result: the lowest concentration whose fitted CV (or its upper CI) is below the target.</summary>
public sealed record LoqResult(double Concentration, double FittedMean, double FittedCv, double CvCiLow, double CvCiHigh, bool Found);

/// <summary>
/// Limit of quantification (port of <c>loqClass</c>):
/// per-material mean/SD/CV with the chi-square CV confidence interval,
/// a fitted CV(x) model and the crossing with the target CV (the upper 95%
/// confidence limit of the fitted CV &lt; target).
/// </summary>
public static class LimitOfQuantification
{
    /// <summary>CV confidence interval from the chi-square distribution:
    /// CV*sqrt((n-1)/chi2_{1-alpha/2}) .. CV*sqrt((n-1)/chi2_{alpha/2}).</summary>
    public static (double Lower, double Upper) CvConfidenceInterval(double cv, int n, double alpha = 0.05)
    {
        if (n < 2) return (double.NaN, double.NaN);
        double df = n - 1;
        double lo = cv * Math.Sqrt(df / StatDistributions.ChiLevel(1.0 - alpha / 2.0, df));
        double hi = cv * Math.Sqrt(df / StatDistributions.ChiLevel(alpha / 2.0, df));
        return (lo, hi);
    }

    /// <summary>Material statistics with CV CI.</summary>
    public static (double Mean, double Sd, double Cv, double CvLow, double CvHigh) Material(LoqMaterial material, double alpha = 0.05)
    {
        var (lo, hi) = CvConfidenceInterval(material.Cv, material.N, alpha);
        return (material.Mean, material.Sd, material.Cv, lo, hi);
    }

    /// <summary>
    /// Finds the LOQ as the lowest concentration where the fitted CV curve (or
    /// its upper confidence limit when <paramref name="useUpperCi"/> is set)
    /// crosses below the target. Materials must be sorted by concentration.
    /// </summary>
    public static LoqResult Find(
        IReadOnlyList<LoqMaterial> materials,
        double targetCv,
        bool useUpperCi = true,
        double confidence = 0.95,
        int degree = 1)
    {
        var valid = materials.Where(m => m.N >= 2 && m.Mean > 0).OrderBy(m => m.Concentration).ToList();
        if (valid.Count < 2) return new LoqResult(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, false);

        var points = valid.Select(m => new DataPoint(m.Mean, m.Cv)).ToArray();
        var fit = PolynomialRegression.Fit(points, Math.Min(degree, valid.Count - 2));
        double t = fit.Fit.TFactor(confidence);

        double Curve(double x)
        {
            double cv = fit.Fit.Predict(x);
            return useUpperCi ? cv + t * fit.Fit.ConfidenceHalfWidth(x, confidence) : cv;
        }

        // The LOQ is the lowest concentration at which the curve is below the
        // target. Walk the materials from the top down (loqClass LocalInterpolate).
        double loX = valid[0].Mean;
        double hiX = valid[^1].Mean;
        if (Curve(loX) < targetCv) return new LoqResult(loX, fit.Fit.Predict(loX), Curve(loX), double.NaN, double.NaN, true);
        if (Curve(hiX) >= targetCv) return new LoqResult(double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, false);

        // Bisection on the crossing (guaranteed to bracket here).
        double low = loX, high = hiX;
        for (int i = 0; i < 200; i++)
        {
            double mid = (low + high) / 2.0;
            if (Curve(mid) < targetCv) high = mid; else low = mid;
            if (high - low < 1e-9 * Math.Max(1.0, high)) break;
        }

        double c = high;
        double boundary = Curve(c);
        var (ciLo, ciHi) = MaterialAt(valid, c);
        return new LoqResult(c, fit.Fit.Predict(c), boundary, ciLo, ciHi, true);
    }

    private static (double Low, double High) MaterialAt(IReadOnlyList<LoqMaterial> materials, double x)
    {
        var nearest = materials.OrderBy(m => Math.Abs(m.Mean - x)).First();
        return CvConfidenceInterval(nearest.Cv, nearest.N);
    }

    /// <summary>Evaluates the fitted CV at a concentration with a prediction interval.</summary>
    public static (double Predicted, double Lower, double Upper) Forecast(
        IReadOnlyList<LoqMaterial> materials,
        double concentration,
        double confidence = 0.95,
        int degree = 1)
    {
        var valid = materials.Where(m => m.N >= 2 && m.Mean > 0).OrderBy(m => m.Concentration).ToList();
        if (valid.Count < 2) return (double.NaN, double.NaN, double.NaN);

        var points = valid.Select(m => new DataPoint(m.Mean, m.Cv)).ToArray();
        var fit = PolynomialRegression.Fit(points, Math.Min(degree, valid.Count - 2));
        var (lo, hi) = fit.Fit.PredictionInterval(concentration, confidence);
        return (fit.Fit.Predict(concentration), lo, hi);
    }
}
