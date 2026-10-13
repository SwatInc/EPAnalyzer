using EPAnalyzer.Statistics.Distributions;

namespace EPAnalyzer.Statistics.Descriptive;

/// <summary>
/// Order-statistic / robust / normal-distribution extension (port of the
/// <c>dgrs2.TStatUnitEx</c> engine). Used by reference-interval, percentile,
/// geometric-mean and non-parametric code paths.
/// </summary>
public sealed class StatUnitEx : StatUnit
{
    private readonly List<double> _values = new();

    public IReadOnlyList<double> Values => _values;

    public override void AddW(double x, double w)
    {
        base.AddW(x, w);
        if (!double.IsNaN(x) && !double.IsInfinity(x) && w > 0.0) _values.Add(x);
    }

    private double[] SortedValues()
    {
        var copy = new double[_values.Count];
        _values.CopyTo(copy);
        Array.Sort(copy);
        return copy;
    }

    /// <summary>Interpolated percentile (0..100), the order statistic used by EP.</summary>
    public double Percentile(double p)
    {
        double[] v = SortedValues();
        if (v.Length == 0) return double.NaN;
        if (v.Length == 1) return v[0];
        double rank = p / 100.0 * (v.Length - 1);
        int lo = (int)Math.Floor(rank);
        int hi = (int)Math.Ceiling(rank);
        if (lo == hi) return v[lo];
        return v[lo] + (rank - lo) * (v[hi] - v[lo]);
    }

    public double Median => Percentile(50.0);
    public double P25 => Percentile(25.0);
    public double P30 => Percentile(30.0);
    public double P70 => Percentile(70.0);
    public double P75 => Percentile(75.0);
    public double Iqr => P75 - P25;

    /// <summary>Sample skewness g1 = m3/m2^(3/2).</summary>
    public double Skewness
    {
        get
        {
            if (_values.Count < 3) return 0.0;
            double mean = Mean;
            double m2 = _values.Sum(v => Math.Pow(v - mean, 2)) / _values.Count;
            double m3 = _values.Sum(v => Math.Pow(v - mean, 3)) / _values.Count;
            return m2 <= 0 ? 0.0 : m3 / Math.Pow(m2, 1.5);
        }
    }

    /// <summary>Sample (raw) kurtosis m4/m2^2.</summary>
    public double Kurtosis
    {
        get
        {
            if (_values.Count < 4) return 0.0;
            double mean = Mean;
            double m2 = _values.Sum(v => Math.Pow(v - mean, 2)) / _values.Count;
            double m4 = _values.Sum(v => Math.Pow(v - mean, 4)) / _values.Count;
            return m2 <= 0 ? 0.0 : m4 / (m2 * m2);
        }
    }

    /// <summary>Fisher corrected skewness G1.</summary>
    public double FisherG1
    {
        get
        {
            int n = _values.Count;
            return n < 3 ? 0.0 : Math.Sqrt((double)n * (n - 1)) / (n - 2) * Skewness;
        }
    }

    /// <summary>Fisher corrected kurtosis G2.</summary>
    public double FisherG2
    {
        get
        {
            int n = _values.Count;
            return n < 4 ? 0.0 : (n - 1.0) / ((n - 2) * (n - 3)) * ((n + 1) * Kurtosis - 3 * (n - 1));
        }
    }

    /// <summary>Normal-consistent median absolute deviation, MAD/0.6745.</summary>
    public double Smad
    {
        get
        {
            if (_values.Count == 0) return 0.0;
            double median = Median;
            double[] dev = _values.Select(v => Math.Abs(v - median)).ToArray();
            Array.Sort(dev);
            double mad = dev.Length % 2 == 1 ? dev[dev.Length / 2]
                : (dev[dev.Length / 2 - 1] + dev[dev.Length / 2]) / 2.0;
            return mad / 0.6745;
        }
    }

    /// <summary>Raw median absolute deviation (no 0.6745 scaling).</summary>
    public double Mad
    {
        get
        {
            if (_values.Count == 0) return 0.0;
            double median = Median;
            double[] dev = _values.Select(v => Math.Abs(v - median)).ToArray();
            Array.Sort(dev);
            return dev.Length % 2 == 1 ? dev[dev.Length / 2]
                : (dev[dev.Length / 2 - 1] + dev[dev.Length / 2]) / 2.0;
        }
    }

