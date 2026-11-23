namespace Demo.Fp0104_ConditionalReceiver;

public sealed class Account
{
    public decimal Balance;
}

// Bug: one of the two accounts that the caller passed in is changed in place.
[Fprog.FunctionalCore]
public static class Booking
{
    public static decimal Credit(bool toFirst, Account first, Account second, decimal amount)
    {
        (toFirst ? first : second).Balance += amount; // expect: FP0104
        return first.Balance + second.Balance;
    }

    public static decimal CreditFallback(Account? preferred, Account fallback, decimal amount)
    {
        (preferred ?? fallback).Balance += amount; // expect: FP0104
        return fallback.Balance;
    }

    public static decimal CreditByIndex(int index, Account first, Account second, decimal amount)
    {
        (index switch { 0 => first, _ => second }).Balance = amount; // expect: FP0104
        return amount;
    }
}
