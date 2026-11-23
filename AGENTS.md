# ancplua-fprog: playbook

Roslyn analyzers that hold C# 15 code to functional-programming rules.

This file describes the product **as it shall be**. It is the single source of truth. When a rule changes, rewrite
its row here in the same change, so the file always shows the current state and stays one coherent text.
`docs/SPEC.source.md` gives the ideas behind the rules and `docs/ROSLYN.source.md` maps rules to Roslyn APIs; both
follow this file.

## Goal

Every rule in the catalog reports each bug of its kind and stays silent on correct functional code.

- A gap is closed at its root: replace the model that produced it, then delete every piece of code, test and text
  that the new model makes unnecessary.
- You own every file in this repository: analyzers, tests, test host, fixtures, demo, docs, Makefile, this file.
  Git history is the archive, so the working tree holds only the current design.
- A rule is done when its tests pass, its demo pair shows the expected counts and `make check` exits 0.

## Environment

- .NET SDK `11.0.100`, pinned in `global.json`. The analyzed code is C# 15 on .NET 11. C# 15 is the default
  language version of `net11.0`; `LangVersion preview` adds only the memory-safety preview. The rules assume C# 15
  and build on its `union` types and `closed` classes.
- The analyzer targets `netstandard2.0` and references `Microsoft.CodeAnalysis.CSharp` 5.12.0, the compiler version
  inside the pinned SDK (a newer one fails to load, CS9057). The test project pins the same version, so tests run
  on the compiler that runs the analyzers in a build. Raise the pin when a newer version loads in the pinned
  compiler, and prove it with `make check` and the demo.
- In Roslyn 5.12.0 the union and closed-class API is stable: `IsUnion`, `UnionCaseTypes`, `IsClosed`,
  `GetClosedDerivedTypeInfo`, `UnionDeclarationSyntax`, `WithElementSyntax`, `LanguageVersion.CSharp15`. Still
  `[Experimental("RSEXPERIMENTAL006")]`: `UnsafeExpressionSyntax`, `SyntaxKind.UnsafeExpression`,
  `SyntaxKind.SafeKeyword`, `ISymbol.RequiresUnsafeContext` and the memory-safety options. A file that uses one of
  these carries its own `#pragma warning disable RSEXPERIMENTAL006` with the reason; the projects set no
  suppression.
- Read the real signature of an API before you rely on it:
  `dotnet tool run ref assembly ~/.nuget/packages/microsoft.codeanalysis.csharp/5.12.0/lib/netstandard2.0/Microsoft.CodeAnalysis.CSharp.dll --type '<full type name>'`
- Tests run on MSTest with Microsoft.Testing.Platform. Reference assemblies come from
  `Basic.Reference.Assemblies.Net110`; tests work offline.

### C# 15 facts the rules build on

Observed with SDK 11.0.100, whose compiler is Roslyn 5.12.0, and with the 5.12.0 packages.

