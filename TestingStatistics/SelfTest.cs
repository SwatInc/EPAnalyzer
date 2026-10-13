using EPAnalyzer.Statistics.Coagulation;
using EPAnalyzer.Statistics.Costs;
using EPAnalyzer.Statistics.Descriptive;
using EPAnalyzer.Statistics.Detection;
using EPAnalyzer.Statistics.Distributions;
using EPAnalyzer.Statistics.Linearity;
using EPAnalyzer.Statistics.MethodComparison;
using EPAnalyzer.Statistics.Precision;
using EPAnalyzer.Statistics.QualityControl;
using EPAnalyzer.Statistics.ReferenceIntervals;
using EPAnalyzer.Statistics.Regression;
using EPAnalyzer.Statistics.Roc;
using EPAnalyzer.Statistics.Trueness;
using EpSimplePrecision = EPAnalyzer.Statistics.Precision.SimplePrecision;
using EpComplexPrecision = EPAnalyzer.Statistics.Precision.ComplexPrecision;

namespace TestingStatistics;

/// <summary>
/// Known-value checks for every formula family in EPAnalyzer.Statistics.
/// Run with the console harness; throws when a check fails.
/// </summary>
internal static class SelfTest
{
    private static int _passed;
    private static int _failed;

    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        TestDescriptive();
        TestDistributions();
        TestRegression();
        TestPassingBablok();
        TestMethodComparison();
        TestAnova();
        TestEp5();
        TestEp10();
        TestLinearity();
        TestEp6();
        TestLoq();
        TestRoc();
        TestReferenceIntervals();
        TestInr();
        TestTrueness();
        TestQc();
        TestCosts();

