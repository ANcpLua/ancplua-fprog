# ANcpLua.Fprog.Analyzers

Roslyn analyzers that check functional-programming rules in C#. You mark the parts of your
code that must be pure, immutable and total, and the compiler reports every place that is not.
Code outside those parts is not checked.

## Install

```xml
<PackageReference Include="ANcpLua.Fprog.Analyzers" Version="0.1.0" PrivateAssets="all" />
```

The package adds the analyzers and compiles two marker attributes into your project as
`internal` types: `Fprog.FunctionalCoreAttribute` and `Fprog.UnionAttribute`. The analyzers
find the attributes by full name, so a copy in any assembly works. To declare your own copies
instead, add `FPROG_NO_ATTRIBUTES` to `DefineConstants`.

The analyzers need a C# compiler that ships Roslyn 5.9 or later (.NET 11 SDK).

## Mark the functional core

```csharp
[Fprog.FunctionalCore]
public static class Pricing
{
    public static decimal Total(ImmutableArray<decimal> lines, decimal rate) =>
        lines.Sum() * (1 + rate);
}
```

The core is every type marked `[Fprog.FunctionalCore]`, every type nested in it, and every
type in an assembly marked `[assembly: Fprog.FunctionalCore]`. Keep I/O, clocks, randomness
and `async` code in the imperative shell outside the core.

## Declare a union

```csharp
[Fprog.Union]
public abstract record Shape
{
    private Shape() { }

    public sealed record Circle(double Radius) : Shape;
    public sealed record Square(double Side) : Shape;
}

static double Area(Shape shape) => shape switch
{
    Shape.Circle c => Math.PI * c.Radius * c.Radius,
    Shape.Square s => s.Side * s.Side,
};
```

A union is an abstract class or record marked `[Fprog.Union]` with only private constructors.
Its cases are the sealed types nested in it that derive from it directly. A `switch`
expression that handles every case needs no catch-all arm: FPS001 suppresses the compiler's
CS8509 warning for it.

## Rules

All rules have category `Functional` and default severity `Warning`. Change a severity in
`.editorconfig`, for example `dotnet_diagnostic.FP0802.severity = none`.

Rules FP01xx to FP03xx and FP06xx to FP08xx apply only inside the core. Union rules apply to
every union. Compiler-generated members are not checked.

| ID | Enforces |
| --- | --- |
| FP0101 | Methods and local functions do not return `void` |
| FP0102 | No hidden inputs: `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow`, `Guid.NewGuid`, `TimeProvider.System`, `System.Environment`, `System.Random`, mutable static fields |
| FP0103 | No `ref` or `out` parameters (`in` and `ref readonly` are allowed) |
| FP0104 | No writes through a reference-type parameter (field, property or element assignment, `++`, `--`) |
| FP0201 | Fields are `readonly` or `const` |
| FP0202 | Properties have no `set` accessor (`init` is allowed) |
| FP0203 | Records and readonly structs hold no arrays, `StringBuilder` or mutable collections (`System.Collections.Immutable` and `.Frozen` are allowed) |
| FP0301 | No `throw` statements or expressions |
| FP0302 | No partial members: `Parse` on `System` types, `First/Last/Single/ElementAt`, dictionary indexer reads |
| FP0303 | No `null` or `default` returned for a reference or nullable return type |
| FP0401 | A union is abstract |
| FP0402 | A union declares only private instance constructors |
| FP0403 | A union case is sealed and nested in its union |
| FP0501 | A `switch` over a union has no catch-all (`_`, `var`, `default:`, `{ }`, `not null`, the union or a base type) |
| FP0502 | A `switch` expression over a union handles every case with an arm without `when` |
| FPS001 | Suppresses CS8509 on a `switch` expression over a union that handles every case |
| FP0601 | No type tests on an unconstrained type parameter (`is`, `as`, type patterns, `typeof(T)`, `GetType()`) |
| FP0701 | Closures do not capture a local or parameter that is assigned after its declaration |
| FP0801 | No I/O, network, data, console, process or thread primitives, no `lock` |
| FP0802 | Methods and local functions do not return `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>` |

The exact definition of each rule, including the span it reports, is the rule catalog in
[AGENTS.md](https://github.com/ANcpLua/ancplua-fprog/blob/main/AGENTS.md).

## Build from source

```bash
make tools   # restore the local tools (ReSharper CLI, RefTrace)
make check   # build, tests, InspectCode, RefTrace
make pack    # artifacts/package/ANcpLua.Fprog.Analyzers.<version>.nupkg
```

## License

MIT
