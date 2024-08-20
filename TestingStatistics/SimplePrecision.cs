using Dumpify;
using MathNet.Numerics.Statistics;

namespace TestingStatistics;

public class SimplePrecision
{
    public void RunTests()
    {
        var simplePrecisionTest = new TestSimplePrecision_AllowableTotalErrorBased();
        simplePrecisionTest.ExecuteWithPercent();
        simplePrecisionTest.DumpConsole();

        Console.WriteLine("Please press any key to do simple precision with concentration");
        Console.ReadLine();
        Console.Clear();

        simplePrecisionTest = new TestSimplePrecision_AllowableTotalErrorBased();
        simplePrecisionTest.ExecuteWithConcentration();
        simplePrecisionTest.DumpConsole();

        Console.WriteLine("Please press any key to do simple precision with Vendor SD based");
        Console.ReadLine();
        Console.Clear();

        var simplePrecisionWithVendorSD = new TestSimplePrecision_VendorStandardDeviationBased();
        simplePrecisionWithVendorSD.Execute();
        simplePrecisionWithVendorSD.DumpConsole();

        Console.WriteLine("Please press any key to do simple precision with Vendor REa based - CONCENTRATION");
        Console.ReadLine();
        Console.Clear();

        var simplePrecisionWithVendorREa = new TestSimplePrecision_VendorRandomErrorAllowableBased();
        simplePrecisionWithVendorREa.ExecuteWithConcentration();
        simplePrecisionWithVendorREa.DumpConsole();

        Console.WriteLine("Please press any key to do simple precision with Vendor REa based - PERCENT");
        Console.ReadLine();
        Console.Clear();

        var simplePrecisionWithVendorREaPct = new TestSimplePrecision_VendorRandomErrorAllowableBased();
        simplePrecisionWithVendorREaPct.ExecuteWithPercent();
        simplePrecisionWithVendorREaPct.DumpConsole();

        Console.ReadLine();
    }
}

public class TestSimplePrecision_AllowableTotalErrorBased
{
    public void ExecuteWithPercent()
    {
        //USER INPUT
        Measurements = [21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 31];
        Units = "mg/dL";
        Analyst = "Ibrahim";
        AnalysisDate = DateOnly.FromDateTime(DateTime.Today);
        MaxDecimal = 2;
        TargetMean = 19;
        AllowableTotalErrorPercent = 300;
        PercentRandomErrorBudget = 50;

        SampleObservedMean = Measurements.Mean();
        SampleObservedSD = Measurements.StandardDeviation();
        ObservedCV = (SampleObservedSD / SampleObservedMean) * 100f;
        DegressOfFreedom = Measurements.Count - 1;
        AllowableRandomError = (PercentRandomErrorBudget / 100) * AllowableTotalErrorPercent;
        StandardDeviationGoal = (AllowableRandomError / 100) * SampleObservedMean;
        TargetCV = (StandardDeviationGoal / SampleObservedMean) * 100f;

        // Calculate 95% CI for the SD


        ConfidenceInterval = CalculateNintyFivePercentConfidenceInterval(SampleObservedSD, DegressOfFreedom);
        Comment = "TEa Based - With TEa %";


    }

    public void ExecuteWithConcentration()
    {
        //USER INPUT
        Measurements = [21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 31];
        Units = "mg/dL";
        Analyst = "Ibrahim";
        AnalysisDate = DateOnly.FromDateTime(DateTime.Today);
        MaxDecimal = 2;
        TargetMean = 19;
        AllowableTotalErrorConcentration = 13.5;
        PercentRandomErrorBudget = 50;

        GoalMode = PrecisionVerificationGoalMode.TEaBased;
        SampleObservedMean = Measurements.Mean();
        SampleObservedSD = Measurements.StandardDeviation();
        ObservedCV = (SampleObservedSD / SampleObservedMean) * 100f;
        DegressOfFreedom = Measurements.Count - 1;
        AllowableRandomError = (PercentRandomErrorBudget / 100) * AllowableTotalErrorConcentration;
        StandardDeviationGoal = AllowableRandomError;
        TargetCV = (StandardDeviationGoal / SampleObservedMean) * 100;

        // Calculate 95% CI for the SD


        ConfidenceInterval = CalculateNintyFivePercentConfidenceInterval(SampleObservedSD, DegressOfFreedom);
        Comment = "TEa Based - With TEa CONCENTRATION";
    }

