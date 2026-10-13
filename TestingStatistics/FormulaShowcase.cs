using EPAnalyzer.Statistics.Coagulation;
using EPAnalyzer.Statistics.Costs;
using EPAnalyzer.Statistics.Descriptive;
using EPAnalyzer.Statistics.Detection;
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
/// Prints a worked example for every formula family in EPAnalyzer.Statistics,
/// so the numbers can be compared against the EP Evaluator reports.
/// </summary>
internal static class FormulaShowcase
{
    public static void RunAll()
    {
        Section("Descriptive statistics (dgrs1.TStatUnit / dgrs2.TStatUnitEx)");
        var unit = new StatUnit();
        foreach (double v in new[] { 21.0, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 31 })
            unit.Add(v);
        Console.WriteLine($"mean={unit.Mean:F4} sd={unit.StandardDeviation:F4} cv={unit.Cv:F2}% df={unit.N - 1}");
        Console.WriteLine($"sd 95% CI = [{unit.SdConfidenceInterval(0.05).Lower:F4}, {unit.SdConfidenceInterval(0.05).Upper:F4}]");

        Section("Simple precision");
        var sp = EpSimplePrecision.Calculate(
            new double[] { 21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 31 },
            new SimplePrecisionOptions { GoalMode = PrecisionGoalMode.TeaBased, AllowableTotalErrorPercent = 300, PercentRandomErrorBudget = 50 });
        Console.WriteLine($"mean={sp.Mean:F3} sd={sp.Sd:F3} cv={sp.Cv:F2}% goal={sp.SdGoal:F3} outcome={sp.Outcome}");

        Section("Complex precision / EP5 nested ANOVA");
        var runs = BuildEp5Design();
        var ep5 = EpComplexPrecision.Calculate(runs);
        Console.WriteLine($"within-run SD={ep5.SdWithinRun:F4} between-run SD={ep5.SdBetweenRun:F4} between-day SD={ep5.SdBetweenDay:F4}");
        Console.WriteLine($"total SD={ep5.SdTotal:F4} (sum of variances = {ep5.VarWithinRun + ep5.VarBetweenRun + ep5.VarBetweenDay:F4})");

        Section("EP10 grand statistics");
        var ep10Runs = Enumerable.Range(0, 5)
            .Select(i => new Ep10Run { Values = new[] { 100.0 + i * 0.1, 101 + i * 0.1, 99 + i * 0.1 } })
            .ToArray();
        var level = Ep10.Level(ep10Runs, assigned: 100.0, cvLimit: 2.0, biasLimit: 1.0);
        Console.WriteLine($"grand mean={level.GrandMean:F4} grand SD={level.GrandSd:F4} CV={level.GrandCv:F2}% bias={level.Bias:F4}");

        Section("Method comparison (EP9-A3): Deming, Passing-Bablok, bias");
        var mc = new MethodComparisonStats();
        var rng = new Random(42);
        for (int x = 10; x <= 100; x += 5)
        {
            double y = 1.05 * x + 2.0 + rng.NextDouble() * 2 - 1;
            mc.Add(x, y);
        }

        var deming = mc.Fit(RegressionModel.Deming, errorSdX: 1.0, errorSdY: 1.0);
        var pb = PassingBablok.Calculate(mc.Points);
        Console.WriteLine($"n={mc.N} r={mc.R:F4} bias={mc.Bias:F3} bias CI=[{mc.BiasConfidenceInterval().Lower:F3}, {mc.BiasConfidenceInterval().Upper:F3}]");
        Console.WriteLine($"Deming  y = {deming.Slope:F4} x + {deming.Intercept:F4}  (slope CI [{deming.SlopeCiLow:F4}, {deming.SlopeCiHigh:F4}])");
        Console.WriteLine($"PB      y = {pb.Slope:F4} x + {pb.Intercept:F4}  (slope CI [{pb.SlopeCiLow:F4}, {pb.SlopeCiHigh:F4}])");

        Section("EP6 linearity: pooled SD/CV, Bartlett, deviation from linearity");
        var levels = new[]
        {
            new Ep6Level { Concentration = 10, Replicates = new double[] { 9.6, 10.0, 10.4 } },
            new Ep6Level { Concentration = 20, Replicates = new double[] { 19.3, 20.0, 20.6 } },
            new Ep6Level { Concentration = 30, Replicates = new double[] { 28.8, 30.0, 30.9 } },
            new Ep6Level { Concentration = 40, Replicates = new double[] { 38.2, 40.0, 41.4 } },
            new Ep6Level { Concentration = 50, Replicates = new double[] { 47.9, 50.0, 51.8 } },
        };
        var pooled = Ep6Linearity.PooledStatistics(levels);
        Console.WriteLine($"pooled SD={pooled.PooledSd:F4} pooled CV={pooled.PooledCv:F3}% Bartlett p={pooled.BartlettPValue:F4} heterogeneous={pooled.Heterogeneous}");
        var best = Ep6Linearity.BestDegree(levels);
        Console.WriteLine($"best degree={best.Degree} lack-of-fit p={best.Result.FPValue:F4} R2={best.Result.RSquared:F5}");

        Section("Clinical linearity (wedge test)");
        var wedge = ClinicalLinearity.FindFactor(new[]
        {
            new LinearityPoint(10, 10.1, 0.2),
            new LinearityPoint(20, 19.9, 0.2),
            new LinearityPoint(30, 30.2, 0.2),
            new LinearityPoint(40, 39.8, 0.2),
        });
        Console.WriteLine($"smallest factor={wedge.Factor:E4} linear={wedge.IsLinear} intercept={wedge.Intercept:F3}");
        Console.WriteLine($"recovery 30.2/30 = {ClinicalLinearity.PercentRecovery(30.2, 30):F2}%");

        Section("Limit of quantification (CV fit crossing)");
        var loq = LimitOfQuantification.Find(new[]
        {
            new LoqMaterial { Concentration = 1, Replicates = new double[] { 0.7, 1.0, 1.3 } },
            new LoqMaterial { Concentration = 5, Replicates = new double[] { 4.4, 5.0, 5.6 } },
            new LoqMaterial { Concentration = 10, Replicates = new double[] { 9.2, 10.0, 10.8 } },
        }, targetCv: 20.0, useUpperCi: false);
        Console.WriteLine($"LOQ={loq.Concentration:F3} fitted CV={loq.FittedCv:F2}% found={loq.Found}");

        Section("Limit of blank");
        var lob = LimitOfBlank.Calculate(new double[] { 0.1, 0.2, 0.05, 0.15, 0.12, -0.03, 0.08, 0.11, 0.02, 0.09 });
        Console.WriteLine($"LOB(1.645)={lob.Lob:F4} mean={lob.Mean:F4} sd={lob.Sd:F4}  LOB(2SD)={LimitOfBlank.Calculate2Sd(new double[] { 0.1, 0.2, 0.05, 0.15, 0.12, -0.03, 0.08, 0.11, 0.02, 0.09 }):F4}");

        Section("ROC / DeLong");
        var roc = RocAnalysis.Compute(new double[] { 6, 7, 5, 8, 9, 7 }, new double[] { 2, 3, 4, 5, 4, 3 });
        Console.WriteLine($"AUC={roc.Auc:F4} SE={roc.StandardError:F4} 95% CI=[{roc.ConfidenceInterval().Lower:F4}, {roc.ConfidenceInterval().Upper:F4}]");
        var roc2 = RocAnalysis.Compute(new double[] { 5, 6, 5, 7, 8, 6 }, new double[] { 2, 3, 4, 5, 4, 3 });
        var cmp = RocAnalysis.Compare(roc, roc2);
        Console.WriteLine($"AUC diff={cmp.Difference:F4} r={cmp.Correlation:F4} z={cmp.ZStatistic:F4} p2={cmp.PTwoSided:F4}");

        Section("Reference intervals");
        var riValues = Enumerable.Range(0, 120).Select(i => 50 + 12 * MathNet.Numerics.Distributions.Normal.InvCDF(0, 1, (i + 0.5) / 120)).ToArray();
        var nonParam = ReferenceIntervals.NonParametric(riValues);
        var parametric = ReferenceIntervals.Parametric(riValues);
        Console.WriteLine($"non-param RI = [{nonParam.Lower:F2}, {nonParam.Upper:F2}]  CI ratio={nonParam.ConfidenceRatio:F3}");
        Console.WriteLine($"parametric RI = [{parametric.Lower:F2}, {parametric.Upper:F2}]  CI ratio={parametric.ConfidenceRatio:F3}");

        Section("INR");
        Console.WriteLine($"INR(24 s, MNPT 12 s, ISI 1.0) = {Inr.Calculate(24, 12, 1.0):F3}");
        Console.WriteLine($"MNPT = {Inr.GeometricMean(new double[] { 11.8, 12.1, 12.0, 11.9 }):F4}");

        Section("Trueness, Ricos goals, sigma");
        var goals = Trueness.BiologicalVariation(cvi: 4.0, cvg: 8.0, f: 2.0, z: 1.65);
        Console.WriteLine($"CVa={goals.CvAnalytical:F3}% Ba={goals.Bias:F3}% TEa={goals.TotalError:F3}% ({goals.Level})");
        Console.WriteLine($"sigma = {Trueness.SigmaMetric(10, 1, 1):F2} -> {Trueness.SigmaClassification(Trueness.SigmaMetric(10, 1, 1))}");

        Section("Carryover and stability");
        var carry = QcEvaluations.Carryover(new double[] { 10.8, 11.0, 10.9 }, new double[] { 5.0, 5.1, 4.9 });
        Console.WriteLine($"carryover={carry.Carryover:F3} limit={carry.Limit:F3} passes={carry.Passes}");
        var stability = QcEvaluations.Stability(new (double, double)[] { (0, 100), (7, 98), (14, 96), (21, 94) }, allowableDrift: -5);
        Console.WriteLine($"slope={stability.Slope:F4}/day significant={stability.SlopeSignificant} crossing={stability.CrossingTime:F2} days");

        Section("Cost per test");
        Console.WriteLine($"labour 300 s @ 0.80/min = {CostPerTest.LaborCost(300, 0.80):F2}");
        Console.WriteLine($"materials 3 x 2.50 = {CostPerTest.MaterialCost(3, 2.5):F2}; weekly 120 -> monthly {CostPerTest.WeeklyToMonthly(120):F2}");
    }

    private static IReadOnlyList<Ep5Run> BuildEp5Design()
    {
        var rng = new Random(7);
        var runs = new List<Ep5Run>();
        for (int day = 1; day <= 20; day++)
        {
            for (int run = 1; run <= 2; run++)
            {
                double level = 100 + (day % 5) * 0.5 + (run - 1) * 0.3 + rng.NextDouble() * 0.2;
                runs.Add(new Ep5Run { Day = day, Replicates = new[] { level, level + rng.NextDouble() * 0.3 } });
            }
        }
        return runs;
    }

    private static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {title} ---");
    }
}