| Fact | Consequence |
| --- | --- |
| `public union Pet(Cat, Dog, Bird);` declares a struct: `TypeKind.Struct`, `IsUnion` is true, it implements `System.Runtime.CompilerServices.IUnion`. `ITypeSymbol.UnionCaseTypes` returns its case types with type arguments substituted, also for a hand-written `[Union]` type. It is empty for a closed class and for `Nullable<T>` of a union | Cases of a union come from `UnionCaseTypes`; unwrap `Nullable<T>` first |
| `default(Pet)` and `new Pet()` hold no case. A switch expression that names every case then throws `SwitchExpressionException`, and the compiler stays silent | FP0404 |
| A `null` arm on a union struct compiles and makes the switch complete for the empty value | A `null` arm is ordinary handling, FP0501 leaves it alone |
| `closed` applies to classes and record classes, makes the type abstract (`new` gives CS0144) and keeps its direct subtypes in the same assembly. `GetClosedDerivedTypeInfo(ct).ClosedDerivedTypes` lists them | A closed class is a union; its direct subtypes are its cases |
| A case may itself be `closed`. A switch expression that names the leaf types is complete for the compiler | Nested unions; a case is finished when it is `sealed` or `closed` (FP0403) |
| The compiler checks switch **expressions** over unions and closed classes (CS8509, CS8655 for a missing `null`) | Switch statements are FP0502's job; switch expressions are the compiler's |
| A `_`, `var x` or `object` arm satisfies the compiler and hides every case added later | FP0501 |
| For `T s where T : Closed`, a switch expression that names every case is complete for the compiler, as it is for the closed type itself | A type parameter constrained to a closed class is a union value |
| Pattern operations over a union carry `InputType` = the union and `NarrowedType` = the case. For `x is Cat` the type pattern operation is implicit and its syntax is the whole `is` expression | Take spans from syntax for `is` |
| `(object)pet` is a boxing conversion whose operand has the union type | Look through casts on the governing expression |
| `ISwitchExpressionOperation.IsExhaustive` is false for a complete switch over a closed hierarchy and over a union struct without a `null` arm | Compute coverage from the cases |
| With `LangVersion preview`, a pointer declaration and `unsafe(expr)` compile outside an `unsafe` block. With C# 15 they are errors (CS0214, CS8652) | FP0107 is syntactic and covers both |
| A syntax node action runs once per node, also inside a union body. The driver skips the defining part of a partial constructor | Syntax node actions need no containing-symbol filter; FP0103 walks the semantic model |
| Extension indexers, collection expression arguments `[with(...), ..]` and labeled `break`/`continue` compile and analyze cleanly | Each has a test that pins this |
| Runtime async (`<Features>runtime-async=on</Features>`) changes code generation only; syntax and symbols are the same | FP0802 behaves the same with the feature on; one test pins this |

## Commands

| Command | What it does |
| --- | --- |
| `make tools` | Restores the pinned local tools `jb` and `ref` |
| `make build` | Builds the solution with warnings as errors |
| `make test` | Runs all tests |
| `make inspect` | ReSharper InspectCode; passes with 0 findings at warning level or above |
| `make verify-dll` | RefTrace: the analyzer DLL is free of Workspaces, reflection and dynamic code |
| `make check` | All of the above. Work is done when this exits 0 |
| `python3 demo/count.py` | Builds `demo/`, compares every diagnostic with the `// expect: <ID>` comments, ends with `RESULT: everything as expected` |

## Scope

- **Core** is every type annotated `[Fprog.FunctionalCore]`, the types nested inside it, and every type of an
  assembly annotated `[assembly: Fprog.FunctionalCore]`. The attribute is matched by metadata name
  (`Fprog.FunctionalCoreAttribute`), in source and in referenced assemblies. It applies to assemblies, classes,
  structs, interfaces, enums and delegates. `FpAnalyzerTestHost` and `demo/Fprog.Annotations.cs` declare it.
- A **union** is (a) a type whose `ITypeSymbol.IsUnion` is true, or (b) a class or record with the `closed`
  modifier. Its **cases** are (a) its `ITypeSymbol.UnionCaseTypes`, (b) its direct
  subtypes. A case that is itself closed stands for its own cases.
- A **union value** is an expression whose type is a union, `Nullable<T>` of a union, or a type parameter
  constrained to a closed class. A union value that a cast or an implicit conversion turned into a base type,
  an interface or `object` at the head of a `switch`, `is` or `as` still counts as that union value.
- Where the rules apply:
  - inside core: FP0101 to FP0303, FP0401, FP0601 to FP0802;
  - on every union and union value in the compilation: FP0403, FP0404, FP0501, FP0502, FP0503;
  - on types outside core: FP0803.
- Only members declared in source count. Compiler-synthesized members (record `Deconstruct`, copy constructors,
  `EqualityContract`, backing fields) are exempt.

## Purity classes

Every type a core member touches falls into exactly one class, tested in this order.

| Class | Types | Used from core |
| --- | --- | --- |
| **Core** | see Scope | free; the rules check it |
| **Async** | every type in `System.Threading.Tasks` and below; `IAsyncEnumerable<T>`, `IAsyncEnumerator<T>`, `IAsyncDisposable`; `System.Linq.AsyncEnumerable`; the types of `System.Runtime.CompilerServices` whose name contains `Await` or `Async` | FP0802 |
| **Pure** | the list below, plus the entries of `fprog_pure_apis` | free; single members are FP0102, FP0106, FP0302 |
| **Shell** | everything else: other platform types, third-party types, source types outside core | FP0801 |