    #region USER INPUTS

    //data for simple precision calculation
    public List<float> Measurements;
    public string Units { get; set; }
    public string Analyst { get; set; }
    public DateOnly AnalysisDate { get; set; }
    public int MaxDecimal { get; set; }

    #region Allowable Error Criteria
    public PrecisionVerificationGoalMode GoalMode { get; set; }
    public double AllowableTotalErrorConcentration { get; set; }
    public double AllowableTotalErrorPercent { get; set; }

    /// <summary>
    /// % for Random Error
    /// </summary>
    public double PercentRandomErrorBudget { get; set; }

    public Control? Control { get; set; }
    public Reagent? Reagent { get; set; }
    public Calibrator? Calibrator { get; set; }
    public string? Comment { get; set; }

    #endregion

    #region Histogram Result Distribution
    public decimal? TargetMean { get; set; }
    public bool ShowHistogram { get; set; }
    public bool ShowTargetRange { get; set; }
    public TargetRange TargetRange { get; set; }

    #endregion

    #endregion

    #region CALCULATED
    public double SampleObservedMean { get; private set; }
    public double SampleObservedSD { get; private set; }
    public NintyFivePercentConfidenceInterval ConfidenceInterval { get; set; }
    public double ObservedCV { get; private set; }
    public double TargetCV { get; private set; }
    public int DegressOfFreedom { get; private set; }
    public double StandardDeviationGoal { get; set; }
    public double AllowableRandomError { get; set; }

    #endregion

    public static NintyFivePercentConfidenceInterval CalculateNintyFivePercentConfidenceInterval(double standardDeviation, int degressOfFreedom)
    {
        double alpha = 0.05;
        double chiSquareLower = MathNet.Numerics.Distributions.ChiSquared.InvCDF(degressOfFreedom, alpha / 2);
        double chiSquareUpper = MathNet.Numerics.Distributions.ChiSquared.InvCDF(degressOfFreedom, 1 - alpha / 2);

        double lowerBound = Math.Sqrt(degressOfFreedom * Math.Pow(standardDeviation, 2) / chiSquareUpper);
        double upperBound = Math.Sqrt(degressOfFreedom * Math.Pow(standardDeviation, 2) / chiSquareLower);

        return new NintyFivePercentConfidenceInterval(upperBound, lowerBound);
    }

}

public class TestSimplePrecision_VendorStandardDeviationBased
{
    public void Execute()
    {
        //USER INPUT
        Measurements = [21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 31];
        Units = "mg/dL";
        Analyst = "Ibrahim";
        AnalysisDate = DateOnly.FromDateTime(DateTime.Today);
        MaxDecimal = 2;
        TargetMean = 19;
        VendorSD = 5.4;

        GoalMode = PrecisionVerificationGoalMode.VendorSDBased;

        SampleObservedMean = Measurements.Mean();
        SampleObservedSD = Measurements.StandardDeviation();
        ObservedCV = (SampleObservedSD / SampleObservedMean) * 100f;
        DegressOfFreedom = Measurements.Count - 1;

        StandardDeviationGoal = VendorSD; //SD Goal is the Vendor SD
        TargetCV = (StandardDeviationGoal / SampleObservedMean) * 100f;

        // Calculate 95% CI for the SD


        ConfidenceInterval = CalculateNintyFivePercentConfidenceInterval(SampleObservedSD, DegressOfFreedom);
        Comment = "Vendor SD Based";


    }

