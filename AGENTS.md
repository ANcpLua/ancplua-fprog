# ancplua-fprog: maintainer contract

Roslyn analyzers that check functional-programming rules in C#, shipped as the NuGet package
`ANcpLua.Fprog.Analyzers`. This file is binding for everyone who changes the repository, human
or agent. `README.md` is the user documentation; `docs/production-setup.md` records why the
repository looks the way it does.

`docs/SPEC.source.md` explains the ideas behind the rules; it was written by another model and
its claims are **not verified**. `docs/ROSLYN.source.md` maps the rules to Roslyn APIs; its API
names were checked against Roslyn 5.9.0, and its errata are listed at the top. Where either
document differs from this file, this file wins.

## Environment

- .NET 11 SDK `11.0.100`, pinned in `global.json`. C# language version `preview`.
- Before you rely on a compiler API you are unsure about, read its real signature:
  `dotnet tool run ref assembly ~/.nuget/packages/microsoft.codeanalysis.csharp/<version>/lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll --type 'Microsoft.CodeAnalysis.CSharp.Syntax.SwitchExpressionSyntax'`
- Tests run on MSTest with Microsoft.Testing.Platform. Reference assemblies come from
  `Basic.Reference.Assemblies.Net110`; nothing is downloaded at test time.
- The analyzer's `Microsoft.CodeAnalysis.CSharp` version stays at or below the compiler in the
  oldest SDK the package supports. A newer one does not load in the compiler (CS9057). Raising
  it raises the minimum SDK for every user: treat it as a breaking change.
- `RefTrace` is not on nuget.org; `tools/feed` holds the package that `nuget.config` points to.

## Commands

| Command | What it does |
| --- | --- |
| `make tools` | Restores the pinned local tools `jb` and `ref` |
| `make build` | Builds the solution with warnings as errors |
| `make test` | Runs all tests |
| `make inspect` | ReSharper InspectCode; fails on any finding at warning level or above |
| `make verify-dll` | RefTrace: no Workspaces dependency, no reflection or dynamic code in the analyzer DLL |
| `make check` | All of the above. A change is done only when this exits 0 |
| `make pack` | Packs `ANcpLua.Fprog.Analyzers` into `artifacts/package` |
| `make manifest` | Writes commit, SDK and tool versions to `artifacts/run-manifest.txt` |

`mise run check` and `mise run test` do the same. CI (`.github/workflows/check.yml`) runs the
`make check` steps on every push, then packs and uploads the `.nupkg`.

## Scope

- **Core** is every type annotated `[Fprog.FunctionalCore]`, the types nested inside it, and
  every type in an assembly annotated `[assembly: Fprog.FunctionalCore]`.
- A **union** is a class or record annotated `[Fprog.Union]`. Its **cases** are the types that
  derive from it directly.
- Match the attributes by metadata name (`Fprog.FunctionalCoreAttribute`, `Fprog.UnionAttribute`)
  in any assembly. The package delivers them as source (`ancplua-fprog/contentFiles/FprogAttributes.cs`);
  `FpAnalyzerTestHost` adds the same declarations to every test compilation.
- Rules FP01xx–FP03xx and FP06xx–FP08xx apply only inside core. Union rules apply to every union.
- Only members declared in source count. Compiler-synthesized members (record `Deconstruct`,
  copy constructors, `EqualityContract`, backing fields) are exempt.

## Rule catalog (binding)

The **report at** column is the exact span a diagnostic covers. Tests mark it, and users
suppress and navigate by it: changing a span is a breaking change.

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

### Candidate rules

Not implemented. Each needs an exact definition and span in the catalog before work starts.

- Smart constructors: a public constructor on a type that also offers `TryCreate`/`Parse`.
- Boolean-flag records: a record with two or more `bool` state properties.
- Capturing `this` of a mutable type in a closure.

## Diagnostic conventions

- One `DiagnosticDescriptor` per ID, category `Functional`, default severity `Warning`,
  enabled by default. Titles and messages may change; ID, category, default severity and span
  are release surface.
- Analyzers are stateless and thread-safe; call `EnableConcurrentExecution()` and
  `ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None)`. An analyzer must not
  throw (AD0001) on any input, including incomplete code.
- The analyzer project targets `netstandard2.0`, keeps `EnforceExtendedAnalyzerRules`, and does
  not reference `Microsoft.CodeAnalysis.Workspaces` (`make verify-dll` checks this).
- Type names use .NET casing with the rule number, for example `Fp0301ThrowAnalyzer` and
  `Fp0301Tests`. Diagnostic IDs and test method names keep the upper-case ID
  (`FP0301_<Case>_Reports`). InspectCode accepts both.

## Changing a rule

1. Change its catalog row first. If the row is ambiguous for a case, choose the stricter
   reading and write that case down as a test.
2. Write the test before the fix. Name every test method `FPxxxx_<Case>_Reports` or
   `FPxxxx_<Case>_NoReport` and call `FpAnalyzerTestHost.VerifyRuleAsync("FPxxxx", source, ct)`
   with markup `{|FPxxxx:...|}`. For FPS001 call
   `FpAnalyzerTestHost.VerifySuppressionAsync("CS8509", source, ct, expected)` with markup
   `{|#0:switch|}` and `DiagnosticResult.CompilerWarning("CS8509").WithLocation(0).WithIsSuppressed(true)`.
3. Every rule has at least one `_NoReport` test with correct code that looks similar, for example
   a local `StringBuilder` in a pure function or the same code outside core. Every false positive
   that is fixed gets a `_NoReport` test with the code that triggered it.
4. Keep each rule in its own files: `ancplua-fprog/Rules/Fpxxxx*.cs` and
   `ancplua-fprog.Tests/Rules/FpxxxxTests.cs`. Shared helpers live in `ancplua-fprog/Shared/`;
   say in the commit message when you change one.
5. Commit as `FPxxxx: <change in a few words>`.

## Releases and versions

- A new ID goes into `AnalyzerReleases.Unshipped.md` (RS2008). Fix that finding; do not suppress it.
- At a release, move the rows from `Unshipped` to a `## Release <version>` section in
  `AnalyzerReleases.Shipped.md`. A changed category or severity of a shipped rule goes into
  `### Changed Rules`, a removed one into `### Removed Rules`.
- `VersionPrefix` in `ancplua-fprog/ancplua-fprog.csproj` is the next version. Semantic
  versioning: a new rule or a wider rule is a minor version while below 1.0 and a major version
  after it, because it can break a build that treats warnings as errors.
- CI publishes to nuget.org only from a `v<version>` tag that the owner pushes.

## Allowed and forbidden

- Copying code from GitHub or from the Roslyn sources is allowed. Put the source URL in a
  comment above it.
- Package references change only with a reason in the commit body; see the Roslyn pin above.
- Do not silence findings: no `#pragma warning disable`, `NoWarn`, `SuppressMessage`, ReSharper
  disable comments or settings files, and no deleted or skipped tests to make `make check` pass.
- No network access from tests or analyzers.
