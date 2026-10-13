using EPAnalyzer.Statistics.Distributions;
using MathNet.Numerics.LinearAlgebra;

namespace EPAnalyzer.Statistics.Regression;

/// <summary>
/// Weighted polynomial regression with lack-of-fit decomposition
/// (port of <c>PRClass.TPRegClass</c>/<c>TFitInfo</c>).
/// Used by EP6 linearity, factor sensitivity and interference dose-response fits.
/// </summary>
public sealed class PolynomialFit
{
    private readonly double[] _coefficients;
    private readonly double[][] _inverseNormalMatrix;
    private readonly double[] _xRaw;

    internal PolynomialFit(double[] coefficients, double[][] inverseNormalMatrix, double[] xRaw, double residualMs, double residualDf)
    {
        _coefficients = coefficients;
        _inverseNormalMatrix = inverseNormalMatrix;
        _xRaw = xRaw;
        ResidualMeanSquare = residualMs;
        ResidualDegreesOfFreedom = residualDf;
    }

    public IReadOnlyList<double> Coefficients => _coefficients;
    public int Degree => _coefficients.Length - 1;
    public double ResidualMeanSquare { get; }
    public double ResidualDegreesOfFreedom { get; }

    /// <summary>Evaluates the fitted polynomial at x.</summary>
    public double Predict(double x)
    {
        double total = 0.0;
        double power = 1.0;
        foreach (double c in _coefficients)
        {
            total += c * power;
            power *= x;
        }
        return total;
    }

    /// <summary>Standard error of a coefficient, sqrt(inv(X'WX)_jj * residualMS).</summary>
    public double CoefficientStandardError(int index)
        => index >= 0 && index < _inverseNormalMatrix.Length
            ? Math.Sqrt(Math.Max(0.0, _inverseNormalMatrix[index][index] * ResidualMeanSquare))
            : double.NaN;

    /// <summary>Two-sided t factor for the given confidence.</summary>
    public double TFactor(double confidence = 0.95) => StatDistributions.TLevel(confidence, ResidualDegreesOfFreedom);

    /// <summary>Confidence interval of the fitted curve at x (half-width).</summary>
    public double ConfidenceHalfWidth(double x, double confidence = 0.95)
    {
        double[] basis = Basis(x);
        double var = QuadraticForm(_inverseNormalMatrix, basis);
        return TFactor(confidence) * Math.Sqrt(Math.Max(0.0, var) * ResidualMeanSquare);
    }

    /// <summary>Prediction interval at x (half-width).</summary>
    public double PredictionHalfWidth(double x, double confidence = 0.95)
    {
        double[] basis = Basis(x);
        double var = QuadraticForm(_inverseNormalMatrix, basis);
        return TFactor(confidence) * Math.Sqrt(Math.Max(0.0, (1.0 + var) * ResidualMeanSquare));
    }

    public (double Lower, double Upper) ConfidenceInterval(double x, double confidence = 0.95)
    {
        double h = ConfidenceHalfWidth(x, confidence);
        double y = Predict(x);
        return (y - h, y + h);
    }

    public (double Lower, double Upper) PredictionInterval(double x, double confidence = 0.95)
    {
        double h = PredictionHalfWidth(x, confidence);
        double y = Predict(x);
        return (y - h, y + h);
    }

    /// <summary>|estimate/SE| for the selected coefficient.</summary>
    public double StandardizedCoefficient(int index)
    {
        double se = CoefficientStandardError(index);
        return se > 0 ? Math.Abs(_coefficients[index]) / se : double.NaN;
    }

    private double[] Basis(double x)
    {
        var b = new double[_coefficients.Length];
        double power = 1.0;
        for (int i = 0; i < b.Length; i++) { b[i] = power; power *= x; }
        return b;
    }

    private static double QuadraticForm(double[][] m, double[] v)
    {
        double total = 0.0;
        for (int i = 0; i < v.Length; i++)
            for (int j = 0; j < v.Length; j++)
                total += v[i] * m[i][j] * v[j];
        return total;
    }
}

