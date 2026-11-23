namespace Demo.Fp0501_NativeUnionCatchAll;

[Fprog.FunctionalCore]
public static class Shelter
{
    public sealed record Cat;
    public sealed record Dog;
    public sealed record Bird;
    public union Pet(Cat, Dog, Bird);

    public static int Legs(Pet pet) => pet switch
    {
        Cat => 4,
        _ => 2, // expect: FP0501
    };
}
