namespace Demo.KnownGaps.Fp0501_TupleUpcastAndNativeUnion;

[Fprog.Union]
public abstract record State
{
    private State() { }

    public sealed record Idle : State;

    public sealed record Running : State;

    public sealed record Done : State;
}

[Fprog.Union]
public abstract record Event
{
    private Event() { }

    public sealed record Start : Event;

    public sealed record Finish : Event;
}

public record Cat(string Name);

public record Dog(string Name);

public record Bird(string Name);

public union Pet(Cat, Dog, Bird);

// Not reported today: a catch-all over a tuple of unions, behind a cast, and over a C# 15 union.
[Fprog.FunctionalCore]
public static class Machine
{
    public static State Next(State state, Event input) => (state, input) switch
    {
        (State.Idle, Event.Start) => new State.Running(),
        (State.Running, Event.Finish) => new State.Done(),
        _ => state,                                  // Finish before Start is swallowed
    };

    public static int Kind(State state) => ((object)state) switch
    {
        State.Idle => 0,
        _ => 1,
    };

    public static int Legs(Pet pet) => pet switch
    {
        Cat => 4,
        _ => 2,
    };
}
