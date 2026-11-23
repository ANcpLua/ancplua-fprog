namespace Demo.Fp0404_EmptyUnion;

[Fprog.FunctionalCore]
public static class Shelter
{
    public sealed record Cat;
    public sealed record Dog;
    public sealed record Bird;
    public union Pet(Cat, Dog, Bird);

    public static int FirstIntake() => Legs(default); // expect: FP0404
    public static int SecondIntake() => Legs(new Pet()); // expect: FP0404
    public static int ThirdIntake() => Legs(default(Pet)); // expect: FP0404

    public static int Legs(Pet pet) => pet switch
    {
        Cat => 4,
        Dog => 4,
        Bird => 2,
    };
}
