using System;

namespace Demo.Fp0102_GuidCreateVersion7;

// Bug: a version 7 GUID is built from the clock and random bits. Two calls with the same arguments give two
// different orders, so a retried request creates a second order.
[Fprog.FunctionalCore]
public static class Orders
{
    public static (Guid Id, string Sku) Create(string sku) => (Guid.CreateVersion7(), sku); // expect: FP0102

    public static Guid CreateAt(DateTimeOffset time) => Guid.CreateVersion7(time); // expect: FP0102

    public static Guid Classic() => Guid.NewGuid(); // expect: FP0102
}