Pure list:

- every type in `System.Collections`, `System.Collections.Generic`, `System.Collections.ObjectModel`,
  `System.Collections.Immutable`, `System.Collections.Frozen`, `System.Numerics`, `System.Globalization`,
  `System.Buffers.Binary`, `System.Buffers.Text`, `System.Diagnostics.CodeAnalysis`, and `System.Text` with every
  namespace below it;
- namespace `System` itself, except `Activator`, `AppContext`, `AppDomain`, `Console`, `Environment`, `GC`,
  `Random`, `TimeProvider`, `OperatingSystem`, `WeakReference`, `WeakReference<T>`, `Progress<T>`, `IProgress<T>`,
  `IServiceProvider`, `IObservable<T>`, `IObserver<T>`, `IAsyncResult`, `AsyncCallback`, `MarshalByRefObject`;
- in `System.Linq`: `Enumerable`, `ImmutableArrayExtensions`, `IGrouping<,>`, `ILookup<,>`, `Lookup<,>`,
  `IOrderedEnumerable<>`;
- in `System.Runtime.CompilerServices`: `ITuple`, `IUnion`, `FormattableStringFactory`,
  `DefaultInterpolatedStringHandler`;
- in `System.Buffers`: `SearchValues`, `SearchValues<T>`.

`fprog_pure_apis` is an analyzer config key (`.globalconfig` or `.editorconfig`): a comma-separated list of full
namespace or type names that count as Pure, each namespace with everything below it. It is the door for trusted
libraries: `fprog_pure_apis = NodaTime, LanguageExt`. Read it per syntax tree, with the global options as fallback.

The list lives in one file, `ancplua-fprog/Shared/Purity.cs`, and this section mirrors it.

## Rule catalog

**At** is the exact span of the diagnostic; the tests mark it.

