using System;

namespace Demo.Fp0102_GuidCreateVersion7;

// Correct: the shell creates the id once and passes it in; a retry with the same id gives the same order.
[Fprog.FunctionalCore]
public static class Orders
{
    public static (Guid Id, string Sku) Create(Guid id, string sku) => (id, sku);

    public static Guid None() => Guid.Empty;

    public static bool Same(Guid left, Guid right) => left == right;
}