    #region USER INPUTS

    //data for simple precision calculation
    public List<float> Measurements;
    public string Units { get; set; }
    public string Analyst { get; set; }
    public DateOnly AnalysisDate { get; set; }
    public int MaxDecimal { get; set; }

    #region Allowable Error Criteria

    public PrecisionVerificationGoalMode GoalMode { get; set; }
    public double AllowableTotalErrorConcentration { get; set; }
    public double AllowableTotalErrorPercent { get; set; }

    /// <summary>
    /// % for Random Error
    /// </summary>
    public double PercentRandomErrorBudget { get; set; }

    public double VendorSD { get; set; }

    public Control? Control { get; set; }
    public Reagent? Reagent { get; set; }
    public Calibrator? Calibrator { get; set; }
    public string? Comment { get; set; }

    #endregion

    #region Histogram Result Distribution
    public decimal? TargetMean { get; set; }
    public bool ShowHistogram { get; set; }
    public bool ShowTargetRange { get; set; }
    public TargetRange TargetRange { get; set; }

    #endregion

    #endregion

    #region CALCULATED
    public double SampleObservedMean { get; private set; }
    public double SampleObservedSD { get; private set; }
    public NintyFivePercentConfidenceInterval ConfidenceInterval { get; set; }
    public double ObservedCV { get; private set; }
    public double TargetCV { get; private set; }
    public int DegressOfFreedom { get; private set; }
    public double StandardDeviationGoal { get; set; }
    public double AllowableRandomError { get; set; }

    #endregion

    public static NintyFivePercentConfidenceInterval CalculateNintyFivePercentConfidenceInterval(double standardDeviation, int degressOfFreedom)
    {
        double alpha = 0.05;
        double chiSquareLower = MathNet.Numerics.Distributions.ChiSquared.InvCDF(degressOfFreedom, alpha / 2);
        double chiSquareUpper = MathNet.Numerics.Distributions.ChiSquared.InvCDF(degressOfFreedom, 1 - alpha / 2);

        double lowerBound = Math.Sqrt(degressOfFreedom * Math.Pow(standardDeviation, 2) / chiSquareUpper);
        double upperBound = Math.Sqrt(degressOfFreedom * Math.Pow(standardDeviation, 2) / chiSquareLower);

        return new NintyFivePercentConfidenceInterval(upperBound, lowerBound);
    }

}

public class TestSimplePrecision_VendorRandomErrorAllowableBased
{
    public void ExecuteWithConcentration()
    {
        //USER INPUT
        Measurements = [21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 31];
        Units = "mg/dL";
        Analyst = "Ibrahim";
        AnalysisDate = DateOnly.FromDateTime(DateTime.Today);
        MaxDecimal = 2;
        TargetMean = 19;
        WithinRunConcentration = 50;
        GoalMode = PrecisionVerificationGoalMode.VendorREaBased;


        SampleObservedMean = Measurements.Mean();
        SampleObservedSD = Measurements.StandardDeviation();
        ObservedCV = (SampleObservedSD / SampleObservedMean) * 100f;
        DegressOfFreedom = Measurements.Count - 1;

        StandardDeviationGoal = WithinRunConcentration;
        TargetCV = (StandardDeviationGoal / SampleObservedMean) * 100f;

        // Calculate 95% CI for the SD


        ConfidenceInterval = CalculateNintyFivePercentConfidenceInterval(SampleObservedSD, DegressOfFreedom);
        Comment = "Vendor REa (Random Error Allowable) Based - Within Run Concentration";
    }

