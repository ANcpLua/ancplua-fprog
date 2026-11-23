namespace Demo.Fp0501_CatchAllBehindCast;

[Fprog.FunctionalCore]
public static class Intake
{
    public closed record Status;
    public sealed record Pending : Status;
    public sealed record Confirmed : Status;
    public sealed record Rejected : Status;

    public static string Label(Status status) => status switch
    {
        Pending => "Pending",
        Confirmed => "Confirmed",
        Rejected => "Rejected",
    };
}
