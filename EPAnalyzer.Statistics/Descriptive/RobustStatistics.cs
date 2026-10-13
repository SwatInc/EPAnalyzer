namespace EPAnalyzer.Statistics.Descriptive;

/// <summary>
/// Robust statistics (port of the EP Evaluator <c>Robstat</c> unit and the
/// Tukey biweight iteration). Used by reference intervals and outlier handling.
/// </summary>
public static class RobustStatistics
{
    /// <summary>Classic median (middle value, or mean of the two middle values).</summary>
    public static double Median(IEnumerable<double> source)
    {
        double[] v = source.Where(x => !double.IsNaN(x)).ToArray();
        if (v.Length == 0) return double.NaN;
        Array.Sort(v);
        return v.Length % 2 == 1 ? v[v.Length / 2] : (v[v.Length / 2 - 1] + v[v.Length / 2]) / 2.0;
    }

    /// <summary>Median absolute deviation, unscaled.</summary>
    public static double Mad(IEnumerable<double> source)
    {
        double median = Median(source);
        return Median(source.Select(x => Math.Abs(x - median)));
    }

    /// <summary>Normal-consistent median absolute deviation, MAD/0.6745 (the EP <c>smad</c>).</summary>
    public static double Smad(IEnumerable<double> source) => Mad(source) / 0.6745;

    /// <summary>Mean absolute deviation from the median, Sigma|x - median|/n.</summary>
    public static double MeanAbsoluteDeviation(IEnumerable<double> source)
    {
        double[] v = source.ToArray();
        if (v.Length == 0) return double.NaN;
        double median = Median(v);
        return v.Sum(x => Math.Abs(x - median)) / v.Length;
    }

    /// <summary>
    /// One Tukey biweight (M-estimator) iteration:
    /// u = (x - c)/(9*s), w = (1-u^2)^2 for |u| &lt; 1, location update
    /// c' = c + Sigma(x-c)w/Sigma w, scale update from the (1-5u^2)(1-u^2) weights.
    /// </summary>
    public static (double Location, double Scale) OneIteration(IReadOnlyList<double> values, double location, double scale)
    {
        if (values.Count == 0) return (location, scale);
        if (scale <= 0.0) scale = Smad(values);

        double sumW = 0.0;
        double sumDeltaW = 0.0;
        double sumDelta2W = 0.0;
        double sumCorrection = 0.0;

        foreach (double x in values)
        {
            double u = (x - location) / (9.0 * scale);
            if (Math.Abs(u) >= 1.0) continue;
            double u2 = u * u;
            double w = (1.0 - u2) * (1.0 - u2);
            double delta = x - location;
            sumW += w;
            sumDeltaW += delta * w;
            sumDelta2W += delta * delta * w;
            sumCorrection += delta * delta * (1.0 - 5.0 * u2) * (1.0 - u2);
        }

        if (sumW <= 0.0) return (location, scale);

        double newLocation = location + sumDeltaW / sumW;
        double variance = Math.Max(0.0, sumCorrection / sumW);
        double newScale = scale;

        // Tukey biweight scale correction factor: s' = s * sqrt(n * Sigma u^2 (1-u^2)^4 / |Sigma (1-u^2)(1-5u^2)|)
        double sumU2W4 = 0.0;
        double sumCorr2 = 0.0;
        foreach (double x in values)
        {
            double u = (x - newLocation) / (9.0 * scale);
            if (Math.Abs(u) >= 1.0) continue;
            double u2 = u * u;
            double w = (1.0 - u2) * (1.0 - u2);
            sumU2W4 += u2 * w * w;
            sumCorr2 += (1.0 - u2) * (1.0 - 5.0 * u2);
        }

        if (Math.Abs(sumCorr2) > 1e-12)
            newScale = 9.0 * scale * Math.Sqrt(values.Count * sumU2W4) / Math.Abs(sumCorr2);

        if (newScale <= 0.0 || double.IsNaN(newScale)) newScale = Math.Sqrt(variance);

        return (newLocation, newScale);
    }

    /// <summary>Iterated Tukey biweight robust location/scale (convergence tolerance 1e-4).</summary>
    public static (double Location, double Scale) TukeyRobust(IEnumerable<double> source, int maxIterations = 50, double tolerance = 1e-4)
    {
        double[] v = source.Where(x => !double.IsNaN(x)).ToArray();
        if (v.Length == 0) return (double.NaN, double.NaN);

        double location = Median(v);
        double scale = Smad(v);
        if (scale <= 0.0) return (location, 0.0);

        for (int i = 0; i < maxIterations; i++)
        {
            var (newLocation, newScale) = OneIteration(v, location, scale);
            bool converged = Math.Abs(newLocation - location) < tolerance * Math.Max(1.0, Math.Abs(location))
                             && Math.Abs(newScale - scale) < tolerance * Math.Max(1.0, Math.Abs(scale));
            location = newLocation;
            scale = newScale;
            if (converged) break;
        }

        return (location, scale);
    }

    /// <summary>Robust CV in percent.</summary>
    public static double RobustCv(IEnumerable<double> source)
    {
        var (location, scale) = TukeyRobust(source);
        return location != 0.0 ? 100.0 * scale / Math.Abs(location) : double.NaN;
    }
}