        Console.WriteLine($"Self-test summary: {_passed} passed, {_failed} failed.");
        if (_failed > 0) throw new InvalidOperationException($"{_failed} formula self-test(s) failed.");
    }

    private static void TestDescriptive()
    {
        var unit = new StatUnit();
        foreach (double v in new[] { 1.0, 2, 3, 4, 5 }) unit.Add(v);
        Near("mean", unit.Mean, 3.0);
        Near("sd", unit.StandardDeviation, Math.Sqrt(2.5));
        Near("cv", unit.Cv, 100.0 * Math.Sqrt(2.5) / 3.0);
        Near("df", unit.N - 1, 4);

        var ex = new StatUnitEx();
        foreach (double v in new[] { 1.0, 2, 3, 4, 5, 6, 7, 8, 9, 10 }) ex.Add(v);
        Near("median", ex.Median, 5.5);
        Near("p25", ex.P25, 3.25);
        Near("iqr", ex.Iqr, 4.5);

        var (loc, scale) = RobustStatistics.TukeyRobust(new double[] { 10, 11, 12, 13, 14, 100 });
        Near("robust location", loc, 12.0, 0.5);
        Check("robust scale > 0", scale > 0);
    }

    private static void TestDistributions()
    {
        // Chi-square SD factors for SD = 1, n = 3 (guide: 0.52 .. 6.29).
        var unit = new StatUnit();
        unit.Add(1); unit.Add(2); unit.Add(3);
        var (lo, hi) = unit.SdConfidenceInterval(0.05);
        Near("chi2 sd lower factor", lo / unit.StandardDeviation, 0.5206, 0.001);
        Near("chi2 sd upper factor", hi / unit.StandardDeviation, 6.2844, 0.01);

        // Verification value grows with confidence.
        double v95 = EpSimplePrecision.VerificationValue(2.5, 40, 0.95);
        double v99 = EpSimplePrecision.VerificationValue(2.5, 40, 0.99);
        Check("verification value 95 > sd", v95 > 2.5 && v95 < 3.2);
        Check("verification value 99 > 95", v99 > v95);

        // Clopper-Pearson x=0, n=10.
        var (cpLow, cpHigh) = StatDistributions.ExactBinomialCi(0, 10);
        Near("CP lower", cpLow, 0.0, 1e-12);
        Near("CP upper", cpHigh, 1.0 - Math.Pow(0.025, 0.1), 1e-4);

        // Wilson score x=5, n=10 symmetric around 0.5.
        var (wLow, wHigh) = StatDistributions.WilsonScoreCi(5, 10);
        Near("Wilson center", (wLow + wHigh) / 2.0, 0.5, 1e-9);
        Near("Wilson lower", wLow, 0.2366, 0.001);

        // t / F sanity.
        Near("t level 97.5 df 10", StatDistributions.TLevel(0.95, 10), 2.2281, 0.001);
        Near("F prob 1", StatDistributions.FProb(1.0, 10, 10), 0.5, 0.02);
    }

    private static void TestRegression()
    {
        var points = Enumerable.Range(1, 10).Select(x => new DataPoint(x, 3.0 + 2.0 * x)).ToArray();

        var ols = RegressionMath.Ols(points);
        Near("ols slope", ols.Slope, 2.0, 1e-9);
        Near("ols intercept", ols.Intercept, 3.0, 1e-9);
        Near("ols see", ols.See, 0.0, 1e-9);

        var deming = RegressionMath.Deming(points, 1.0, 1.0);
        Near("deming slope", deming.Slope, 2.0, 1e-9);
        Near("deming intercept", deming.Intercept, 3.0, 1e-9);

        var wdem = RegressionMath.WeightedDeming(points, 1.0);
        Near("weighted deming slope", wdem.Slope, 2.0, 1e-5);

        // Polynomial: quadratic data lacks fit against a line.
        var parabola = Enumerable.Range(1, 5)
            .SelectMany(x => new[] { new DataPoint(x, x * x), new DataPoint(x, x * x) }).ToArray();
        var groups = Enumerable.Range(1, 5).Select(x => (IReadOnlyList<double>)new[] { (double)(x * x), x * x }).ToArray();
        var poly = PolynomialRegression.Fit(parabola, 1, groups);
        Check("lack-of-fit significant for parabola", poly.LackOfFitSignificant());
        var quad = PolynomialRegression.Fit(parabola, 2, groups);
        Check("quadratic fit not significant", !quad.LackOfFitSignificant());
    }

    private static void TestPassingBablok()
    {
        var points = Enumerable.Range(1, 10).Select(x => new DataPoint(x, 1.0 + 1.0 * x)).ToArray();
        var pb = PassingBablok.Calculate(points);
        Near("pb slope", pb.Slope, 1.0, 1e-9);
        Near("pb intercept", pb.Intercept, 1.0, 1e-9);
        Check("pb ci contains slope", pb.SlopeCiLow <= 1.0 && pb.SlopeCiHigh >= 1.0);

        // Median of pairwise slopes with one outlier should stay at the line.
        var withOutlier = new List<DataPoint>
        {
            new(1, 2), new(2, 3), new(3, 4), new(4, 5), new(5, 6), new(6, 100),
        };
        var robust = PassingBablok.Calculate(withOutlier);
        Check("pb robust to outlier", Math.Abs(robust.Slope - 1.0) < 0.5);
    }

    private static void TestMethodComparison()
    {
        var mc = new MethodComparisonStats();
        for (int x = 1; x <= 10; x++) mc.Add(x, x + 1.0);
        Near("mc bias", mc.Bias, 1.0, 1e-9);
        Near("mc r", mc.R, 1.0, 1e-9);
        Near("mc slope", mc.Fit(RegressionModel.Ols).Slope, 1.0, 1e-9);
        Check("mc t large", Math.Abs(mc.TValue) > 10);

        var ratio = Ep9A3Regressions.PrecisionRatio(Enumerable.Range(1, 10).Select(x => new DataPoint(x, 2.0 * x)).ToArray());
        Near("precision ratio", ratio.Ratio, 0.5, 1e-9);
        Check("uniform scatter", ratio.UniformScatter);

        var jk = Ep9A3Regressions.JackknifeSlope(
            Enumerable.Range(1, 10).Select(x => new DataPoint(x, 1.0 + x)).ToArray(),
            pts => RegressionMath.Ols(pts).Slope);
        Near("jackknife slope", jk.Estimate, 1.0, 1e-6);
    }

    private static void TestAnova()
    {
        var result = Anova.OneWay(new[]
        {
            new double[] { 10, 11, 12 },
            new double[] { 20, 21, 22 },
            new double[] { 30, 31, 32 },
        });
        Near("anova grand mean", result.GrandMean, 21.0);
        Near("anova pooled sd", result.PooledSd, 1.0);
        Check("anova f significant", result.FPValue < 0.001);
    }

    private static void TestEp5()
    {
        var runs = new[]
        {
            new Ep5Run { Day = 1, Replicates = new double[] { 10, 10 } },
            new Ep5Run { Day = 1, Replicates = new double[] { 10.5, 10.5 } },
            new Ep5Run { Day = 2, Replicates = new double[] { 12, 12 } },
            new Ep5Run { Day = 2, Replicates = new double[] { 12.5, 12.5 } },
            new Ep5Run { Day = 3, Replicates = new double[] { 14, 14 } },
            new Ep5Run { Day = 3, Replicates = new double[] { 14.5, 14.5 } },
        };
        var result = EpComplexPrecision.Calculate(runs);
        Near("ep5 within sd", result.SdWithinRun, 0.0, 1e-12);
        Near("ep5 between-run sd", result.SdBetweenRun, Math.Sqrt(0.125), 1e-9);
        Near("ep5 between-day sd", result.SdBetweenDay, Math.Sqrt(3.9375), 1e-9);
        Near("ep5 total sd", result.SdTotal, Math.Sqrt(4.0625), 1e-9);
        Check("ep5 preliminary (3 days, 6 runs -> not preliminary)", !result.IsPreliminary);

        var (_, maxDiff, _) = EpComplexPrecision.DetectOutliers(runs);
        Near("ep5 max difference", maxDiff, 0.0, 1e-12);
    }

    private static void TestEp10()
    {
        var runs = Enumerable.Range(0, 5).Select(_ => new Ep10Run { Values = new double[] { 10, 10, 10 } }).ToArray();
        var level = Ep10.Level(runs, 10.0, cvLimit: 5.0, biasLimit: 0.5);
        Near("ep10 grand mean", level.GrandMean, 10.0);
        Near("ep10 grand sd", level.GrandSd, 0.0, 1e-12);
        Near("ep10 bias", level.Bias, 0.0, 1e-12);
        Check("ep10 passes", level.BiasPass && level.CvPass);
    }

    private static void TestLinearity()
    {
        var points = new[]
        {
            new LinearityPoint(1, 10, 0.1),
            new LinearityPoint(2, 20, 0.1),
            new LinearityPoint(3, 30, 0.1),
        };
        Check("wedge exactly linear at f=0", ClinicalLinearity.IsLinear(points, 0.0).IsLinear);
        var found = ClinicalLinearity.FindFactor(points, 0.001);
        Check("wedge find factor linear", found.IsLinear);
        Near("pct recovery", ClinicalLinearity.PercentRecovery(52, 50), 104.0, 1e-9);
        Near("sd interpolation", ClinicalLinearity.InterpolateControlsForSd(15, new (double, double)[] { (10, 1), (20, 3) }), 2.0, 1e-9);
        var band = ClinicalLinearity.Band(100, 10, 12);
        Near("band lower", band.Lower, 10.0);
        Near("band upper", band.Upper, 12.0);
    }

    private static void TestEp6()
    {
        var pooled = Ep6Linearity.PooledStatistics(new[]
        {
            new Ep6Level { Concentration = 10, Replicates = new double[] { 9, 10, 11 } },
            new Ep6Level { Concentration = 20, Replicates = new double[] { 19, 20, 21 } },
        });
        Near("ep6 pooled sd", pooled.PooledSd, 1.0, 1e-9);
        Near("ep6 pooled cv", pooled.PooledCv, 100.0 * Math.Sqrt(0.00625), 1e-9);
        Check("ep6 homogeneous", !pooled.Heterogeneous);
    }

    private static void TestLoq()
    {
        var materials = new[]
        {
            new LoqMaterial { Concentration = 1, Replicates = new double[] { 0.7, 1.0, 1.3 } },
            new LoqMaterial { Concentration = 5, Replicates = new double[] { 4.4, 5.0, 5.6 } },
            new LoqMaterial { Concentration = 10, Replicates = new double[] { 9.2, 10.0, 10.8 } },
        };
        var result = LimitOfQuantification.Find(materials, targetCv: 20.0, useUpperCi: false, degree: 1);
        Check("loq found", result.Found);
        Check("loq in range", result.Concentration > 1.0 && result.Concentration < 5.0);

        var (cvLo, cvHi) = LimitOfQuantification.CvConfidenceInterval(10.0, 20);
        Check("cv ci ordered", cvLo < 10.0 && cvHi > 10.0);
    }

    private static void TestRoc()
    {
        var auc = RocAnalysis.Compute(new double[] { 3, 4 }, new double[] { 1, 2 });
        Near("auc perfect", auc.Auc, 1.0, 1e-12);
        Near("auc variance", auc.Variance, 0.0, 1e-12);

        var tied = RocAnalysis.Compute(new double[] { 1, 2 }, new double[] { 2, 3 });
        Near("auc flipped", tied.Auc, 0.875, 1e-12);

        var matrix = new ConfusionMatrix { TruePositive = 40, FalsePositive = 10, FalseNegative = 5, TrueNegative = 45 };
        Near("sensitivity", matrix.Sensitivity, 40.0 / 45.0, 1e-12);
        Near("specificity", matrix.Specificity, 45.0 / 55.0, 1e-12);
        Near("efficiency", matrix.Efficiency, 85.0 / 100.0, 1e-12);
        Near("kappa", matrix.Kappa, 0.7, 1e-12);
        var (sLo, sHi) = matrix.SensitivityConfidenceInterval();
        Check("sens ci ordered", sLo < matrix.Sensitivity && matrix.Sensitivity < sHi);

        var self = RocAnalysis.Compare(auc, auc);
        Near("auc self-difference", self.Difference, 0.0, 1e-12);
        Near("auc self-se", self.DifferenceStandardError, 0.0, 1e-12);
    }

    private static void TestReferenceIntervals()
    {
        var values = new double[] { 1, 2, 3, 4, 5 };
        var param = ReferenceIntervals.Parametric(values);
        Near("ri mean lower", param.Lower, 3.0 - 1.959963984540054 * Math.Sqrt(2.5), 1e-9);
        Near("ri mean upper", param.Upper, 3.0 + 1.959963984540054 * Math.Sqrt(2.5), 1e-9);

        var verify = ReferenceIntervals.Verify(Enumerable.Range(1, 20).Select(x => (double)x).ToArray(), 1, 20);
        Near("percent outside", verify.PercentOutside, 0.0);
        Check("verify passes", verify.Passes && !verify.Preliminary);

        var partition = ReferenceIntervals.HarrisBoydPartition(
            new double[] { 1, 2, 3, 4, 5 }, new double[] { 6, 7, 8, 9, 10 });
        Check("harris-boyd partition", partition.Partition);
    }

    private static void TestInr()
    {
        Near("inr 1", Inr.Calculate(12, 12, 1.0), 1.0, 1e-12);
        Near("inr 2", Inr.Calculate(24, 12, 1.0), 2.0, 1e-12);
        Near("mnpt", Inr.GeometricMean(new double[] { 10, 10, 10 }), 10.0, 1e-12);
        Check("ck pass", Inr.CheckPasses(2.01, 2.0, 2));
        Check("ck fail", !Inr.CheckPasses(2.2, 2.0, 2));
        Check("inr passes <10%", Inr.Passes(5.0));
    }

    private static void TestTrueness()
    {
        var goals = Trueness.BiologicalVariation(5, 10, f: 1.0, z: 1.65);
        Near("cv analytical", goals.CvAnalytical, 1.25, 1e-12);
        Near("bias goal", goals.Bias, 0.125 * Math.Sqrt(125), 1e-12);
        Near("tea goal", goals.TotalError, 1.65 * 1.25 + 0.125 * Math.Sqrt(125), 1e-9);
        Near("sigma metric", Trueness.SigmaMetric(10, 2, 1), 8.0, 1e-12);
        Check("sigma class", Trueness.SigmaClassification(8) == "Excellent");
    }

    private static void TestQc()
    {
        var carry = QcEvaluations.Carryover(new double[] { 15 }, new double[] { 10, 10, 10 });
        Near("carryover", carry.Carryover, 5.0);
        Check("carryover fails", !carry.Passes);

        var (mean, _, _, _) = QcEvaluations.InterferencePairedDifference(
            new double[] { 11, 12, 13 }, new double[] { 10, 11, 12 });
        Near("interference effect", mean, 1.0);

        Check("ep26 error index passes", QcEvaluations.ErrorIndexPasses(new double[] { 0.5, -0.2, 0.9 }));
        Check("ep26 error index fails", !QcEvaluations.ErrorIndexPasses(new double[] { 1.2 }));
    }

    private static void TestCosts()
    {
        Near("labour", CostPerTest.LaborCost(60, 1.0), 1.0);
        Near("material", CostPerTest.MaterialCost(3, 2.5), 7.5);
        Near("weekly to monthly", CostPerTest.WeeklyToMonthly(120), 520.0);
        Near("annual to monthly", CostPerTest.AnnualToMonthly(1200), 100.0);
    }

    private static void Near(string name, double actual, double expected, double tolerance = 1e-9)
        => Check($"{name} = {expected:0.######} (got {actual:0.######})", Math.Abs(actual - expected) <= tolerance);

    private static void Check(string name, bool condition)
    {
        if (condition) { _passed++; Console.WriteLine($"  PASS  {name}"); }
        else { _failed++; Console.WriteLine($"  FAIL  {name}"); }
    }
}
