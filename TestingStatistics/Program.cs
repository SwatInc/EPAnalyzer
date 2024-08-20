using Dumpify;
using MathNet.Numerics.Statistics;

namespace TestingStatistics;

internal class Program
{
    static void Main(string[] args)
    {
        var simplePrecision = new SimplePrecision();
        //simplePrecision.RunTests();

        var complex = new ComplexPrecision();
        complex.CalculateVerificationValue();

    }
}