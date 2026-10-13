using EPAnalyzer.Statistics.Distributions;

namespace EPAnalyzer.Statistics.Regression;

/// <summary>Simple linear fit result with optional uncertainty terms.</summary>
public readonly record struct LinearFit(
    double Slope,
    double Intercept,
    int N,
    double Sxx,
    double Syy,
    double Sxy,
    double See,
    double SlopeSe,
    double InterceptSe,
    double SlopeCiLow,
    double SlopeCiHigh,
    double InterceptCiLow,
    double InterceptCiHigh)
{
    public double Predict(double x) => Intercept + Slope * x;

    /// <summary>Prediction interval half-width t*SEE*sqrt(1 + 1/n + (x-xbar)^2/Sxx).</summary>
    public double PredictionIntervalHalfWidth(double x, double xMean, double confidence = 0.95)
    {
        double t = StatDistributions.TLevel(confidence, Math.Max(1, N - 2));
        double z = (x - xMean) * (x - xMean) / Sxx;
        return t * See * Math.Sqrt(1.0 + 1.0 / N + z);
    }

    /// <summary>Confidence interval half-width of the fitted line.</summary>
    public double MeanIntervalHalfWidth(double x, double xMean, double confidence = 0.95)
    {
        double t = StatDistributions.TLevel(confidence, Math.Max(1, N - 2));
        double z = (x - xMean) * (x - xMean) / Sxx;
        return t * See * Math.Sqrt(1.0 / N + z);
    }
}

/// <summary>Pairwise point used by method-comparison regressions.</summary>
public readonly record struct DataPoint(double X, double Y)
{
    public double Difference => Y - X;
}

/// <summary>
/// Weighted linear-regression accumulator and closed-form estimators
/// (port of <c>RegClass.TRegClass</c> / <c>amcClass.TAMCStats</c> math).
/// </summary>
public sealed class WeightedRegressionAccumulator
{
    public double SumW { get; private set; }
    public double SumWX { get; private set; }
    public double SumWY { get; private set; }
    public double SumWX2 { get; private set; }
    public double SumWY2 { get; private set; }
    public double SumWXY { get; private set; }
    public double SumDiff { get; private set; }
    public double SumDiff2 { get; private set; }
    public int N { get; private set; }

    public double MinX { get; private set; } = double.PositiveInfinity;
    public double MaxX { get; private set; } = double.NegativeInfinity;
    public double MinY { get; private set; } = double.PositiveInfinity;
    public double MaxY { get; private set; } = double.NegativeInfinity;

    public void Add(double x, double y) => AddW(x, y, 1.0);

    public void AddW(double x, double y, double w)
    {
        if (double.IsNaN(x) || double.IsNaN(y) || w <= 0.0) return;
        SumW += w;
        SumWX += w * x;
        SumWY += w * y;
        SumWX2 += w * x * x;
        SumWY2 += w * y * y;
        SumWXY += w * x * y;
        SumDiff += y - x;
        SumDiff2 += (y - x) * (y - x);
        N++;
        if (x < MinX) MinX = x;
        if (x > MaxX) MaxX = x;
        if (y < MinY) MinY = y;
        if (y > MaxY) MaxY = y;
    }

    public double MeanX => SumW > 0 ? SumWX / SumW : 0.0;
    public double MeanY => SumW > 0 ? SumWY / SumW : 0.0;
    public double Sxx => SumW > 0 ? SumWX2 - SumWX * SumWX / SumW : 0.0;
    public double Syy => SumW > 0 ? SumWY2 - SumWY * SumWY / SumW : 0.0;
    public double Sxy => SumW > 0 ? SumWXY - SumWX * SumWY / SumW : 0.0;

    /// <summary>Weighted variance of X, Sxx/(Sigma w - 1).</summary>
    public double VarX => SumW > 1 ? Sxx / (SumW - 1.0) : 0.0;

    /// <summary>Weighted variance of Y, Syy/(Sigma w - 1).</summary>
    public double VarY => SumW > 1 ? Syy / (SumW - 1.0) : 0.0;

    public double SdX => Math.Sqrt(Math.Max(0.0, VarX));
    public double SdY => Math.Sqrt(Math.Max(0.0, VarY));

    public double Bias => MeanY - MeanX;

    public double PercentBias(bool useMidpoint)
    {
        double reference = useMidpoint ? (MeanX + MeanY) / 2.0 : MeanX;
        return reference != 0.0 ? 100.0 * Bias / reference : 0.0;
    }

    public double SdDiff => N > 1 ? Math.Sqrt(Math.Max(0.0, (SumDiff2 - SumDiff * SumDiff / N) / (N - 1.0))) : 0.0;
    public double SeDiff => N > 0 ? SdDiff / Math.Sqrt(N) : 0.0;
    public double Df => Math.Max(0, N - 2);

    /// <summary>Paired t for the mean difference, t = bias/SE(diff).</summary>
    public double TValue
    {
        get
        {
            if (SeDiff > 0) return Bias / SeDiff;
            if (Bias != 0) return Math.Sign(Bias) * double.PositiveInfinity;
            return 0.0;
        }
    }

