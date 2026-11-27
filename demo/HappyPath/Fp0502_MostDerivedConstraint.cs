namespace Demo.Fp0502_MostDerivedConstraint;

[Fprog.FunctionalCore]
public static class Tracking
{
    public static string Describe<T, Wide, Narrow>(T change)
        where T : Wide, Narrow where Wide : ShipmentEvent where Narrow : TransitEvent
    {
        switch (change)
        {
            case Arrived: return "arrived";
            case Departed: return "departed";
        }

        return "unknown";
    }
}
