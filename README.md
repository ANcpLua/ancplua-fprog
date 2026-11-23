# ANcpLua.Fprog.Analyzers

Roslyn analyzers that hold C# 15 code to functional-programming rules. You mark the part of your code that has to
be pure, immutable and total. The compiler then reports every place that is not, and each message says what to write
instead. Unions are checked everywhere; the rest of your code stays as it is.

The package is made for code that coding agents write and people review: the rules are exact, the messages are
instructions, and one line of configuration turns every finding into a build error. A green build then means the
core is functional.

## Install

```xml
<PackageReference Include="ANcpLua.Fprog.Analyzers" Version="0.3.0" PrivateAssets="all" />
```

The package needs the .NET 11 SDK (11.0.100 or later), whose compiler is Roslyn 5.12. It adds the analyzers and
compiles one marker attribute into your project as an `internal` type: `Fprog.FunctionalCoreAttribute`. The analyzers
find the attribute by its full name, so a copy in any assembly works. To declare your own copy, add
`FPROG_NO_ATTRIBUTES` to `DefineConstants`.

## Mark the functional core

```csharp
using System.Collections.Immutable;

[Fprog.FunctionalCore]
public sealed record Order(string Id, ImmutableArray<decimal> Lines);

[Fprog.FunctionalCore]
public static class Orders
{
    public static void AddLine(List<decimal> lines, decimal line) => lines.Add(line);

    public static decimal First(Order order) => order.Lines.First();
}
```

The core is every type marked `[Fprog.FunctionalCore]`, every type nested in it, and every type of an assembly
marked `[assembly: Fprog.FunctionalCore]`. The build reports:

```text
Orders.cs(9,24): warning FP0101: 'AddLine' has a void result; return what it computes instead: the new state as a 'with' copy or a new record, a result value for an outcome that can fail, or a Func delegate in place of an Action delegate
Orders.cs(9,76): warning FP0104: Leave the data reached from parameter 'lines' unchanged and return a changed copy instead: a 'with' expression for a record, a new immutable collection for elements
Orders.cs(11,61): warning FP0302: 'First' fails for some inputs; use its total sibling (TryParse, FirstOrDefault, TryGetValue, a list pattern, a type pattern) or guard it, and return the failure as a value
```

Written as the messages say, the same class is silent:

```csharp
[Fprog.FunctionalCore]
public static class Orders
{
    public static ImmutableArray<decimal> AddLine(ImmutableArray<decimal> lines, decimal line) => lines.Add(line);

    public static decimal First(Order order) => order.Lines is [var first, ..] ? first : 0m;
}
```

I/O, clocks, randomness and `async` work belong to the shell around the core: the shell does the work and hands
plain values to the core.

## Unions

A union is a C# 15 `union` type or a `closed` class or record. The compiler knows its cases, so a `switch`
expression that names every case needs no catch-all arm.

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

The union rules keep it that way: every case is named, no arm swallows the cases added later, and no empty union
value is created.

## Use it with a coding agent

Turn the findings into errors, so that a build with a finding fails. In `.editorconfig`:

```ini
[*.cs]
dotnet_analyzer_diagnostic.category-Functional.severity = error
```

Tell the agent what the core is. A block for the `AGENTS.md` of your repository:

```markdown
## Functional core

- Types marked `[Fprog.FunctionalCore]` hold the domain logic. They are pure, immutable and total; the `FP` rules of
  ANcpLua.Fprog.Analyzers check that in every build.
- An `FP####` finding says what to write instead. Change the code that way and keep the rule switched on.
- I/O, clocks, randomness, tasks and third-party calls live in the shell: code without the attribute. The shell
  does the work and passes plain values into the core.
