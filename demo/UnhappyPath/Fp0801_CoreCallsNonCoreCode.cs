namespace Demo.Fp0801_CoreCallsNonCoreCode;

public static class Database
{
    public static int Save(string order)
    {
        System.IO.File.AppendAllText("orders.txt", order);
        return 1;
    }
}

public static class Sequence
{
    public static int Counter { get; set; }
}

[Fprog.FunctionalCore]
public static class Orders
{
    public static int Place(string order) => Database.Save(order); // expect: FP0801
    public static int Next() => Sequence.Counter + 1; // expect: FP0801
}