    public double TProb => Df > 0 ? StatDistributions.TTwoSidedP(TValue, Df) : double.NaN;

    /// <summary>Variance-ratio F = max(SDx^2, SDy^2)/min(...).</summary>
    public double FValue
    {
        get
        {
            double a = Math.Max(VarX, VarY);
            double b = Math.Min(VarX, VarY);
            return b > 0 ? a / b : 0.0;
        }
    }

    public double FProb
    {
        get
        {
            double df = Math.Max(1, N - 1);
            return FValue > 0 ? StatDistributions.FProb(FValue, df, df) : double.NaN;
        }
    }

    public double R => Sxx > 0 && Syy > 0 ? Sxy / Math.Sqrt(Sxx * Syy) : 0.0;

    public double R2 => R * R;

    /// <summary>Median of (Y - X) over the accumulated pairs.</summary>
    public double MedianBias(IReadOnlyList<DataPoint> points) => Descriptive.RobustStatistics.Median(points.Select(p => p.Difference));

    public void Clear()
    {
        SumW = SumWX = SumWY = SumWX2 = SumWY2 = SumWXY = SumDiff = SumDiff2 = 0.0;
        N = 0;
        MinX = MinY = double.PositiveInfinity;
        MaxX = MaxY = double.NegativeInfinity;
    }
}

/// <summary>Closed-form OLS / Deming estimators and post-fit uncertainty terms.</summary>
public static class RegressionMath
{
    /// <summary>Weighted ordinary least squares of Y on X.</summary>
    public static (double Slope, double Intercept) Ols(WeightedRegressionAccumulator a)
    {
        if (a.N < 2 || a.Sxx <= 0.0) return (0.0, a.MeanY);
        double b = a.Sxy / a.Sxx;
        return (b, a.MeanY - b * a.MeanX);
    }

    /// <summary>
    /// Deming regression closed form:
    /// b = (Syy - lambda*Sxx + sqrt((Syy - lambda*Sxx)^2 + 4*lambda*Sxy^2)) / (2*Sxy),
    /// lambda = (SD_yError/SD_xError)^2; the sign of b follows sign(Sxy).
    /// Falls back to OLS when the solution is unusable.
    /// </summary>
    public static (double Slope, double Intercept) Deming(WeightedRegressionAccumulator a, double sx, double sy)
    {
        if (a.N < 2 || a.Sxx <= 0.0) return (0.0, a.MeanY);
        if (sx <= 0.0 || sy <= 0.0 || a.Sxy == 0.0) return Ols(a);

        double lambda = (sy / sx) * (sy / sx);
        double diff = a.Syy - lambda * a.Sxx;
        double root = Math.Sqrt(diff * diff + 4.0 * lambda * a.Sxy * a.Sxy);
        double b = (diff + root) / (2.0 * a.Sxy);
        if (Math.Sign(b) != Math.Sign(a.Sxy)) b = (diff - root) / (2.0 * a.Sxy);
        if (!double.IsFinite(b)) return Ols(a);
        return (b, a.MeanY - b * a.MeanX);
    }

    /// <summary>
    /// Full fit with residual terms (port of <c>InternalPostCalc</c>):
    /// SEE^2 = (b^2*Sxx - 2*b*Sxy + Syy)/(n-2), SE_slope = SEE/sqrt(Sxx),
    /// SE_intercept = SEE*sqrt(1/Sigma w + xbar^2/Sxx), CIs from t(n-2).
    /// </summary>
    public static LinearFit PostCalc(WeightedRegressionAccumulator a, double slope, double intercept, double confidence = 0.95)
    {
        int n = a.N;
        double see = 0.0;
        if (n > 2)
        {
            double ss = slope * slope * a.Sxx - 2.0 * slope * a.Sxy + a.Syy;
            see = ss > 0.0 ? Math.Sqrt(ss / (n - 2.0)) : 0.0;
        }

        double seB = a.Sxx > 0 ? see / Math.Sqrt(a.Sxx) : 0.0;
        double seA = a.SumW > 0 ? see * Math.Sqrt(1.0 / a.SumW + a.MeanX * a.MeanX / a.Sxx) : 0.0;
        double t = n > 2 ? StatDistributions.TLevel(confidence, n - 2) : 0.0;

        return new LinearFit(
            slope, intercept, n, a.Sxx, a.Syy, a.Sxy, see, seB, seA,
            slope - t * seB, slope + t * seB,
            intercept - t * seA, intercept + t * seA);
    }

    /// <summary>Weighted least squares with explicit weights (per-point).</summary>
    public static LinearFit WeightedOls(IEnumerable<DataPoint> points, Func<DataPoint, double> weight, double confidence = 0.95)
    {
        var acc = new WeightedRegressionAccumulator();
        foreach (var p in points) acc.AddW(p.X, p.Y, weight(p));
        var (b, a) = Ols(acc);
        return PostCalc(acc, b, a, confidence);
    }

    /// <summary>Unweighted OLS convenience overload.</summary>
    public static LinearFit Ols(IEnumerable<DataPoint> points, double confidence = 0.95)
    {
        var acc = new WeightedRegressionAccumulator();
        foreach (var p in points) acc.Add(p.X, p.Y);
        var (b, a) = Ols(acc);
        return PostCalc(acc, b, a, confidence);
    }