- Model alternatives as a `union` type or a `closed` record, and match them with a `switch` that names every case.
- When a finding looks wrong, show the owner the code and the message.
```

## Rules

All rules have category `Functional` and severity `Warning`.

| ID | Inside the core, the rule reports | Write instead |
| --- | --- | --- |
| FP0101 | a `void` result: method, operator, local function, delegate | return the new value |
| FP0102 | a hidden input: clock, time zone, new GUID, current culture, hash code, mutable static state | take the value as a parameter |
| FP0103 | a `ref` or `out` parameter | return a tuple or a record; `in` and `ref readonly` are allowed |
| FP0104 | a write into memory the caller owns | return a changed copy: `with`, an immutable collection |
| FP0105 | a change of state after construction | return a new instance |
| FP0106 | formatting, parsing, comparing or sorting by the current culture | pass `CultureInfo.InvariantCulture` or `StringComparison.Ordinal` |
| FP0107 | a pointer or `unsafe` code | `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>` |
| FP0201 | a field without `readonly` or `const` | `readonly`, and a new instance for a new value |
| FP0202 | a property with a `set` accessor | `init` or get-only, and a `with` copy |
| FP0203 | a mutable collection in a record or readonly struct | `ImmutableArray<T>`, `ImmutableList<T>`, `ImmutableDictionary<TKey, TValue>`, a frozen collection |
| FP0301 | `throw`, a `Throw…` helper, a `[DoesNotReturn]` call | return the failure as a value |
| FP0302 | a partial operation: `Parse`, `First`, `Single`, a dictionary indexer, an unguarded index or integer division, a downcast | `TryParse`, `FirstOrDefault`, `TryGetValue`, a list pattern, a guard |
| FP0303 | `null` or `default` as a result, `T?` in a return type, the `!` operator | an option or result value, or an empty case of a union |
| FP0401 | a class or record that is neither `sealed`, `static` nor `closed` | add the modifier |
| FP0601 | a runtime type test on `object`, `dynamic` or a type parameter; any `dynamic` | a constraint and its member, a function parameter, a union |
| FP0701 | a closure that captures a variable assigned after its declaration | capture a local that is assigned once, or pass the value in |
| FP0801 | a shell type: I/O, network, processes, threads, third-party libraries, your types outside the core; `lock` | take the result as a parameter, or return a value that describes the effect |
| FP0802 | an asynchronous type or an `async` lambda | await in the shell and pass the plain result |

| ID | On unions and closed classes, the rule reports | Write instead |
| --- | --- | --- |
| FP0403 | a direct subtype of a closed class that is neither `sealed` nor `closed` | add the modifier |
| FP0404 | zero initialization of a union struct whose `Value` is an auto-property | create the union from one of its cases |
| FP0501 | a catch-all in a `switch`: `_`, `var x`, `default:`, `{ }`, a `not` pattern, the base type | one arm or label per case |
| FP0502 | a `switch` statement that leaves a case unhandled | add the missing labels |
| FP0503 | `is` or `as` that picks one case | a `switch` that names every case |

FP0501, FP0502 and FP0503 apply to a closed class when the compiler can list every case for the type being matched.

| ID | Outside the core, the rule reports | Write instead |
| --- | --- | --- |
| FP0803 | a type that derives from a core class or implements a core interface | keep the implementation in the core, or compose |

The exact definition of each rule, including the span it reports, is the rule catalog in
[AGENTS.md](https://github.com/ANcpLua/ancplua-fprog/blob/main/AGENTS.md).

## Configure

Change one rule in `.editorconfig`, for example `dotnet_diagnostic.FP0802.severity = none`.

A library you trust to be pure joins the allowed list, in `.editorconfig` or `.globalconfig`. Each entry is a
namespace or a type name, and a namespace counts with everything below it:

```ini
fprog_pure_apis = NodaTime, LanguageExt
```

## Build from source

```bash
make tools             # restore the local tools (ReSharper CLI, RefTrace)
make check             # build, tests, InspectCode, RefTrace
python3 demo/count.py  # every rule on a wrong and a correct example
make pack              # artifacts/package/ANcpLua.Fprog.Analyzers.<version>.nupkg
```

[CHANGELOG.md](https://github.com/ANcpLua/ancplua-fprog/blob/main/CHANGELOG.md) lists the changes of each release.

## License

MIT
