namespace Demo.Fp0501_CatchAllInTuple;

[Fprog.FunctionalCore]
public static class Machine
{
    public closed record State;
    public sealed record Idle : State;
    public sealed record Running : State;
    public sealed record Done : State;

    public closed record Event;
    public sealed record Start : Event;
    public sealed record Finish : Event;

    public closed record Outcome;
    public sealed record Accepted(State State) : Outcome;
    public sealed record InvalidTransition : Outcome;

    public static Outcome Next(State state, Event input) => (state, input) switch
    {
        (Idle, Start) => new Accepted(new Running()),
        (Running, Finish) => new Accepted(new Done()),
        _ => new Accepted(state), // expect: FP0501
    };
}
