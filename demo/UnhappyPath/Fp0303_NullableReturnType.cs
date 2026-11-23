namespace Demo.Fp0303_NullableReturnType;

[Fprog.FunctionalCore]
public static class Finder
{
    private const string? Nothing = null;

    public static string? ViaLocal(bool found) // expect: FP0303
    {
        string? result = null;
        if (found) result = "x";
        return result;
    }

    public static string? ViaConstant() => Nothing; // expect: FP0303

    public static (string? Name, int Age) InTuple() => (null, 0); // expect: FP0303

    public static int ForcedLength(string? name) => name!.Length; // expect: FP0303
}