| ID | Reports | At |
| --- | --- | --- |
| FP0101 | A `void` result in any of these forms. (a) A method, user-defined operator or local function returns `void`. (b) A delegate declaration returns `void`. (c) A written delegate type whose `Invoke` returns `void` (`Action`, `Action<…>`, `EventHandler`, …) in the type of a parameter, return, local, field, property, event or type argument. (d) A lambda, anonymous method or method group that becomes such a delegate without its target type written in core, as in an argument to a member outside core or a `var` local | (a) the name or the operator token, (b) the name, (c) the type's name token, (d) the `=>` token, the `delegate` keyword or the method group's name |
| FP0102 | Reads a hidden input or accesses mutable static state (list below) | the name token of the member |
| FP0103 | A `ref` or `out` parameter (`in` and `ref readonly` are allowed) | the parameter name |
| FP0104 | Writes into memory the caller owns: the target or receiver is *reached from a parameter* (below). Forms: assignment, compound assignment (including event subscription), `??=`, `++`, `--`, a deconstruction target, an argument bound to a `ref` or `out` parameter, a *mutating call* (below) | the target or argument expression; for a mutating call the method name |
| FP0105 | Changes state after construction. The forms of FP0104 apply to a target or receiver *reached from a field*. Event subscriptions also count on `this` and on static events | as FP0104. For an event, the event name |
| FP0106 | Depends on the current culture (list below) | see list |
| FP0107 | Uses a pointer or unsafe code: a pointer or function-pointer type, the address-of operator, a `fixed` statement, the `unsafe` modifier, an `unsafe` block, an `unsafe(…)` expression | the type syntax, the `&` token, the `fixed` keyword, the `unsafe` keyword |
| FP0201 | A field without `readonly` or `const`, or a primary-constructor parameter that a member body writes directly or through a `ref` local | the variable declarator name or parameter name |
| FP0202 | A property with a `set` accessor (`init` is allowed) | the `set` keyword |
| FP0203 | A record or readonly struct has a field or property whose type is an array (including `System.Array`), `StringBuilder`, `Memory<T>`, `ArraySegment<T>`, any type declared directly in `System.Collections`, `System.Collections.Generic` or `System.Collections.ObjectModel` (interfaces included), or a nested `Builder` type of an immutable collection. The other types of `System.Collections.Immutable` and `.Frozen` are the collections to use | the member name (for positional records: the parameter name) |
| FP0301 | A `throw` statement or `throw` expression, including `throw;`; a call to a static `Throw…` helper of an exception type; a call to a member annotated `[DoesNotReturn]` | the `throw` keyword; the method name |
| FP0302 | Uses a partial operation in the list below. Method entries cover calls and method groups | see list |
| FP0303 | (a) A method, local function, lambda, getter or operator returns `null` or `default` through a result type that can hold null, including async results, yielded values, pointers and unconstrained type parameters. (b) A method, local function or operator writes `T?` anywhere in its return type, including tuple elements and type arguments. (c) The null-forgiving operator `!` | (a) the literal or `default` expression; (b) each such `?` token; (c) the `!` token |
| FP0401 | A class or record in core is open: it carries neither `sealed`, `static` nor `closed`. Cases of a closed class belong to FP0403 | the type's name |
| FP0403 | A case of a closed class carries neither `sealed` nor `closed` | the case's name |
| FP0404 | Creates an empty value of a union struct: `default`, `default(U)` or `new U()` | that expression |
| FP0501 | A `switch` expression arm or `switch` statement label over a union value is a catch-all: `_`, `var x`, `default:`, a property pattern without subpatterns `{ }`, any `not` pattern, or a type pattern (with or without a designation) whose type every case converts to (the union itself, a shared base type or interface, `object`). The input type or a type it always converts to also counts, including for constrained parameters. A pattern naming the only case explicitly is allowed. Every pattern position whose input is a union value counts, including tuple elements, positional and property subpatterns and the sides of logical patterns. A top-level catch-all over a tuple with a union element also counts | that pattern, or the `default` keyword |
| FP0502 | A `switch` statement over a union value has no catch-all and leaves a case unhandled (handling: below) | the `switch` keyword |
| FP0503 | Picks one case out of a union value outside a `switch`: an `is` expression whose pattern names a type, or an `as` expression | the type or pattern to the right of `is`; the type to the right of `as` |
| FP0601 | Inspects a runtime type that the static type hides. The inspected value has static type `object`, `dynamic`, a type parameter or `Nullable<T>` of a type parameter. Union values are exempt and keep their identity through upcasts at the head of `switch`, `is` or `as`. Reports `is` and `as` type tests, type patterns in switches, explicit reference downcasts, unboxing casts, casts from `dynamic` to a more specific type, and calls to `Object.GetType()` or its dynamic equivalent. Tests for null alone are allowed. An `object` parameter of a member that overrides or implements a member declared outside core (`Equals(object)`, `CompareTo(object)`) may be inspected within that member. Also reports `typeof(T)` and `typeof(T?)` of a type parameter and every written use of `dynamic` | the `is` or `as` keyword, the switch pattern, the cast or `typeof` expression, the `GetType` name, the `dynamic` identifier |
| FP0701 | A lambda or local function captures a local or parameter that is assigned anywhere after its declaration, inside or outside the closure (including `++`, `--`, compound assignment, `ref`/`out` argument) | the first use of that variable inside the closure body |
| FP0801 | Uses a Shell type (entry rule below); a `lock` statement | see entry rule; the `lock` keyword |
| FP0802 | Uses an Async type (entry rule below); an `async` lambda or anonymous method | see entry rule; the `async` keyword |
| FP0803 | A type outside core derives from a core class or implements a core interface | the name token of that core type in the base list |

IDs FP0402 and FPS001 are retired and stay unused: `closed` gives the compiler the knowledge that the private
constructor and the suppressor used to imitate.

### FP0102: hidden inputs

The ambient members and mutable static state below belong to Pure types. Calls or method groups count for the
ambient methods. The hash-code check also applies to a call named `GetHashCode` on any type.
`Random`, `Environment`, `TimeProvider`, `Stopwatch`, `RandomNumberGenerator` and `Thread` arrive as FP0801.

