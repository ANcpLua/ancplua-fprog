namespace Demo.Fp0502_MostDerivedConstraint;

[Fprog.FunctionalCore]
public static class Tracking
{
    public static string Describe<T, Wide, Narrow>(T change)
        where T : Wide, Narrow where Wide : ShipmentEvent where Narrow : TransitEvent
    {
        switch (change) // expect: FP0502
        {
            case Arrived: return "arrived";
        }

        return "unknown";
    }
}
