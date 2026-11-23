# ANcpLua.Fprog.Analyzers

Roslyn analyzers that hold C# 15 code to functional-programming rules. You mark the part of your
code that has to be pure, immutable and total, and the compiler reports every place that is not.
Unions are checked everywhere; the rest of your code stays as it is.

## Install

```xml
<PackageReference Include="ANcpLua.Fprog.Analyzers" Version="0.1.0" PrivateAssets="all" />
```

The package needs the .NET 11 SDK (11.0.100 or later), whose compiler is Roslyn 5.12. It adds the
analyzers and compiles one marker attribute into your project as an `internal` type:
`Fprog.FunctionalCoreAttribute`. The analyzers find the attribute by its full name, so a copy in
any assembly works. To declare your own copy, add `FPROG_NO_ATTRIBUTES` to `DefineConstants`.

## Mark the functional core

```csharp
[Fprog.FunctionalCore]
public static class Pricing
{
    public static decimal Total(ImmutableArray<decimal> lines, decimal rate) =>
        lines.Sum() * (1 + rate);
}
```

The core is every type marked `[Fprog.FunctionalCore]`, every type nested in it, and every type
of an assembly marked `[assembly: Fprog.FunctionalCore]`. I/O, clocks, randomness and `async`
work belong to the shell around it: the shell does the work and hands plain values to the core.

## Unions

A union is a C# 15 `union` type or a `closed` class or record. Its cases are known to the
compiler, so a `switch` expression that names every case needs no catch-all arm.

```csharp
public closed record Shape;
public sealed record Circle(double Radius) : Shape;
public sealed record Square(double Side) : Shape;

public static class Geometry
{
    public static double Area(Shape shape) => shape switch
    {
        Circle c => Math.PI * c.Radius * c.Radius,
        Square s => s.Side * s.Side,
    };
}
```

The union rules keep it that way: every case is named, no arm swallows the cases added later,
and no empty union value is created.

## Rules

All rules have category `Functional` and severity `Warning`. Change a severity in
`.editorconfig`, for example `dotnet_diagnostic.FP0802.severity = none`.

| ID | Inside the core |
| --- | --- |
| FP0101 | Every method, operator, local function and delegate returns a value instead of `void` |
| FP0102 | Clocks, time zones, new GUIDs, the current culture, hash codes and mutable static state arrive as parameters |
| FP0103 | Parameters are passed by value, `in` or `ref readonly`; results are returned instead of `ref` or `out` |
| FP0104 | Memory the caller owns is left unchanged; a method returns a new value |
| FP0105 | State held in fields stays as constructed |
| FP0106 | Formatting, parsing, comparing and sorting name their culture or comparer |
| FP0107 | Code is free of pointers and `unsafe` |
| FP0201 | Fields are `readonly` or `const` |
| FP0202 | Properties are get-only or `init` |
| FP0203 | Records and readonly structs hold immutable collections |
| FP0301 | Failures are returned as values instead of thrown |
| FP0302 | Operations are total: `TryParse`, `FirstOrDefault`, `TryGetValue`, guarded indexing and division |
| FP0303 | A missing result is an explicit value instead of `null`, `default` or `!` |
| FP0401 | Classes and records are `sealed`, `static` or `closed` |
| FP0601 | Values are used through their static type, without runtime type tests or `dynamic` |
| FP0701 | Closures capture variables that are assigned once |
| FP0801 | Shell types (I/O, network, processes, threads, third-party libraries) and `lock` stay outside |
| FP0802 | Asynchronous types and `async` stay outside |

| ID | On every union |
| --- | --- |
| FP0403 | Each direct subtype of a closed class is `sealed` or `closed` |
| FP0404 | A union struct is created from a case, never as `default` or `new U()` |
| FP0501 | A `switch` names each case; it has no catch-all |
| FP0502 | A `switch` statement handles every case |
| FP0503 | A case is picked in a `switch`, not with `is` or `as` |

| ID | Outside the core |
| --- | --- |
| FP0803 | Core classes and interfaces are implemented inside the core |

A library you trust to be pure joins the allowed list in `.editorconfig` or `.globalconfig`:

```ini
fprog_pure_apis = NodaTime, LanguageExt
```

The exact definition of each rule, including the span it reports, is the rule catalog in
[AGENTS.md](https://github.com/ANcpLua/ancplua-fprog/blob/main/AGENTS.md).

## Build from source

```bash
make tools             # restore the local tools (ReSharper CLI, RefTrace)
make check             # build, tests, InspectCode, RefTrace
python3 demo/count.py  # every rule on a wrong and a correct example
make pack              # artifacts/package/ANcpLua.Fprog.Analyzers.<version>.nupkg
```

## License

MIT