- Clock: `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`.
- Local time zone: `TimeZoneInfo.Local`, `TimeZoneInfo.FindSystemTimeZoneById`, `TryFindSystemTimeZoneById`,
  `GetSystemTimeZones`, `DateTime.ToLocalTime`, `DateTime.ToUniversalTime`, `DateTimeOffset.ToLocalTime`,
  `DateTimeOffset.LocalDateTime`.
- Randomness: `Guid.NewGuid`, `Guid.CreateVersion7`.
- Current culture as a value: `CultureInfo.CurrentCulture`, `CurrentUICulture`, `DefaultThreadCurrentCulture`,
  `DefaultThreadCurrentUICulture`, `InstalledUICulture`, `RegionInfo.CurrentRegion`, `NumberFormatInfo.CurrentInfo`,
  `DateTimeFormatInfo.CurrentInfo`, `StringComparer.CurrentCulture`, `StringComparer.CurrentCultureIgnoreCase`,
  `StringComparison.CurrentCulture`, `StringComparison.CurrentCultureIgnoreCase`.
- Hash codes, whose values change from process to process: a call to any method named `GetHashCode` and any member
  of `System.HashCode`. The body of a `GetHashCode` override is the place for them and is exempt.
- Static state: a static field that is neither `readonly` nor `const`; a static property that has a `set` accessor.

### FP0106: current culture

Culture-free types: `char`, `bool`, `Guid`, `Rune`, `Version`, `Uri`, every enum, and `string` for `ToString`.

| Form | At |
| --- | --- |
| A call to a method of a Pure type, outside the culture-free types, while the type has an overload with the same name and the same parameters plus one `IFormatProvider` or `CultureInfo` (`value.ToString("C")`, `value.ToString()`, `int.TryParse(text, out n)`, `string.Format(format, value)`, `text.ToUpper()`, `Convert.ToDouble(text)`) | the method name |
| `string.Compare`, `CompareTo`, `StartsWith`, `EndsWith`, `IndexOf` and `LastIndexOf` with `string` arguments, bound to an overload that takes neither `StringComparison` nor `CultureInfo` | the method name |
| `OrderBy`, `OrderByDescending`, `ThenBy`, `ThenByDescending`, `Order`, `OrderDescending`, `Min`, `Max`, `MinBy`, `MaxBy`, and `Sort`/`BinarySearch` of arrays and lists, with a `string` key or element and without a comparer | the method name |
| An interpolated string that becomes a `string` and has a hole whose value is `IFormattable` and outside the culture-free types. `string.Create(CultureInfo.InvariantCulture, $"…")` and `FormattableString.Invariant($"…")` are the invariant forms | the expression in the hole |
| String concatenation with such a value as an operand | that operand |

### FP0302: partial members

| Member | At |
| --- | --- |
| A static `Parse` or `ParseExact` of a Pure type, or through a type parameter (`T.Parse`) | the method name |
| `System.Convert`: every `To…` method except `ToString`, `ToBase64String`, `ToBase64CharArray`, `ToHexString`, `ToHexStringLower`; every `From…` method; `ChangeType` | the method name |
| `First`, `Last`, `Single`, `ElementAt` of `Enumerable` and `ImmutableArrayExtensions`; `Enumerable.Aggregate` without a seed; `Min`, `Max`, `Average`, `MinBy`, `MaxBy` when the result is a non-nullable value type | the method name |
| `Dequeue`, `Peek`, `Pop` of any `System.Collections*` type | the method name |
| A read of a dictionary indexer: the type is declared in `System.Collections` or below, implements `IDictionary<,>` or `IReadOnlyDictionary<,>` or derives from `KeyedCollection<,>`, and the indexer takes the key | the whole element access |
| A positional element read, unless guarded: an array, `string`, `Span<T>`, `ReadOnlySpan<T>`, or a `System.Collections*` type through an `int`, `Index` or `Range` indexer | the whole element access |
| `Nullable<T>.Value`, unless guarded | the `Value` name |
| Integer or `decimal` division and remainder (`/`, `%`, `/=`, `%=`), unless the divisor is a constant other than zero or guarded | the operator token |
| An explicit reference downcast or unboxing cast between named types (casts from `object`, `dynamic` and type parameters are FP0601) | the cast expression |

