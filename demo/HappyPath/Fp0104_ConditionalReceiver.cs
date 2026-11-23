namespace Demo.Fp0104_ConditionalReceiver;

[Fprog.FunctionalCore]
public sealed record Account(decimal Balance);

// Correct: the chosen account is copied with the new balance; the caller's objects stay as they were.
[Fprog.FunctionalCore]
public static class Booking
{
    public static Account Credit(bool toFirst, Account first, Account second, decimal amount)
    {
        var chosen = toFirst ? first : second;
        return chosen with { Balance = chosen.Balance + amount };
    }

    public static Account CreditFallback(Account? preferred, Account fallback, decimal amount)
    {
        var chosen = preferred ?? fallback;
        return chosen with { Balance = chosen.Balance + amount };
    }
}
