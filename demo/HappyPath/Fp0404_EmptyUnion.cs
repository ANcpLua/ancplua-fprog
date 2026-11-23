namespace Demo.Fp0404_EmptyUnion;

[Fprog.FunctionalCore]
public static class Shelter
{
    public sealed record Cat;
    public sealed record Dog;
    public sealed record Bird;
    public union Pet(Cat, Dog, Bird);

    public static int FirstIntake() => Legs(new Cat());
    public static int SecondIntake() => Legs(new Pet(new Dog()));
    public static int ThirdIntake() => Legs(new Bird());

    public static int Legs(Pet pet) => pet switch
    {
        Cat => 4,
        Dog => 4,
        Bird => 2,
    };
}
