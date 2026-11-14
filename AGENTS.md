# ancplua-fprog: playbook

You are building Roslyn analyzers that verify functional-programming rules in C#.
This file is binding. `docs/SPEC.source.md` explains the ideas behind the rules; it was
written by another model and its claims are **not verified**. `docs/ROSLYN.source.md` maps the
rules to Roslyn APIs; its API names were checked against Roslyn 5.9.0, and its errata are listed
at the top. Where either document differs from this file, this file wins.

## Goal

Implement as many rules from the catalog below as you can, each one correct.
Correct means: the hidden tests for that rule pass and the rule reports nothing on the
hidden clean corpus. A rule that is half done scores nothing, so finish rules one at a time.

## Environment

- .NET 11 SDK `11.0.100`, pinned in `global.json`. C# language version `preview`.
- Newer compiler APIs may be missing from your training data. Before you rely on an API you
  are unsure about, read its real signature:
  `dotnet tool run ref assembly ~/.nuget/packages/microsoft.codeanalysis.csharp/<version>/lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll --type 'Microsoft.CodeAnalysis.CSharp.Syntax.SwitchExpressionSyntax'`
- Tests run on MSTest with Microsoft.Testing.Platform. Reference assemblies come from
  `Basic.Reference.Assemblies.Net110`; nothing is downloaded at test time.
- The analyzer's `Microsoft.CodeAnalysis.CSharp` version is pinned to the compiler in the pinned
  SDK or lower. A newer one would not load in the compiler (CS9057). Do not change it.

## Commands

| Command | What it does |
| --- | --- |
| `make tools` | Restores the pinned local tools `jb` and `ref` |
| `make build` | Builds the solution with warnings as errors |
| `make test` | Runs all visible tests |
| `make inspect` | ReSharper InspectCode; fails on any finding at warning level or above |
| `make verify-dll` | RefTrace: no Workspaces dependency, no reflection or dynamic code in the analyzer DLL |
| `make check` | All of the above. Your work is done only when this exits 0 |
| `make manifest` | Writes commit, SDK and tool versions to `artifacts/run-manifest.txt` |

`mise run check` and `mise run test` do the same.

## Scope

- **Core** is every type annotated `[Fprog.FunctionalCore]`, the types nested inside it, and
  every type in an assembly annotated `[assembly: Fprog.FunctionalCore]`.
- A **union** is a class or record annotated `[Fprog.Union]`. Its **cases** are the types that
  derive from it directly.
- Match the attributes by metadata name (`Fprog.FunctionalCoreAttribute`, `Fprog.UnionAttribute`).
  `FpAnalyzerTestHost` adds their source to every test compilation.
- Rules FP01xx–FP03xx and FP06xx–FP08xx apply only inside core. Union rules apply to every union.
- Only members declared in source count. Compiler-synthesized members (record `Deconstruct`,
  copy constructors, `EqualityContract`, backing fields) are exempt.

## Rule catalog (binding)

The **report at** column is the exact span the hidden tests mark. A wrong span fails the test.

| ID | Rule | Report at |
| --- | --- | --- |
| FP0101 | A method or local function returns `void` | the method or local function name |
| FP0102 | Hidden input: reads `DateTime.Now/UtcNow/Today`, `DateTimeOffset.Now/UtcNow`, `Guid.NewGuid`, `TimeProvider.System`, any member of `System.Environment`, any member of `System.Random` or `new Random(...)`, or a static field that is neither `readonly` nor `const` | the name token of that member, or `Random` in the object creation |
| FP0103 | A `ref` or `out` parameter (`in` and `ref readonly` are allowed) | the parameter name |
| FP0104 | Writes through a reference-type parameter: assignment, compound assignment, `++` or `--` whose target is a field, property or element reached from that parameter | the assignment target expression |
| FP0201 | A field without `readonly` or `const` | the variable declarator name |
| FP0202 | A property with a `set` accessor (`init` is allowed) | the `set` keyword |
| FP0203 | A record or readonly struct has a field or property whose type is an array, `StringBuilder`, or any type from `System.Collections`, `.Generic`, `.ObjectModel` or `.Concurrent` (interfaces included). `System.Collections.Immutable` and `.Frozen` are allowed | the member name (for positional records: the parameter name) |
| FP0301 | A `throw` statement or `throw` expression, including `throw;` | the `throw` keyword |
| FP0302 | Calls a partial member: any static `Parse` method on a type in namespace `System`, `Enumerable.First/Last/Single/ElementAt` (not the `OrDefault` forms), or reads an indexer of `Dictionary<,>`, `IDictionary<,>` or `IReadOnlyDictionary<,>` | the method name token; for indexers the whole element access `d[key]` |
| FP0303 | Returns `null` or `default` from a method, local function or lambda whose return type is a reference type or nullable | the `null` or `default` literal/expression |
| FP0401 | A union is not abstract | the union's name |
| FP0402 | A union has an instance constructor that is not private, or has no declared constructor (the implicit one is not private) | the constructor name, or the union's name for the implicit one |
| FP0403 | A case is not sealed or is not nested inside its union | the case's name |
| FP0501 | A `switch` over a union value uses a catch-all: discard `_`, a `var` pattern, `default:`, an empty property pattern `{ }`, `not null`, or a type pattern (with or without a designation) whose type is the union itself or one of its base types, including `object` | that pattern, or the `default` keyword |
| FP0502 | A `switch` expression over a union value does not handle every case with an arm that has no `when` clause | the `switch` keyword |
| FPS001 | A `DiagnosticSuppressor` that suppresses CS8509 on a `switch` expression over a union value that handles every case (same definition as FP0502) | suppression of the CS8509 span |
| FP0601 | A type test involving an unconstrained type parameter `T`: `is`, `as`, a type pattern, `typeof(T)`, or `.GetType()` on a value of type `T` | the `is`/`as` keyword, the pattern, the `typeof` expression, or the `GetType` name |
| FP0701 | A lambda or local function captures a local or parameter that is assigned anywhere after its declaration, inside or outside the closure (including `++`, `--`, compound assignment, `ref`/`out` argument) | the first use of that variable inside the closure body |
| FP0801 | References `System.IO`, `System.Net*`, `System.Data*`, `System.Console`, `System.Diagnostics.Process`, or `System.Threading.Thread/Monitor/Lock/Mutex/Semaphore/SemaphoreSlim/Interlocked/Timer`, or uses a `lock` statement. `using` directives do not count | the name token of the referenced type, or the `lock` keyword |
| FP0802 | A method or local function returns `Task`, `Task<T>`, `ValueTask` or `ValueTask<T>` | the return type |