    /// <summary>Deming fit convenience overload with error SDs.</summary>
    public static LinearFit Deming(IEnumerable<DataPoint> points, double sx, double sy, double confidence = 0.95)
    {
        var acc = new WeightedRegressionAccumulator();
        foreach (var p in points) acc.Add(p.X, p.Y);
        var (b, a) = Deming(acc, sx, sy);
        return PostCalc(acc, b, a, confidence);
    }

    /// <summary>
    /// Iterative weighted Deming regression with proportional-error weights
    /// (Linnet method, port of <c>UMC1EP9A3.WeightedDeming</c>):
    /// m = (X + lambda*Y)/(1+lambda) initially, w = 1/m^2, then re-evaluate
    /// m from the fitted line until slope/intercept converge.
    /// </summary>
    public static LinearFit WeightedDeming(
        IReadOnlyList<DataPoint> points,
        double lambda,
        double confidence = 0.95,
        int maxIterations = 100,
        double tolerance = 1e-10)
    {
        if (points.Count < 2) throw new ArgumentException("At least two points are required.", nameof(points));

        double[] weights = points.Select(p => WeightFor(p.X, p.Y, lambda, 0.0, 0.0, firstPass: true)).ToArray();
        double slope = 0.0, intercept = 0.0;

        for (int iteration = 0; iteration < maxIterations; iteration++)
        {
            var acc = new WeightedRegressionAccumulator();
            for (int i = 0; i < points.Count; i++) acc.AddW(points[i].X, points[i].Y, weights[i]);
            (double b, double a) = Deming(acc, lambda);

            bool converged = iteration > 0
                             && Math.Abs(b - slope) <= tolerance * Math.Max(1.0, Math.Abs(b))
                             && Math.Abs(a - intercept) <= tolerance * Math.Max(1.0, Math.Abs(a));

            slope = b;
            intercept = a;

            for (int i = 0; i < points.Count; i++)
                weights[i] = WeightFor(points[i].X, points[i].Y, lambda, slope, intercept, firstPass: false);

            if (converged)
            {
                var final = new WeightedRegressionAccumulator();
                for (int i = 0; i < points.Count; i++) final.AddW(points[i].X, points[i].Y, weights[i]);
                return PostCalc(final, slope, intercept, confidence);
            }
        }

        {
            var final = new WeightedRegressionAccumulator();
            for (int i = 0; i < points.Count; i++) final.AddW(points[i].X, points[i].Y, weights[i]);
            return PostCalc(final, slope, intercept, confidence);
        }
    }

    private static double WeightFor(double x, double y, double lambda, double slope, double intercept, bool firstPass)
    {
        double m;
        if (firstPass)
        {
            m = (x + lambda * y) / (1.0 + lambda);
        }
        else
        {
            double yHat = intercept + slope * x;
            double xHat = slope != 0.0 ? (y - intercept) / slope : x;
            m = (xHat + lambda * yHat) / (1.0 + lambda);
        }

        if (!double.IsFinite(m) || m <= 0.0) return 1.0;
        return 1.0 / (m * m);
    }

    private static (double Slope, double Intercept) Deming(WeightedRegressionAccumulator a, double lambda)
    {
        if (a.N < 2 || a.Sxx <= 0.0) return (0.0, a.MeanY);
        if (a.Sxy == 0.0) return Ols(a);
        double diff = a.Syy - lambda * a.Sxx;
        double root = Math.Sqrt(diff * diff + 4.0 * lambda * a.Sxy * a.Sxy);
        double b = (diff + root) / (2.0 * a.Sxy);
        if (Math.Sign(b) != Math.Sign(a.Sxy)) b = (diff - root) / (2.0 * a.Sxy);
        if (!double.IsFinite(b)) return Ols(a);
        return (b, a.MeanY - b * a.MeanX);
    }

    /// <summary>
    /// Jackknife bias-corrected estimate and standard error
    /// (port of <c>JackknifeSlopeIcept</c>):
    /// theta*_i = n*theta - (n-1)*theta_(-i); SE = sqrt(Sigma(theta* - mean)^2/(n(n-1))).
    /// </summary>
    public static (double Estimate, double StandardError) Jackknife(
        IReadOnlyList<DataPoint> points,
        Func<IReadOnlyList<DataPoint>, double> estimator)
    {
        int n = points.Count;
        if (n < 3) throw new ArgumentException("Jackknife requires at least three points.", nameof(points));

        double full = estimator(points);
        double[] pseudo = new double[n];
        for (int i = 0; i < n; i++)
        {
            var subset = new List<DataPoint>(n - 1);
            for (int j = 0; j < n; j++) if (j != i) subset.Add(points[j]);
            double minusI = estimator(subset);
            pseudo[i] = n * full - (n - 1.0) * minusI;
        }

        double mean = pseudo.Average();
        double se = Math.Sqrt(pseudo.Sum(v => (v - mean) * (v - mean)) / (n * (n - 1.0)));
        return (mean, se);
    }
}