    public void ExecuteWithPercent()
    {
        //USER INPUT
        Measurements = [21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 21, 19, 22, 20, 18, 20, 15, 20, 31];
        Units = "mg/dL";
        Analyst = "Ibrahim";
        AnalysisDate = DateOnly.FromDateTime(DateTime.Today);
        MaxDecimal = 2;
        TargetMean = 19;
        WithinRunPercent = 50;

        GoalMode = PrecisionVerificationGoalMode.VendorREaBased;

        SampleObservedMean = Measurements.Mean();
        SampleObservedSD = Measurements.StandardDeviation();
        ObservedCV = (SampleObservedSD / SampleObservedMean) * 100f;
        DegressOfFreedom = Measurements.Count - 1;

        StandardDeviationGoal = (WithinRunPercent / 100f) * SampleObservedMean;
        TargetCV = (StandardDeviationGoal / SampleObservedMean) * 100f;

        // Calculate 95% CI for the SD


        ConfidenceInterval = CalculateNintyFivePercentConfidenceInterval(SampleObservedSD, DegressOfFreedom);
        Comment = "Vendor REa (Random Error Allowable) Based - PERCENT";


    }

    #region USER INPUTS

    //data for simple precision calculation
    public List<float> Measurements;
    public string Units { get; set; }
    public string Analyst { get; set; }
    public DateOnly AnalysisDate { get; set; }
    public int MaxDecimal { get; set; }

    #region Allowable Error Criteria
    public PrecisionVerificationGoalMode GoalMode { get; set; }
    public double AllowableTotalErrorConcentration { get; set; }
    public double AllowableTotalErrorPercent { get; set; }

    /// <summary>
    /// % for Random Error
    /// </summary>
    public double PercentRandomErrorBudget { get; set; }

    public double VendorSD { get; set; }

    public Control? Control { get; set; }
    public Reagent? Reagent { get; set; }
    public Calibrator? Calibrator { get; set; }
    public string? Comment { get; set; }

    #endregion

    #region Histogram Result Distribution
    public decimal? TargetMean { get; set; }
    public bool ShowHistogram { get; set; }
    public bool ShowTargetRange { get; set; }
    public TargetRange TargetRange { get; set; }

    #endregion

    #region Vendor REa Precision Goal
    public double WithinRunConcentration { get; set; }
    public double WithinRunPercent { get; set; }

    #endregion

    #endregion

    #region CALCULATED
    public double SampleObservedMean { get; private set; }
    public double SampleObservedSD { get; private set; }
    public NintyFivePercentConfidenceInterval ConfidenceInterval { get; set; }
    public double ObservedCV { get; private set; }
    public double TargetCV { get; private set; }
    public int DegressOfFreedom { get; private set; }
    public double StandardDeviationGoal { get; set; }
    public double AllowableRandomError { get; set; }

    #endregion

    public static NintyFivePercentConfidenceInterval CalculateNintyFivePercentConfidenceInterval(double standardDeviation, int degressOfFreedom)
    {
        double alpha = 0.05;
        double chiSquareLower = MathNet.Numerics.Distributions.ChiSquared.InvCDF(degressOfFreedom, alpha / 2);
        double chiSquareUpper = MathNet.Numerics.Distributions.ChiSquared.InvCDF(degressOfFreedom, 1 - alpha / 2);

        double lowerBound = Math.Sqrt(degressOfFreedom * Math.Pow(standardDeviation, 2) / chiSquareUpper);
        double upperBound = Math.Sqrt(degressOfFreedom * Math.Pow(standardDeviation, 2) / chiSquareLower);

        return new NintyFivePercentConfidenceInterval(upperBound, lowerBound);
    }

}

public record Control(string LotNumber, string Source, DateOnly ExpirationDate);

public record Reagent(string LotNumber, string Source, DateOnly ExpirationDate);

public record Calibrator(string LotNumber, string Source, DateOnly ExpirationDate);

public enum TargetRange
{
    TwoSD, TwoPointFiveSD, ThreeSD, ThreePointFiveSD
}

public enum PrecisionVerificationGoalMode
{
    None, TEaBased, VendorSDBased, VendorREaBased
}

public record NintyFivePercentConfidenceInterval(double UpperBound, double LowerBound);
