using System;

namespace Demo.Fp0105_EventSubscription;

[Fprog.FunctionalCore]
public sealed class Quotes
{
    private event Func<decimal, decimal>? Discount;

    public Quotes WithDiscount(Func<decimal, decimal> discount)
    {
        Discount += discount; // expect: FP0105
        return this;
    }

    public Quotes WithoutDiscount(Func<decimal, decimal> discount)
    {
        Discount -= discount; // expect: FP0105
        return this;
    }

    public decimal Quote(decimal total) => Discount?.Invoke(total) ?? total;
}
