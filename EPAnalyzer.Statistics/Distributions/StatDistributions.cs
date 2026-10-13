using MathNet.Numerics;
using MathNet.Numerics.Distributions;

namespace EPAnalyzer.Statistics.Distributions;

/// <summary>
/// Distribution helpers shared by every statistical engine.
/// These wrap MathNet.Numerics with the exact idioms used by EP Evaluator
/// (t / chi-square / F / normal / binomial probabilities and the exact or
/// score confidence intervals for proportions).
/// </summary>
public static class StatDistributions
{
    public const double Z95 = 1.959963984540054;
    public const double Z99 = 2.5758293035489004;
    public const double Z90 = 1.6448536269514722;
    public const double Z975 = 1.959963984540054;
    public const double Z995 = 2.5758293035489004;

    // --- Student t -------------------------------------------------------

    /// <summary>Two-sided t quantile (i.e. inverse survival at p/2) for the given degrees of freedom.</summary>
    public static double TLevel(double confidence, double df) => StudentT.InvCDF(0.0, 1.0, df, 0.5 + confidence / 2.0);

    /// <summary>Student t cumulative distribution function.</summary>
    public static double TCdf(double t, double df) => StudentT.CDF(0.0, 1.0, df, t);

    /// <summary>Two-sided p-value P(|T_df| >= |t|).</summary>
    public static double TTwoSidedP(double t, double df) => 2.0 * (1.0 - StudentT.CDF(0.0, 1.0, df, Math.Abs(t)));

    /// <summary>One-sided upper-tail probability P(T_df >= t).</summary>
    public static double TUpperP(double t, double df) => 1.0 - StudentT.CDF(0.0, 1.0, df, t);

    // --- Chi-square ------------------------------------------------------

    /// <summary>Chi-square quantile (inverse CDF).</summary>
    public static double ChiLevel(double p, double df) => ChiSquared.InvCDF(df, p);

    /// <summary>Upper-tail chi-square probability P(X &gt; chi2).</summary>
    public static double ChiProb(double chi2, double df) => 1.0 - ChiSquared.CDF(df, chi2);

    // --- F ---------------------------------------------------------------

    /// <summary>F quantile (inverse CDF).</summary>
    public static double FLevel(double p, double df1, double df2) => FisherSnedecor.InvCDF(df1, df2, p);

    /// <summary>Upper-tail F probability P(F &gt; f).</summary>
    public static double FProb(double f, double df1, double df2)
    {
        if (double.IsNaN(f) || f <= 0.0) return 1.0;
        if (double.IsPositiveInfinity(f)) return 0.0;
        return 1.0 - FisherSnedecor.CDF(df1, df2, f);
    }

    // --- Normal ----------------------------------------------------------

    public static double NormalCdf(double x) => Normal.CDF(0.0, 1.0, x);

    public static double NormalQuantile(double p) => Normal.InvCDF(0.0, 1.0, p);

    // --- Binomial --------------------------------------------------------

    /// <summary>P(X &lt;= k) for X ~ Binomial(n, p). Uses I_{1-p}(n-k, k+1).</summary>
    public static double BinomialCdf(int k, int n, double p)
    {
        if (k < 0) return 0.0;
        if (k >= n) return 1.0;
        if (p <= 0.0) return 1.0;
        if (p >= 1.0) return k >= n ? 1.0 : 0.0;
        return SpecialFunctions.BetaRegularized(n - k, k + 1, 1.0 - p);
    }

    /// <summary>P(X = k) for X ~ Binomial(n, p).</summary>
    public static double BinomialPmf(int k, int n, double p)
    {
        if (k < 0 || k > n) return 0.0;
        return Math.Exp(LogBinomial(n, k) + k * Math.Log(p) + (n - k) * Math.Log(1.0 - p));
    }

    private static double LogBinomial(int n, int k)
    {
        double r = 0.0;
        for (int i = 1; i <= k; i++) r += Math.Log(n - k + i) - Math.Log(i);
        return r;
    }

    // --- Proportion intervals -------------------------------------------