A use is **guarded** when a condition that tests the same symbol stands before it: the condition of an enclosing
`if`, `while`, `for`, `?:` or `when`, the left side of an enclosing `&&` or `||`, or the condition of an `if`
statement that precedes the use in the same or an enclosing block. The test is: for an element read, a read of
`Length`, `Count`, `IsEmpty`, `IsDefaultOrEmpty` or `Any()` on the same collection, a comparison that involves the
same index variable, or a list pattern on the collection; for a division, a comparison or pattern on the divisor;
for `Value`, `HasValue` or a null test on the same nullable. When several case labels share a switch section,
each label needs a qualifying `when` to guard its body. The rule accepts the conditions as written and leaves their
logic to the author.

FP0302 is a list by nature. Each entry has a total sibling (`TryParse`, `FirstOrDefault`, `TryGetValue`, a list
pattern, a type pattern), so every report comes with a way out.

### Reached from a parameter, reached from a field, mutating call (FP0104, FP0105, FP0203)

- A write lands in **memory the caller owns** when the path from the parameter to the written location crosses
  shared storage. The path can start at a reference, span or memory parameter, or at a parameter passed by
  `ref` or `out`. It can also cross a reference, span or memory read from a field, property or element, or
  follow a writable `ref` field or result. Examples are `orders[0].Lines.Add(x)` for `ImmutableArray<Order>`
  and `pair.First.Total = 0` for a tuple or record struct. Replacing `pair.First` or rebinding a `ref` field
  of a by-value parameter changes that parameter's own storage. FP0103 covers direct writes to the named
  `ref` or `out` parameter at its signature.
- An expression is **reached from** a parameter when it is the parameter; a field, property, indexer or element
  read from something reached; `?:`, `??` or a switch expression with a reached branch; a span or memory made from
  something reached by `AsSpan`, `AsMemory`, slicing or an implicit conversion; or a local that stands for something
  reached. A local stands for it when each of its assignments is such an expression: declared locals, `ref` locals,
  `foreach` variables, pattern variables at any depth, deconstruction variables.
- A call on a reached collection whose result (or `out` variable) has the collection's element type, a reference
  type, hands out one of its elements, and that element is reached: `orders.First()`, `orders.Find(…)`,
  `byId.TryGetValue(id, out var order)`.
- Every other value that a method call returns starts fresh (`values.ToArray()`, `source.ToList()`), as do
  `new`, `with` and a boxed copy. A local copy of a struct has its own slots, and a reference or view it
  carries can still reach the source. A writable `ref` result used as a target designates storage reached
  from its receiver. Tuple deconstruction follows the source of each selected element.
- **Reached from a field** uses the same steps with an instance field of `this`, a static field or a source
  property of either kind with a backing field. A constructor or initializer that fills its
  own fields is construction. Direct writes to a field's own storage are covered by the field-declaration and
  static-state rules.
- A **mutating call** writes its receiver: an instance method, or an extension method of
  `System.Collections.Generic.CollectionExtensions`, bound to `System.Array`, `StringBuilder`, `Span<T>`, or a
  `System.Collections*` type outside `.Immutable` and `.Frozen` (their nested `Builder` types count), whose name is
  in the shared table of mutating members in `ancplua-fprog/Shared/Mutators.cs`. The static writers of
  `System.Array`, `System.Buffer` and `System.MemoryExtensions` (`Sort`, `Reverse`, `Clear`, `Fill`, `Copy`,
  `BlockCopy`, …) and every `CopyTo` or `TryCopyTo` write their destination argument.
- The record member `PrintMembers(StringBuilder)` appends to its parameter by contract and is exempt from FP0104.

### Handling a case (FP0502)

A label handles a case when it has no `when` clause and its pattern accepts every value of the case: a type or
declaration pattern that names the case or one of its base types or interfaces; a recursive pattern of such a type
whose subpatterns are all `_` or `var`; an `or` pattern with such a side. A closed case is handled when each of its
own cases is handled. FP0502 speaks when the statement has no catch-all; a statement with one gets FP0501 there.

