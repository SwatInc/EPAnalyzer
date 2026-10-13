namespace EPAnalyzer.Statistics.Costs;

/// <summary>
/// Cost-per-test arithmetic (port of the <c>CPTDAT</c> database calculated-field
/// formulas): labour time x rate, materials quantity x unit cost, equipment and
/// indirect allocation, and the weekly/monthly/annual amortisation factors.
/// </summary>
public static class CostPerTest
{
    /// <summary>Labour cost = (seconds/60) * (wage + benefit) rate.</summary>
    public static double LaborCost(double seconds, double wagePerMinute, double benefitRate = 1.0)
        => seconds / 60.0 * wagePerMinute * benefitRate;

    /// <summary>Labour cost with separate wage and benefit percentages.</summary>
    public static double LaborCostWithBenefits(double seconds, double hourlyWage, double benefitPercent)
        => seconds / 3600.0 * hourlyWage * (1.0 + benefitPercent / 100.0);

    /// <summary>Material cost = quantity * unit cost.</summary>
    public static double MaterialCost(double quantity, double unitCost) => quantity * unitCost;

    /// <summary>Weekly cost converted to a monthly cost (x 52/12).</summary>
    public static double WeeklyToMonthly(double weeklyCost) => weeklyCost * 52.0 / 12.0;

    /// <summary>Monthly cost converted to an annual cost (x 12).</summary>
    public static double MonthlyToAnnual(double monthlyCost) => monthlyCost * 12.0;

    /// <summary>Annual cost converted to a monthly cost (/12).</summary>
    public static double AnnualToMonthly(double annualCost) => annualCost / 12.0;

    /// <summary>Equipment cost allocated per test = annual cost / annual test volume.</summary>
    public static double EquipmentCostPerTest(double annualEquipmentCost, double annualTests)
        => annualTests > 0 ? annualEquipmentCost / annualTests : double.NaN;

    /// <summary>Indirect (overhead) cost allocation by direct-cost share.</summary>
    public static double AllocateIndirect(double indirectTotal, double directCost, double totalDirectCost)
        => totalDirectCost > 0 ? indirectTotal * directCost / totalDirectCost : double.NaN;

    /// <summary>Total cost per test = labour + materials + equipment + indirect.</summary>
    public static double TotalCostPerTest(double labor, double materials, double equipment, double indirect)
        => labor + materials + equipment + indirect;

    /// <summary>Revenue per test = price * (1 - discount/100).</summary>
    public static double NetRevenuePerTest(double price, double discountPercent)
        => price * (1.0 - discountPercent / 100.0);

    /// <summary>Cost-to-charge ratio.</summary>
    public static double CostToChargeRatio(double cost, double charge) => charge > 0 ? cost / charge : double.NaN;

    /// <summary>Break-even annual volume = fixed cost / (revenue per test - variable cost per test).</summary>
    public static double BreakEvenVolume(double fixedCost, double revenuePerTest, double variableCostPerTest)
    {
        double contribution = revenuePerTest - variableCostPerTest;
        return contribution > 0 ? fixedCost / contribution : double.NaN;
    }
}
