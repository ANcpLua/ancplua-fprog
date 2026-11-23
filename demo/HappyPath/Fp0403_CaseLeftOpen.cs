namespace Demo.Fp0403_CaseLeftOpen;

[Fprog.FunctionalCore]
public static class Tracking
{
    public closed record Status;
    public sealed record InTransit(int Days) : Status;
    public sealed record Delivered : Status;

    public static string Describe(Status status) => status switch
    {
        InTransit => "In transit",
        Delivered => "Delivered",
    };
}
