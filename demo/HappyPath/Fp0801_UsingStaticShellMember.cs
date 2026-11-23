using static System.Math;

namespace Demo.Fp0801_UsingStaticShellMember;

// Correct: a static import of pure functions, and the text arrives as a value.
[Fprog.FunctionalCore]
public static class Report
{
    public static decimal Net(decimal gross) => Round(gross / 1.2m, 2);

    public static int Length(string text) => Abs(text.Length);

    public static double Diagonal(double a, double b) => Sqrt(a * a + b * b);
}
