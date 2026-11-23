namespace Demo.Trusted
{
    public static class Price { public static int RoundToWhole(int value) => value; }
}

namespace Demo.Sibling
{
    public static class Price { public static int RoundToWhole(int value) => value; }
}

namespace Demo.Fp0801_TrustedLibrary
{
    [Fprog.FunctionalCore]
    public static class Report
    {
        public static int Apply(int amount) => Trusted.Price.RoundToWhole(amount);
    }
}
