using EPAnalyzer.Statistics.Regression;

namespace EPAnalyzer.Statistics.MethodComparison;

/// <summary>Selectable regression model for method comparison.</summary>
public enum RegressionModel
{
    Ols,
    WeightedOls,
    Deming,
    WeightedDeming,
    PassingBablok,
}

/// <summary>
/// Method-comparison statistics accumulator (port of <c>amcClass.TAMCStats</c>).
/// Wraps the weighted regression accumulator and exposes the bias / dispersion /
/// correlation / t / F / p quantities used by AMC, EP9-A2, EP9-A3, HMC and PT/INR-MC.
/// </summary>
public sealed class MethodComparisonStats
{
    private readonly WeightedRegressionAccumulator _acc = new();
    private readonly List<DataPoint> _points = new();

    public int N => _acc.N;
    public IReadOnlyList<DataPoint> Points => _points;

    public void Add(double x, double y)
    {
        _acc.Add(x, y);
        _points.Add(new DataPoint(x, y));
    }

    public void AddWeighted(double x, double y, double w)
    {
        _acc.AddW(x, y, w);
        _points.Add(new DataPoint(x, y));
    }

    public double MeanX => _acc.MeanX;
    public double MeanY => _acc.MeanY;
    public double VarX => _acc.VarX;
    public double VarY => _acc.VarY;
    public double SdX => _acc.SdX;
    public double SdY => _acc.SdY;
    public double Bias => _acc.Bias;
    public double PercentBias(bool midpoint = false) => _acc.PercentBias(midpoint);
    public double SdOfDifferences => _acc.SdDiff;
    public double SeOfDifferences => _acc.SeDiff;
    public double Df => _acc.Df;
    public double TValue => _acc.TValue;
    public double TProbability => _acc.TProb;
    public double FValue => _acc.FValue;
    public double FProbability => _acc.FProb;
    public double R => _acc.R;
    public double R2 => _acc.R2;
    public double MedianBias => Descriptive.RobustStatistics.Median(_points.Select(p => p.Difference));
    public double MinX => _acc.MinX;
    public double MaxX => _acc.MaxX;
    public double MinY => _acc.MinY;
    public double MaxY => _acc.MaxY;

    public (double Lower, double Upper) BiasConfidenceInterval(double confidence = 0.95)
    {
        if (N < 2) return (Bias, Bias);
        double t = Distributions.StatDistributions.TLevel(confidence, N - 1);
        double half = t * SeOfDifferences;
        return (Bias - half, Bias + half);
    }

    public LinearFit Fit(RegressionModel model, double errorSdX = 1.0, double errorSdY = 1.0, double confidence = 0.95)
        => model switch
        {
            RegressionModel.Ols => RegressionMath.Ols(_points, confidence),
            RegressionModel.WeightedOls => RegressionMath.WeightedOls(_points, p => p.X != 0 ? 1.0 / (p.X * p.X) : 1.0, confidence),
            RegressionModel.Deming => RegressionMath.Deming(_points, errorSdX, errorSdY, confidence),
            RegressionModel.WeightedDeming => RegressionMath.WeightedDeming(_points, (errorSdY / Math.Max(1e-12, errorSdX)) * (errorSdY / Math.Max(1e-12, errorSdX)), confidence),
            RegressionModel.PassingBablok => ToLinearFit(PassingBablok.Calculate(_points, confidence)),
            _ => throw new ArgumentOutOfRangeException(nameof(model)),
        };

    private static LinearFit ToLinearFit(PassingBablokResult pb)
        => new(pb.Slope, pb.Intercept, pb.N, 0, 0, 0, 0, 0, 0,
               pb.SlopeCiLow, pb.SlopeCiHigh, pb.InterceptCiLow, pb.InterceptCiHigh);

    /// <summary>Percent bias against the midpoint ((xbar+ybar)/2) option.</summary>
    public double PercentBiasMidpoint => PercentBias(true);

    /// <summary>X range ratio max/min (1e20 sentinel when empty), as <c>CalcXYRatios</c>.</summary>
    public (double XRatio, double YRatio) RangeRatios
    {
        get
        {
            double xr = N > 0 && _acc.MinX > 0 ? _acc.MaxX / _acc.MinX : 1e20;
            double yr = N > 0 && _acc.MinY > 0 ? _acc.MaxY / _acc.MinY : 1e20;
            return (xr, yr);
        }
    }

    public void Clear()
    {
        _acc.Clear();
        _points.Clear();
    }
}
