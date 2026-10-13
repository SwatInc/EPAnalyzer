using EPAnalyzer.Statistics.Distributions;

namespace EPAnalyzer.Statistics.Roc;

/// <summary>Confusion matrix at a cutoff (higher score = positive).</summary>
public sealed class ConfusionMatrix
{
    public required int TruePositive { get; init; }
    public required int FalsePositive { get; init; }
    public required int FalseNegative { get; init; }
    public required int TrueNegative { get; init; }

    public int Positives => TruePositive + FalseNegative;
    public int Negatives => TrueNegative + FalsePositive;
    public int Total => Positives + Negatives;

    public double Efficiency => Total > 0 ? (double)(TruePositive + TrueNegative) / Total : double.NaN;

    /// <summary>Sensitivity = TP/(TP+FN).</summary>
    public double Sensitivity => Positives > 0 ? (double)TruePositive / Positives : double.NaN;

    /// <summary>Specificity = TN/(TN+FP).</summary>
    public double Specificity => Negatives > 0 ? (double)TrueNegative / Negatives : double.NaN;

    /// <summary>Positive predictive value = TP/(TP+FP).</summary>
    public double PredictiveValuePositive => TruePositive + FalsePositive > 0 ? (double)TruePositive / (TruePositive + FalsePositive) : double.NaN;

    /// <summary>Negative predictive value = TN/(TN+FN).</summary>
    public double PredictiveValueNegative => TrueNegative + FalseNegative > 0 ? (double)TrueNegative / (TrueNegative + FalseNegative) : double.NaN;

    /// <summary>Positive likelihood ratio = sensitivity/(1 - specificity).</summary>
    public double LikelihoodRatioPositive
    {
        get
        {
            double spec = Specificity;
            return spec < 1.0 ? Sensitivity / (1.0 - spec) : double.PositiveInfinity;
        }
    }

    /// <summary>Negative likelihood ratio = (1 - sensitivity)/specificity.</summary>
    public double LikelihoodRatioNegative
    {
        get
        {
            double spec = Specificity;
            return spec > 0.0 ? (1.0 - Sensitivity) / spec : double.PositiveInfinity;
        }
    }

    /// <summary>Youden index = sensitivity + specificity - 1.</summary>
    public double Youden => Sensitivity + Specificity - 1.0;

    /// <summary>Sensitivity CI: exact Clopper-Pearson, or Wilson score interval when exact = false.</summary>
    public (double Lower, double Upper) SensitivityConfidenceInterval(bool exact = true, double alpha = 0.05)
        => exact ? StatDistributions.ExactBinomialCi(TruePositive, Positives, alpha)
                 : StatDistributions.WilsonScoreCi(TruePositive, Positives, alpha);

    /// <summary>Specificity CI: exact Clopper-Pearson, or Wilson score interval when exact = false.</summary>
    public (double Lower, double Upper) SpecificityConfidenceInterval(bool exact = true, double alpha = 0.05)
        => exact ? StatDistributions.ExactBinomialCi(TrueNegative, Negatives, alpha)
                 : StatDistributions.WilsonScoreCi(TrueNegative, Negatives, alpha);

    public double Kappa
    {
        get
        {
            if (Total == 0) return double.NaN;
            double po = Efficiency;
            double pe = ((double)(TruePositive + FalsePositive) / Total) * ((double)(TruePositive + FalseNegative) / Total)
                        + ((double)(TrueNegative + FalseNegative) / Total) * ((double)(TrueNegative + FalsePositive) / Total);
            return pe < 1.0 ? (po - pe) / (1.0 - pe) : double.NaN;
        }
    }
}

/// <summary>AUC with the DeLong variance components.</summary>
public sealed class AucResult
{
    public required double Auc { get; init; }
    public required double Variance { get; init; }
    public required double V10 { get; init; }
    public required double V01 { get; init; }
    public required int Positives { get; init; }
    public required int Negatives { get; init; }
    public required IReadOnlyList<double> PositiveKernelMeans { get; init; }
    public required IReadOnlyList<double> NegativeKernelMeans { get; init; }

    public double StandardError => Math.Sqrt(Variance);

    public (double Lower, double Upper) ConfidenceInterval(double confidence = 0.95)
    {
        double z = StatDistributions.NormalQuantile(0.5 + confidence / 2.0);
        double half = z * StandardError;
        return (Math.Clamp(Auc - half, 0.0, 1.0), Math.Clamp(Auc + half, 0.0, 1.0));
    }
}

