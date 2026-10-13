using EPAnalyzer.Statistics.Distributions;

namespace EPAnalyzer.Statistics.Descriptive;

/// <summary>
/// Weighted single-variable accumulator (port of the <c>dgrs1.TStatUnit</c> engine).
/// All descriptive statistics used by Simple Precision, LOB, Carryover, Trueness,
/// Linearity etc. are produced by this class.
/// </summary>
public class StatUnit
{
    private double _sumW;
    private double _sumWX;
    private double _sumWX2;
    private int _n;
    private int _excluded;
    private bool _weighted;
    private double _min = double.PositiveInfinity;
    private double _max = double.NegativeInfinity;

    public int N => _n;
    public int Excluded => _excluded;
    public int NTotal => _n + _excluded;
    public double SumW => _sumW;
    public double SumWX => _sumWX;
    public double SumWX2 => _sumWX2;
    public bool IsWeighted => _weighted;
    public double Min => _n > 0 ? _min : double.NaN;
    public double Max => _n > 0 ? _max : double.NaN;

    public virtual void Add(double x) => AddW(x, 1.0);

    public virtual void AddW(double x, double w)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || w <= 0.0) return;
        if (Math.Abs(w - 1.0) > 1e-5) _weighted = true;
        _sumW += w;
        _sumWX += w * x;
        _sumWX2 += w * x * x;
        _n++;
        if (x < _min) _min = x;
        if (x > _max) _max = x;
    }

    public void Exclude() => _excluded++;

    /// <summary>Weighted mean, Sigma(wx)/Sigma(w); 0 when no weight.</summary>
    public double Mean => _sumW > 0.0 ? _sumWX / _sumW : 0.0;

    /// <summary>Weighted variance, (Sigma(wx^2) - (Sigma(wx))^2/Sigma(w)) / (n - 1).</summary>
    public double Variance
    {
        get
        {
            if (_n < 2 || _sumW <= 0.0) return 0.0;
            double num = _sumWX2 - _sumWX * _sumWX / _sumW;
            return num > 0.0 ? num / (_n - 1) : 0.0;
        }
    }

    public double StandardDeviation => Math.Sqrt(Variance);

    /// <summary>Standard error of the mean, SD/sqrt(n).</summary>
    public double Sem => _n > 0 ? StandardDeviation / Math.Sqrt(_n) : 0.0;

    /// <summary>One-sided half-width of the 95% confidence interval of the mean.</summary>
    public double D95 => _n >= 2 ? StatDistributions.TLevel(0.95, _n - 1) * Sem : 0.0;

    /// <summary>Coefficient of variation in percent, 100*SD/|mean|.</summary>
    public double Cv => StandardDeviation > 0.0 && Mean != 0.0 ? 100.0 * StandardDeviation / Math.Abs(Mean) : 0.0;

    /// <summary>Standard deviation index (x - mean)/SD with EP's edge-case sentinels.</summary>
    public double Sdi(double x)
    {
        double sd = StandardDeviation;
        double mean = Mean;
        if (sd <= 0.0 || mean == x) return 0.0;
        if (Math.Abs(x - mean) < 1e-8 * Math.Max(1.0, Math.Abs(mean))) return 0.0;
        if (sd <= 0.0) return x > mean ? double.PositiveInfinity : double.NegativeInfinity;
        return (x - mean) / sd;
    }

    /// <summary>SDI clamped to the given bounds with a 0.1 margin.</summary>
    public double ClampedSdi(double x, double bound)
    {
        double sdi = Sdi(x);
        if (double.IsInfinity(sdi)) return Math.Sign(sdi) * bound;
        return Math.Max(-bound, Math.Min(bound, sdi));
    }

    /// <summary>Chi-square confidence factors for the SD: sqrt((n-1)/ChiLvl).</summary>
    public (double LowerFactor, double UpperFactor) SdConfidenceFactors(double alpha = 0.05)
    {
        if (_n < 2) return (1.0, 1.0);
        double df = _n - 1;
        double chiLo = StatDistributions.ChiLevel(alpha / 2.0, df);
        double chiHi = StatDistributions.ChiLevel(1.0 - alpha / 2.0, df);
        return (Math.Sqrt(df / chiHi), Math.Sqrt(df / chiLo));
    }

    /// <summary>95% (or 100(1-alpha)%) confidence interval for the SD from the chi-square distribution.</summary>
    public (double Lower, double Upper) SdConfidenceInterval(double alpha = 0.05)
    {
        var (lo, hi) = SdConfidenceFactors(alpha);
        double sd = StandardDeviation;
        return (sd * lo, sd * hi);
    }

    /// <summary>Mean confidence interval.</summary>
    public (double Lower, double Upper) MeanConfidenceInterval(double confidence = 0.95)
    {
        if (_n < 2) return (Mean, Mean);
        double t = StatDistributions.TLevel(confidence, _n - 1);
        return (Mean - t * Sem, Mean + t * Sem);
    }

    /// <summary>
    /// One-sided upper confidence limit for the SD (CLSI verification value):
    /// SD * sqrt(chi-squareQuantile(confidence, df)/df).
    /// </summary>
    public double VerificationValue(double confidence = 0.95)
        => _n < 2 ? double.NaN : StandardDeviation * Math.Sqrt(StatDistributions.ChiLevel(confidence, _n - 1) / (_n - 1));

    public void Clear()
    {
        _sumW = _sumWX = _sumWX2 = 0.0;
        _n = _excluded = 0;
        _weighted = false;
        _min = double.PositiveInfinity;
        _max = double.NegativeInfinity;
    }
}
