namespace Demo.Fp0801_CoreCallsNonCoreCode;

[Fprog.FunctionalCore]
public static class Orders
{
    public sealed record SaveOrder(int Number, string Description);

    public static SaveOrder Place(string order, int currentNumber) => new(currentNumber + 1, order);
}

public static class Database
{
    public static int Save(string order, int currentNumber)
    {
        var command = Orders.Place(order, currentNumber);
        System.IO.File.AppendAllText("orders.txt", command.Description);
        return command.Number;
    }
}