/// <summary>Two-curve comparison (port of <c>TROCCmprClass.InternalCalc</c>).</summary>
public sealed class AucComparison
{
    public required double Difference { get; init; }
    public required double Correlation { get; init; }
    public required double DifferenceVariance { get; init; }
    public required double DifferenceStandardError { get; init; }
    public required double ZStatistic { get; init; }
    public required double POneSided { get; init; }
    public required double PTwoSided { get; init; }
}

/// <summary>
/// ROC analysis (port of <c>rocclass</c>): non-parametric AUC with the
/// DeLong variance/covariance, two-curve comparison, and the per-cutoff
/// sensitivity/specificity table with exact or score confidence intervals.
/// </summary>
public static class RocAnalysis
{
    private const double PsiEpsilon = 1e-5;

    /// <summary>AUC kernel psi(X,Y): 0.5 when |X-Y| &lt; eps, 1 when Y &gt; X, else 0.</summary>
    public static double Psi(double x, double y, double epsilon = PsiEpsilon)
    {
        if (Math.Abs(x - y) < epsilon) return 0.5;
        return y > x ? 1.0 : 0.0;
    }

    /// <summary>
    /// Computes the AUC and DeLong variance from positive and negative score
    /// samples. If the AUC is below 0.5 the complements are used (direction flip).
    /// </summary>
    public static AucResult Compute(IReadOnlyList<double> positives, IReadOnlyList<double> negatives, double epsilon = PsiEpsilon)
    {
        int m = positives.Count, n = negatives.Count;
        if (m < 1 || n < 1) throw new ArgumentException("Both groups need at least one observation.");

        var positiveMeans = new double[m];
        var negativeMeans = new double[n];

        double total = 0.0;
        for (int i = 0; i < m; i++)
        {
            double row = 0.0;
            for (int j = 0; j < n; j++) row += Psi(negatives[j], positives[i], epsilon);
            positiveMeans[i] = row / n;
            total += row;
        }

        for (int j = 0; j < n; j++)
        {
            double col = 0.0;
            for (int i = 0; i < m; i++) col += Psi(negatives[j], positives[i], epsilon);
            negativeMeans[j] = col / m;
        }

        double auc = total / (m * (double)n);
        if (auc < 0.5)
        {
            // Flip direction: psi becomes 1 - psi.
            auc = 1.0 - auc;
            for (int i = 0; i < m; i++) positiveMeans[i] = 1.0 - positiveMeans[i];
            for (int j = 0; j < n; j++) negativeMeans[j] = 1.0 - negativeMeans[j];
        }

        double v10 = m > 1 ? positiveMeans.Sum(v => (v - auc) * (v - auc)) / (m - 1.0) : 0.0;
        double v01 = n > 1 ? negativeMeans.Sum(v => (v - auc) * (v - auc)) / (n - 1.0) : 0.0;
        double variance = v10 / m + v01 / n;

        return new AucResult
        {
            Auc = auc,
            Variance = variance,
            V10 = v10,
            V01 = v01,
            Positives = m,
            Negatives = n,
            PositiveKernelMeans = positiveMeans,
            NegativeKernelMeans = negativeMeans,
        };
    }

    /// <summary>AUC standard error (DeLong).</summary>
    public static double StandardError(AucResult auc) => auc.StandardError;

    /// <summary>Hanley-McNeil standard error from the AUC components.</summary>
    public static double HanleyMcNeilStandardError(double auc, int positives, int negatives)
    {
        double q1 = auc / (2.0 - auc);
        double q2 = 2.0 * auc * auc / (1.0 + auc);
        return Math.Sqrt((auc * (1.0 - auc) + (positives - 1) * (q1 - auc * auc) + (negatives - 1) * (q2 - auc * auc))
                         / (positives * (double)negatives));
    }