/// <summary>Full polynomial regression report including lack-of-fit statistics.</summary>
public sealed class PolynomialRegressionResult
{
    public required PolynomialFit Fit { get; init; }
    public required double ResidualSumOfSquares { get; init; }
    public required double ResidualDegreesOfFreedom { get; init; }
    public required double PureErrorSumOfSquares { get; init; }
    public required int PureErrorDegreesOfFreedom { get; init; }
    public required double LackOfFitSumOfSquares { get; init; }
    public required int LackOfFitDegreesOfFreedom { get; init; }
    public required double LackOfFitMeanSquare { get; init; }
    public required double PureErrorMeanSquare { get; init; }
    public required double FStatistic { get; init; }
    public required double FPValue { get; init; }
    public required double RSquared { get; init; }
    public required int N { get; init; }

    public bool LackOfFitSignificant(double alpha = 0.05) => FPValue <= alpha;
}

/// <summary>
/// Polynomial least squares with lack-of-fit F-test (pure error from replicate
/// groups) and degree selection, as used by EP6 linearity.
/// </summary>
public static class PolynomialRegression
{
    /// <summary>
    /// Weighted polynomial fit. Groups are replicate groups used for the pure-error term;
    /// pass <paramref name="groups"/> = null when no replication is available.
    /// </summary>
    public static PolynomialRegressionResult Fit(
        IReadOnlyList<DataPoint> points,
        int degree,
        IReadOnlyList<IReadOnlyList<double>>? groups = null,
        Func<DataPoint, double>? weight = null)
    {
        if (points.Count <= degree + 1) throw new ArgumentException("Not enough points for the requested degree.", nameof(points));
        weight ??= _ => 1.0;

        int terms = degree + 1;
        var normal = Matrix<double>.Build.Dense(terms, terms);
        var rhs = Vector<double>.Build.Dense(terms);

        foreach (var p in points)
        {
            double w = weight(p);
            if (w <= 0) continue;
            double[] basis = Basis(p.X, terms);
            for (int i = 0; i < terms; i++)
            {
                rhs[i] += w * basis[i] * p.Y;
                for (int j = 0; j < terms; j++) normal[i, j] += w * basis[i] * basis[j];
            }
        }

        Matrix<double> inverse = normal.Inverse();
        Vector<double> beta = inverse * rhs;

        double residualSs = 0.0;
        double sumW = 0.0;
        double sumWY = 0.0;
        foreach (var p in points)
        {
            double w = weight(p);
            if (w <= 0) continue;
            double yHat = Evaluate(beta, p.X);
            residualSs += w * (p.Y - yHat) * (p.Y - yHat);
            sumW += w;
            sumWY += w * p.Y;
        }

        double meanY = sumW > 0 ? sumWY / sumW : 0.0;
        double totalSs = points.Where(p => weight(p) > 0).Sum(p => weight(p) * (p.Y - meanY) * (p.Y - meanY));
        double r2 = totalSs > 0 ? 1.0 - residualSs / totalSs : 0.0;

        int n = points.Count;
        int residualDf = n - terms;
        double pureErrorSs = 0.0;
        int pureErrorDf = 0;
        if (groups != null)
        {
            foreach (var group in groups)
            {
                if (group.Count < 2) continue;
                double gm = group.Average();
                pureErrorSs += group.Sum(v => (v - gm) * (v - gm));
                pureErrorDf += group.Count - 1;
            }
        }

        double lackOfFitSs = Math.Max(0.0, residualSs - pureErrorSs);
        int lackOfFitDf = residualDf - pureErrorDf;
        double pureErrorMs = pureErrorDf > 0 ? pureErrorSs / pureErrorDf : 0.0;
        double lackOfFitMs = lackOfFitDf > 0 ? lackOfFitSs / lackOfFitDf : 0.0;
        double f;
        double pValue;
        if (lackOfFitDf > 0 && pureErrorDf > 0 && pureErrorSs <= 0.0 && lackOfFitSs > 1e-10 * Math.Max(1.0, totalSs))
        {
            // Exact replication with zero pure error: all residual variation is lack of fit.
            f = double.PositiveInfinity;
            pValue = 0.0;
        }
        else
        {
            f = lackOfFitDf > 0 && pureErrorMs > 0 ? lackOfFitMs / pureErrorMs : double.NaN;
            pValue = double.IsNaN(f) ? double.NaN : StatDistributions.FProb(f, lackOfFitDf, pureErrorDf);
        }

        double[] invDiag = new double[terms];
        for (int i = 0; i < terms; i++) invDiag[i] = inverse[i, i];
        var invArray = new double[terms][];
        for (int i = 0; i < terms; i++)
        {
            invArray[i] = new double[terms];
            for (int j = 0; j < terms; j++) invArray[i][j] = inverse[i, j];
        }

        double residualMs = residualDf > 0 ? residualSs / residualDf : 0.0;
        var fit = new PolynomialFit(beta.ToArray(), invArray, points.Select(p => p.X).ToArray(), residualMs, residualDf);

        return new PolynomialRegressionResult
        {
            Fit = fit,
            ResidualSumOfSquares = residualSs,
            ResidualDegreesOfFreedom = residualDf,
            PureErrorSumOfSquares = pureErrorSs,
            PureErrorDegreesOfFreedom = pureErrorDf,
            LackOfFitSumOfSquares = lackOfFitSs,
            LackOfFitDegreesOfFreedom = lackOfFitDf,
            LackOfFitMeanSquare = lackOfFitMs,
            PureErrorMeanSquare = pureErrorMs,
            FStatistic = f,
            FPValue = pValue,
            RSquared = r2,
            N = n,
        };
    }