    /// <summary>Mean absolute deviation from the median, Sigma|x - median|/n.</summary>
    public double MeanAbsoluteDeviation
    {
        get
        {
            if (_values.Count == 0) return 0.0;
            double median = Median;
            return _values.Sum(v => Math.Abs(v - median)) / _values.Count;
        }
    }

    /// <summary>Geometric mean = exp(mean(ln x)); non-positive values are ignored.</summary>
    public double GeometricMean
    {
        get
        {
            var logs = _values.Where(v => v > 0).Select(v => Math.Log(v)).ToArray();
            return logs.Length == 0 ? double.NaN : Math.Exp(logs.Average());
        }
    }

    /// <summary>Geometric SD = exp(SD(ln x)).</summary>
    public double GeometricSd
    {
        get
        {
            var logs = _values.Where(v => v > 0).Select(v => Math.Log(v)).ToArray();
            if (logs.Length < 2) return double.NaN;
            double mean = logs.Average();
            return Math.Exp(Math.Sqrt(logs.Sum(l => (l - mean) * (l - mean)) / (logs.Length - 1)));
        }
    }

    /// <summary>Geometric CV in percent.</summary>
    public double GeometricCv
    {
        get
        {
            var logs = _values.Where(v => v > 0).Select(v => Math.Log(v)).ToArray();
            if (logs.Length < 2) return double.NaN;
            double mean = logs.Average();
            double sd = Math.Sqrt(logs.Sum(l => (l - mean) * (l - mean)) / (logs.Length - 1));
            return 100.0 * Math.Sqrt(Math.Exp(sd * sd) - 1.0);
        }
    }

    /// <summary>Parametric 95% reference limits (mean +/- 1.96 SD).</summary>
    public (double Lower, double Upper) ParametricLimits(double z = StatDistributions.Z975)
        => (Mean - z * StandardDeviation, Mean + z * StandardDeviation);

    /// <summary>Non-parametric reference limits from the 2.5th and 97.5th percentiles.</summary>
    public (double Lower, double Upper) NonParametricLimits(double pLow = 2.5, double pHigh = 97.5)
        => (Percentile(pLow), Percentile(pHigh));

    /// <summary>
    /// Exact order-statistic rank indices for the 100(1-alpha)% confidence interval of the
    /// p-th percentile (Clopper-Pearson/binomial construction, CLSI C28 "exact formula" option).
    /// Returns 1-based ranks; 0 means "use the extreme rank".
    /// </summary>
    public static (int LowerRank, int UpperRank) PercentileConfidenceRanks(int n, double p, double alpha = 0.10)
    {
        if (n <= 0) return (0, 0);

        // Lower rank: largest r with P(X <= r-1) <= alpha/2 (X ~ Bin(n,p)); 1 when P(X=0) already exceeds it.
        int lower = 1;
        for (int k = 0; k < n; k++)
        {
            if (StatDistributions.BinomialCdf(k, n, p) <= alpha / 2.0) lower = k + 1;
            else break;
        }

        // Upper rank: smallest s with P(X >= s) <= alpha/2.
        int upper = n;
        for (int s = 1; s <= n; s++)
        {
            if (1.0 - StatDistributions.BinomialCdf(s - 1, n, p) <= alpha / 2.0) { upper = s; break; }
        }

        return (lower, upper);
    }

    /// <summary>Normal-approximation ranks used by the CLSI C28 table option.</summary>
    public static (int LowerRank, int UpperRank) PercentileConfidenceRanksNormal(int n, double p, double alpha = 0.10)
    {
        if (n <= 0) return (0, 0);
        double z = StatDistributions.NormalQuantile(1.0 - alpha / 2.0);
        double root = z * Math.Sqrt(n * p * (1.0 - p));
        int lower = (int)Math.Floor(n * p - root);
        int upper = (int)Math.Ceiling(n * p + root);
        return (Math.Clamp(lower, 1, n), Math.Clamp(upper, 1, n));
    }

    /// <summary>Percent of values outside [low, high].</summary>
    public double CalcPctOut(double low, double high)
    {
        if (_values.Count == 0) return 0.0;
        int outside = _values.Count(v => v < low || v > high);
        return 100.0 * outside / _values.Count;
    }

    /// <summary>Anderson-Darling normality test over the collected values.</summary>
    public (double A2, double PValue) AndersonDarling => StatDistributions.AndersonDarlingNormal(_values);

    public bool IsNormal(double alpha = 0.05)
    {
        var (_, p) = AndersonDarling;
        return double.IsNaN(p) || p > alpha;
    }

    public void ClearValues() => _values.Clear();
}