    /// <summary>DeLong covariance between two AUCs on the same subjects.</summary>
    public static double Covariance(AucResult a, AucResult b)
    {
        if (a.Positives != b.Positives || a.Negatives != b.Negatives)
            throw new ArgumentException("Both AUCs must be computed on the same positive/negative subjects.");

        int m = a.Positives, n = a.Negatives;
        double cov10 = 0.0;
        for (int i = 0; i < m; i++) cov10 += (a.PositiveKernelMeans[i] - a.Auc) * (b.PositiveKernelMeans[i] - b.Auc);
        cov10 /= m > 1 ? m - 1.0 : 1.0;

        double cov01 = 0.0;
        for (int j = 0; j < n; j++) cov01 += (a.NegativeKernelMeans[j] - a.Auc) * (b.NegativeKernelMeans[j] - b.Auc);
        cov01 /= n > 1 ? n - 1.0 : 1.0;

        return cov10 / m + cov01 / n;
    }

    /// <summary>Compares two AUCs: diff = AUC1 - AUC2, r = Cov/sqrt(V1*V2), SE = sqrt(V1+V2-2Cov), z, p values.</summary>
    public static AucComparison Compare(AucResult a, AucResult b)
    {
        double difference = a.Auc - b.Auc;
        double covariance = Covariance(a, b);
        double variance = a.Variance + b.Variance - 2.0 * covariance;
        double se = Math.Sqrt(Math.Max(0.0, variance));
        double r = a.Variance > 0 && b.Variance > 0 ? covariance / Math.Sqrt(a.Variance * b.Variance) : double.NaN;
        double z = se > 0 ? difference / se : double.NaN;
        double pOne = double.IsNaN(z) ? double.NaN : 1.0 - StatDistributions.NormalCdf(Math.Abs(z));
        double pTwo = double.IsNaN(z) ? double.NaN : 2.0 * pOne;

        return new AucComparison
        {
            Difference = difference,
            Correlation = r,
            DifferenceVariance = variance,
            DifferenceStandardError = se,
            ZStatistic = z,
            POneSided = pOne,
            PTwoSided = pTwo,
        };
    }

    /// <summary>Builds the confusion matrix at a cutoff (score &gt;= cutoff is positive).</summary>
    public static ConfusionMatrix AtCutoff(IReadOnlyList<double> scores, IReadOnlyList<bool> isPositive, double cutoff)
    {
        int tp = 0, fp = 0, fn = 0, tn = 0;
        for (int i = 0; i < scores.Count; i++)
        {
            bool predicted = scores[i] >= cutoff;
            if (isPositive[i]) { if (predicted) tp++; else fn++; }
            else { if (predicted) fp++; else tn++; }
        }
        return new ConfusionMatrix { TruePositive = tp, FalsePositive = fp, FalseNegative = fn, TrueNegative = tn };
    }

    /// <summary>
    /// Per-cutoff table (port of <c>TROCClass.Calc</c>), one row per unique
    /// cutoff, and the best cutoff by the Youden index.
    /// </summary>
    public static IReadOnlyList<(double Cutoff, ConfusionMatrix Matrix, double Youden)> CutoffTable(
        IReadOnlyList<double> scores,
        IReadOnlyList<bool> isPositive)
    {
        var cutoffs = scores.Distinct().OrderBy(c => c).ToArray();
        return cutoffs.Select(c =>
        {
            var matrix = AtCutoff(scores, isPositive, c);
            return (c, matrix, matrix.Youden);
        }).ToArray();
    }

    public static (double Cutoff, ConfusionMatrix Matrix, double Youden) BestCutoff(
        IReadOnlyList<double> scores,
        IReadOnlyList<bool> isPositive)
        => CutoffTable(scores, isPositive).OrderByDescending(t => t.Youden).First();

    /// <summary>Two-state method agreement (QMC): overall percent agreement and kappa.</summary>
    public static (double PercentAgreement, double Kappa) Agreement(
        int bothPositive, int bothNegative, int referencePositiveComparatorNegative, int referenceNegativeComparatorPositive)
    {
        int total = bothPositive + bothNegative + referencePositiveComparatorNegative + referenceNegativeComparatorPositive;
        if (total == 0) return (double.NaN, double.NaN);
        double po = (double)(bothPositive + bothNegative) / total;
        double prefPos = (double)(bothPositive + referencePositiveComparatorNegative) / total;
        double prefNeg = (double)(bothNegative + referenceNegativeComparatorPositive) / total;
        double compPos = (double)(bothPositive + referenceNegativeComparatorPositive) / total;
        double compNeg = (double)(bothNegative + referencePositiveComparatorNegative) / total;
        double pe = prefPos * compPos + prefNeg * compNeg;
        return (100.0 * po, pe < 1.0 ? (po - pe) / (1.0 - pe) : double.NaN);
    }
}