    /// <summary>
    /// Exact (Clopper-Pearson) 100(1-alpha)% confidence interval for a binomial proportion x/n.
    /// </summary>
    public static (double Lower, double Upper) ExactBinomialCi(int x, int n, double alpha = 0.05)
    {
        if (n <= 0) return (0.0, 1.0);
        x = Math.Clamp(x, 0, n);
        double lower = x == 0 ? 0.0 : Beta.InvCDF(x, n - x + 1, alpha / 2.0);
        double upper = x == n ? 1.0 : Beta.InvCDF(x + 1, n - x, 1.0 - alpha / 2.0);
        return (lower, upper);
    }

    /// <summary>Wilson score interval for a binomial proportion.</summary>
    public static (double Lower, double Upper) WilsonScoreCi(int x, int n, double alpha = 0.05)
    {
        if (n <= 0) return (0.0, 1.0);
        double z = NormalQuantile(1.0 - alpha / 2.0);
        double p = (double)x / n;
        double denom = 1.0 + z * z / n;
        double center = (p + z * z / (2.0 * n)) / denom;
        double half = z * Math.Sqrt(p * (1.0 - p) / n + z * z / (4.0 * n * n)) / denom;
        return (Math.Max(0.0, center - half), Math.Min(1.0, center + half));
    }

    /// <summary>Agresti-Coull (adjusted Wald) interval; EP uses the (n+1) denominator variant on ROC efficiency.</summary>
    public static (double Lower, double Upper) AgrestiCoullCi(int x, int n, double alpha = 0.05)
    {
        if (n <= 0) return (0.0, 1.0);
        double z = NormalQuantile(1.0 - alpha / 2.0);
        double nTilde = n + z * z;
        double pTilde = (x + z * z / 2.0) / nTilde;
        double half = z * Math.Sqrt(pTilde * (1.0 - pTilde) / nTilde);
        return (Math.Max(0.0, pTilde - half), Math.Min(1.0, pTilde + half));
    }

    /// <summary>
    /// Anderson-Darling normality test (case 4: mean and SD estimated from the sample).
    /// Returns the A^2 statistic and the D'Agostino-Stephens approximate p-value.
    /// </summary>
    public static (double A2, double PValue) AndersonDarlingNormal(IReadOnlyList<double> values)
    {
        int n = values.Count;
        if (n < 8) return (double.NaN, double.NaN);

        double mean = values.Average();
        double sd = Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / (n - 1));
        if (sd <= 0) return (double.NaN, double.NaN);

        double[] z = values.Select(v => NormalCdf((v - mean) / sd)).OrderBy(z => z).ToArray();
        double a2 = -n;
        for (int i = 0; i < n; i++)
        {
            double lo = Math.Clamp(z[i], 1e-12, 1 - 1e-12);
            double hi = Math.Clamp(z[n - 1 - i], 1e-12, 1 - 1e-12);
            a2 -= (2.0 * (i + 1) - 1.0) / n * (Math.Log(lo) + Math.Log(1.0 - hi));
        }

        return (a2, AndersonDarlingPValue(a2, n));
    }

    /// <summary>D'Agostino-Stephens p-value for the Anderson-Darling A^2 statistic (SD case).</summary>
    public static double AndersonDarlingPValue(double a2, int n)
    {
        double a2Star = a2 * (1.0 + 0.75 / n + 2.25 / (n * n));
        if (a2Star < 0.2) return 1.0 - Math.Exp(-13.436 + 101.14 * a2Star - 223.73 * a2Star * a2Star);
        if (a2Star < 0.34) return 1.0 - Math.Exp(-8.318 + 42.796 * a2Star - 59.938 * a2Star * a2Star);
        if (a2Star < 0.6) return Math.Exp(0.9177 - 4.279 * a2Star - 1.38 * a2Star * a2Star);
        return Math.Exp(1.2937 - 5.709 * a2Star + 0.0186 * a2Star * a2Star);
    }

    /// <summary>Critical value of the studentized range-like "3 sigma" EP26 comparison limit used by Compare2Mean.</summary>
    public static double Ep26MeanDifferenceLimit(int n) => 3.0 * Math.Sqrt(n / 240.0);
}