## Bonus rules

Not scored until the referee publishes an exact definition. Do not start them before every
binding rule you can finish is finished.

- Smart constructors: a public constructor on a type that also offers `TryCreate`/`Parse`.
- Boolean-flag records: a record with two or more `bool` state properties.
- Capturing `this` of a mutable type in a closure.

## Diagnostic conventions

- One `DiagnosticDescriptor` per ID, category `Functional`, default severity `Warning`,
  enabled by default. Titles and messages are free; hidden tests check only ID and span.
- Analyzers are stateless and thread-safe; call `EnableConcurrentExecution()` and
  `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`.
- The analyzer project targets `netstandard2.0` and keeps `EnforceExtendedAnalyzerRules`.
- Type names use .NET casing with the rule number, for example `Fp0301ThrowAnalyzer` and
  `Fp0301Tests`. Diagnostic IDs and test method names keep the upper-case ID
  (`FP0301_<Case>_Reports`). InspectCode accepts both.
- Record each new ID in `AnalyzerReleases.Unshipped.md` (RS2008). Fix that finding; do not suppress it.

## Workflow

**Phase 0, one agent, sequential.** Add the shared helpers every rule needs: deciding whether
a symbol is in core, and building the union model (union type, its cases). `make check` must pass
before anyone starts phase 1.

**Phase 1, parallel, one agent per rule.** Each agent owns exactly one rule and only its files:
`ancplua-fprog/Rules/Fpxxxx*.cs`, `ancplua-fprog.Tests/Rules/FpxxxxTests.cs` and its own line in
`AnalyzerReleases.Unshipped.md`. Shared helpers change only with a note in the commit message.

For each rule:

1. Read its row in the catalog. If the row is ambiguous for a case, choose the stricter reading
   and write that case down as a test.
2. Write tests first. Name every test method `FPxxxx_<Case>_Reports` or `FPxxxx_<Case>_NoReport`
   and call `FpAnalyzerTestHost.VerifyRuleAsync("FPxxxx", source, ct)` with markup `{|FPxxxx:...|}`.
   Hidden tests use the same method, so your analyzer class names are free. For FPS001 call
   `FpAnalyzerTestHost.VerifySuppressionAsync("CS8509", source, ct, expected)` with markup
   `{|#0:switch|}` and `DiagnosticResult.CompilerWarning("CS8509").WithLocation(0).WithIsSuppressed(true)`.
3. Include at least one `_NoReport` test with correct FP code that looks similar, for example a
   local `StringBuilder` in a pure function (allowed) or the same code outside core.
4. Implement until `make test` passes, then commit: `FPxxxx: <rule in a few words>`.

Finish with `make check` and `make manifest`.

## Allowed and forbidden

Allowed:

- Copying code from GitHub or from the Roslyn sources. Put the source URL in a comment above it.
- Spawning as many subagents as useful, within the token budget.

Forbidden, and checked by the referee:

- Adding or changing package references.
- Editing protected files: `Makefile`, `mise.toml`, `global.json`, `nuget.config`, this file,
  `CLAUDE.md`, `docs/SPEC.source.md`, `docs/ROSLYN.source.md`, `.config/dotnet-tools.json`, `tools/feed`, `eng/`,
  `ancplua-fprog.Tests/FpAnalyzerTestHost.cs`, `ancplua-fprog.Tests/Fixtures/`.
- Silencing findings: `#pragma warning disable`, `NoWarn`, `SuppressMessage`, ReSharper disable
  comments or settings files, deleting or skipping tests to make `make check` pass.
- Network access from tests or analyzers.

## Scoring

The referee runs `make score` with hidden tests and a clean corpus kept outside this repository.
A rule scores 1 when it has hidden tests, all of them pass, and the corpus has no diagnostic
with its ID. Highest score wins; on a tie, the shorter wall time wins. Analyzer crashes in the
corpus (AD0001) are listed and break ties against you.
