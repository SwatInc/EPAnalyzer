using MathNet.Numerics.Distributions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestingStatistics;

internal class ComplexPrecision
{
    public void CalculateVerificationValue()
    {
        var claim_sd = 2.5f;
        var df = 40;
        var ci1 = CalculateNintyFivePercentVerificationValue(claim_sd, df); // 95%
        var ci2 = CalculateNintyNinePercentVerificationValue(claim_sd, df); // 99%

        Console.WriteLine(ci1);
        Console.WriteLine(ci2);
    }

    public static double CalculateNintyFivePercentVerificationValue(double standardDeviation, int degressOfFreedom)
    {
        double chiSquareCriticalValue = ChiSquared.InvCDF(degressOfFreedom, 0.95);
        double upperLimit = standardDeviation * Math.Sqrt(chiSquareCriticalValue / degressOfFreedom);
        return upperLimit;
    }

    public static double CalculateNintyNinePercentVerificationValue(double standardDeviation, int degressOfFreedom)
    {
        double chiSquareCriticalValue = ChiSquared.InvCDF(degressOfFreedom, 0.99);
        double upperLimit = standardDeviation * Math.Sqrt(chiSquareCriticalValue / degressOfFreedom);
        return upperLimit;
    }
}
