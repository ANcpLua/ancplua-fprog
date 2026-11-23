using System;

namespace Demo.Fp0105_EventSubscription;

[Fprog.FunctionalCore]
public sealed class Quotes(Func<decimal, decimal> discount)
{
    public Quotes WithDiscount(Func<decimal, decimal> apply) => new(apply);

    public Quotes WithoutDiscount() => new(total => total);

    public decimal Quote(decimal total) => discount(total);
}
