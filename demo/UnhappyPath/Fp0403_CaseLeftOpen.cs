namespace Demo.Fp0403_CaseLeftOpen;

[Fprog.FunctionalCore]
public static class Tracking
{
    public closed record Status;
    public record InTransit(int Days) : Status; // expect: FP0403
    public sealed record Delivered : Status;

    public static string Describe(Status status) => status switch
    {
        InTransit => "In transit",
        Delivered => "Delivered",
    };
}