### Entry rule (FP0801, FP0802)

A Shell value (for FP0802: an Async value) is reported once, where it enters core code.

1. A written name of a Shell type (for FP0802: of an Async type): the rightmost name token (`File` in `System.IO.File.ReadAllText`, `Task` in
   `Task<int>`). Every position counts: member signatures and bodies, base lists, type arguments, `typeof`, casts,
   `new T(…)`, static member access.
2. A use without a written type name, on an expression that is **unanchored**:
   - a member of such a type (call, property, field, event, method group, extension method, `using static`
     member): the member name;
   - a target-typed `new(…)` that creates such a type: the `new` keyword;
   - an indexer of such a type: the whole element access;
   - an operator or user-defined conversion of such a type: the operator token, the opening parenthesis of an
     explicit cast, or the operand for an implicit conversion;
   - an expression of such a type used as the operand of `foreach`, `using` or `await`, as an argument, as the
     target of a delegate call, or as a returned value: that expression.

An expression is **anchored** when it, or a receiver inside it, is already reported by this rule; when it reads a
parameter, local, field or property whose type syntax carries a reported name; or when it reads a local whose
every assignment is an anchored expression (`var r = new StreamReader(path); r.ReadLine();` reports `StreamReader`
once). The target type of a target-typed `new` also anchors it when that type is written and reported in core. An
anchored expression adds nothing.
Attribute lists, `using` directives, `nameof` operands and documentation comments lie outside the rule.

FP0601 reports `dynamic`, so a call bound at run time is already marked at its source.

## Conventions

- One analyzer per rule: `ancplua-fprog/Rules/Fp####<Name>Analyzer.cs`. Shared models live in
  `ancplua-fprog/Shared/`: `CoreScope`, `Purity`, `EntryRule`, `UnionModel`, `Mutators`, `LocalAliases`,
  `MutationRoots` and `PartialUseGuards`.
- Descriptors come from `FpDescriptors.Create(id, title, messageFormat)`: category `Functional`, severity `Warning`,
  enabled by default. The message tells the reader what to write instead.
- `ancplua-fprog/AnalyzerReleases.Unshipped.md` lists exactly the IDs of the catalog.
- Analyzers run concurrently and skip generated code (`EnableConcurrentExecution`, `ConfigureGeneratedCodeAnalysis`).
  A rule that needs the whole compilation builds its model once in `RegisterCompilationStartAction`.
- Tests: `ancplua-fprog.Tests/Rules/Fp####Tests.cs`, one class per rule, methods named
  `FP####_<What>_Reports` or `FP####_<What>_NoReport`, run through
  `FpAnalyzerTestHost.VerifyRuleAsync("FP####", source, cancellationToken)` with markup `{|FP####:span|}`. Each
  form in a catalog row has a `_Reports` test, and each exemption and each neighbouring correct form has a
  `_NoReport` test. Test sources are C# 15 and parse with `LanguageVersion.CSharp15`; a test asks for `Preview`
  only to reach the memory-safety preview. Unions are `union` declarations and `closed` records. Every rule that
  reads a union or a core type has a test where that type comes from a referenced assembly.
- Demo: every case is a pair `demo/UnhappyPath/<Rule>_<Case>.cs` (each reported line ends in `// expect: <ID>`) and
  `demo/HappyPath/<Rule>_<Case>.cs` (the same job done correctly, silent under every rule).
  `demo/KnownGaps/` holds real bugs that still pass, each with its reason in `demo/FINDINGS.md`.
- Spawn as many subagents as useful.

## Boundaries

What the rules leave to the author, stated so nobody mistakes it for coverage:

- Delegates, interface calls and type parameters hide their target: `Func<…>` values, `IEnumerable<T>`
  implementations and generic arguments are trusted.
- FP0404 looks at expressions that name the union. Array elements and unassigned fields of a union struct hold the
  empty value too.
- FP0302 guards are syntactic. FP0303 (b) relies on nullable annotations.
- A record's copy constructor is protected by the language (CS8878), so a record union can be copied by a subtype
  that FP0403 reports in its own project.