    /// <summary>
    /// Chooses the lowest degree (from 1..maxDegree) whose lack-of-fit is not
    /// significant at alpha; falls back to the degree with the largest p-value.
    /// </summary>
    public static (int Degree, PolynomialRegressionResult Result) BestDegree(
        IReadOnlyList<DataPoint> points,
        IReadOnlyList<IReadOnlyList<double>>? groups = null,
        int maxDegree = 3,
        Func<DataPoint, double>? weight = null,
        double alpha = 0.05)
    {
        int best = 1;
        PolynomialRegressionResult? bestResult = null;
        double bestP = double.NegativeInfinity;

        for (int d = 1; d <= maxDegree; d++)
        {
            if (points.Count <= d + 1) break;
            var result = Fit(points, d, groups, weight);
            double p = result.FPValue;
            if (!double.IsNaN(p) && p >= alpha) return (d, result);
            if (double.IsNaN(bestP) || p > bestP)
            {
                bestP = p;
                best = d;
                bestResult = result;
            }
        }

        return (best, bestResult ?? Fit(points, 1, groups, weight));
    }

    /// <summary>
    /// Deviation from linearity: fitted polynomial minus the straight line
    /// (slope/intercept from OLS) at each assigned concentration.
    /// </summary>
    public static IReadOnlyList<(double X, double Deviation)> DeviationFromLinearity(
        PolynomialRegressionResult polynomial,
        IReadOnlyList<DataPoint> points)
    {
        var acc = new WeightedRegressionAccumulator();
        foreach (var p in points) acc.Add(p.X, p.Y);
        var (b, a) = RegressionMath.Ols(acc);
        return points.Select(p => (p.X, polynomial.Fit.Predict(p.X) - (a + b * p.X))).ToArray();
    }

    private static double[] Basis(double x, int terms)
    {
        var b = new double[terms];
        double power = 1.0;
        for (int i = 0; i < terms; i++) { b[i] = power; power *= x; }
        return b;
    }

    private static double Evaluate(Vector<double> beta, double x)
    {
        double total = 0.0;
        double power = 1.0;
        for (int i = 0; i < beta.Count; i++) { total += beta[i] * power; power *= x; }
        return total;
    }
}
