# Work order: rebuild ancplua-fprog to the catalog in AGENTS.md

You are in the repository `ancplua-fprog`, branch `pins-11.0.100`, with an uncommitted working tree.

## The situation

- `AGENTS.md` describes the product **as it shall be**: scope, purity classes, rule catalog, exact spans. It is the
  target and the single source of truth.
- The code implements an older catalog: unions marked with `[Fprog.Union]`, closedness imitated with private
  constructors, effects found through block-lists of type names. Those three models are the root of every open gap.
- `demo/FINDINGS.md` is the audit of that older state. Three parts of it are your input:
  the table "Not changed: real bugs that still pass", every "Still open" sentence under "What each change
  deliberately does not report", and the paragraph "Not in a file".
- `demo/KnownGaps/*.cs` holds one compilable file per open bug. Today each of them builds with 0 diagnostics.
- `python3 demo/count.py` builds `demo/` and compares every diagnostic with the `// expect: <ID>` comments.

## Your mandate

Make the code, the tests, the demo and the docs match `AGENTS.md`, completely.

You have full write access to every file in the repository, including the test host, the fixtures and the Makefile.
Replace a model as a whole, and delete what the new model makes unnecessary in the same commit: analyzers, helpers,
tests, fixtures, comments, demo files. Git history is the archive; the working tree holds the new design only.
Old tests that say `_NoReport` for a bug the catalog now reports become `_Reports` tests.

Work in `ancplua-fprog/`, `ancplua-fprog.Tests/`, `demo/`, `docs/`, `AGENTS.md` and the build files. `fprog-kit/` is
a frozen copy of the earlier kit and belongs to the owner, as do the Makefile targets `protected` and `score`.

## Step 0: baseline

```sh
git switch -c fprog-v2
git add -A
git commit -m "baseline: audit fixes, Rider cleanup, demo, v2 playbook"
make tools && make check          # expected: exit 0, 1350 tests
python3 demo/count.py             # expected: RESULT: everything as expected (UnhappyPath 40 warnings)
```

These two results are your reference point; put the numbers you saw into the final report.
Every commit of this work lands on `fprog-v2`, locally. The owner pushes.

## The loop, once per package

1. **Tests first.** For each catalog row of the package write the `_Reports` tests (one per form in the row) and the
   `_NoReport` tests (each exemption, each neighbouring correct form), with `{|FP####:span|}` markup at the exact
   span of the **At** column. Run them and see them fail.
2. **Build the model, then the rule on top of it.** Shared knowledge goes into `ancplua-fprog/Shared/`, one file
   per model as named in `AGENTS.md`, "Conventions".
3. **Delete what the model replaces.**
4. **Gate:** `make check` exits 0.
5. **Demo:** turn each `KnownGaps` file of the package into a pair. `UnhappyPath/<Rule>_<Case>.cs` is the bug with
   `// expect: <ID>` at the end of every reported line. `HappyPath/<Rule>_<Case>.cs` does the same job correctly and
   is silent under **all** rules. Delete the `KnownGaps` file. `python3 demo/count.py` ends with
   `RESULT: everything as expected`.
6. **Records:** delete the closed rows and sentences from `demo/FINDINGS.md`. Where the implementation showed that a
   row in `AGENTS.md` needs sharper wording, rewrite that row, so file and code agree at every commit.
7. **Commit:** `v2(<n>): <package title>`.

Order: 1, 2, 3, 4 in sequence. Packages 5, 6 and 7 are independent of each other and of 2 to 4; with subagents, give
each one package and its own git worktree, merge in numeric order and run both gates after every merge.

Verify a Roslyn API by reading its signature with `dotnet tool run ref assembly …` (command in `AGENTS.md`) before
you build on it. The section "C# 15 facts the rules build on" lists what was observed with this SDK.

---

## Package 1: unions are C# 15 unions

Catalog rows: Scope ("union", "cases", "union value"), FP0401, FP0403, FP0404.

Build:

- `ancplua-fprog.csproj` and `ancplua-fprog.Tests.csproj`: add `<NoWarn>$(NoWarn);RSEXPERIMENTAL006</NoWarn>`.
- `Shared/UnionModel.cs`, written new on the compiler's own knowledge: `ITypeSymbol.IsUnion` with the public
  one-parameter constructors for `union` types, `ITypeSymbol.IsClosed` with
  `GetClosedDerivedTypeInfo(ct).ClosedDerivedTypes` for closed classes. It answers: the union of a type, its cases,
  its leaf cases (a closed case stands for its own cases), whether an expression is a union value (through
  `Nullable<T>`, type-parameter constraints and casts at the head of `switch`/`is`/`as`), and which cases a pattern
  accepts.
- `FP0401` (open class in core), `FP0403` (case neither `sealed` nor `closed`), `FP0404` (empty union value:
  `IDefaultValueOperation` or an `IObjectCreationOperation` with the implicit parameterless constructor, whose type
  is a union struct).
- `[Fprog.FunctionalCore]` gains the targets `Interface`, `Enum` and `Delegate` in `FpAnalyzerTestHost` and in
  `demo/Fprog.Annotations.cs`.

Delete:

- `Fprog.UnionAttribute` everywhere: `FprogAttributes.Union`, the attribute source in the test host and in
  `demo/Fprog.Annotations.cs`, every `[Fprog.Union]` in tests, fixtures and demo.
- `Rules/Fp0402UnionConstructorAnalyzer.cs` with `Fp0402Tests.cs`.
- `Rules/Fps001UnionSwitchSuppressor.cs` with `Fps001Tests.cs`, `VerifySuppressionAsync` in the test host and
  `Shared/UnionSwitchCoverage.cs` with its tests.
- `Shared/UnionModel.Closed.cs`, `Shared/UnionModel.Coverage.cs` and the namespace walk that searched cases across
  assemblies: the compiler lists the cases now.
- The old `Fp0401AbstractUnionAnalyzer` and `Fp0403SealedNestedCaseAnalyzer`, replaced by the new rows.

Rewrite every test source that declares a union as C# 15: `public closed record Shape;` with
`public sealed record Circle(double Radius) : Shape;`, or `public union Pet(Cat, Dog, Bird);`.

Demo pairs: rewrite `Fps001_UnionThatIsNotClosed` as `Fp0401_OpenClassInCore` (unhappy: `abstract record` with a
private constructor and nested cases; happy: `closed record` with sealed cases). Add `Fp0403_CaseLeftOpen` and
`Fp0404_EmptyUnion` (unhappy: `default` and `new Pet()` reach a complete switch; happy: every value is built from a
case).

## Package 2: matching on unions

Catalog rows: FP0501, FP0502, FP0503, section "Handling a case".

Build:

- `FP0501` on `IPatternOperation.InputType`: every pattern position whose input is a union value, at any depth, in
  switch expressions and switch statements; plus a top-level catch-all over a tuple with a union element; plus the
  governing expression seen through casts (`((object)shape) switch { … }` is a switch over `Shape`).
- `FP0502` for switch statements, with the handling rule of the catalog. Switch expressions belong to the compiler
  (CS8509, CS8655); add tests that pin both compiler warnings for a missing case and a missing `null`.
- `FP0503` for `is` with a type and for `as` on a union value. For `x is Cat` take the span from the syntax: the
  type pattern operation is implicit and spans the whole expression.

Close from `demo/KnownGaps`: `Fp0501_TupleUpcastAndNativeUnion.cs` (tuple `_`, cast to `object`, native `union`
with `_`), `Fp0502_SwitchStatementAndIfChain.cs` (statement missing a case, `case not Card:`, the `is` chain).
Pairs: `Fp0501_CatchAllInTuple`, `Fp0501_CatchAllBehindCast`, `Fp0501_NativeUnionCatchAll`,
`Fp0502_SwitchStatementMissesCase`, `Fp0501_NegatedCase`, `Fp0503_IsChain`.

## Package 3: the core boundary

Catalog rows: section "Purity classes", FP0801, FP0802, FP0803, section "Entry rule".

Build:

- `Shared/Purity.cs`: one function that classifies a type as Core, Async, Pure or Shell, in that order, from the
  lists in `AGENTS.md` and the config key `fprog_pure_apis`. One table, one test per list entry kind (namespace,
  namespace with subtree, single type, excluded type, config entry).
- `FpAnalyzerTestHost`: an overload of `VerifyRuleAsync` that takes analyzer config text, so tests can set
  `fprog_pure_apis`.
- `FP0801` and `FP0802` as two rule IDs over one implementation of the entry rule, differing in the class they
  report. Every form of the entry rule gets its `_Reports` test and its anchored twin as `_NoReport`.
- `FP0803` as a symbol action on named types outside core.

Delete: every block-list of namespaces and type names in `Fp0801ImperativeShellAnalyzer` and `Fp0801ShellUses`; the
return-type logic of `Fp0802TaskReturnAnalyzer`. Tests that used a non-core helper type inside core as neutral
scenery now mark that type core, or expect FP0801.

Close from `demo/KnownGaps`: `Fp0801_CoreCallsShell.cs` (core calls `Database.Save`; core dispatches into
`TodayRule : Rule`), `Fp0801_ConcurrencyInCore.cs` (`Task.Run(…).Result`, `AsParallel`, `ReaderWriterLockSlim`,
`AsyncLocal`, `ThreadPool`), `Fp0802_BlockingOnTask.cs` (`.Result`, `GetAwaiter().GetResult()`,
`async IAsyncEnumerable`), `Fp0102_StaticMutableProperty.cs` (arrives as FP0801 on the non-core type).
Close from FINDINGS, changes 11 and 12, "Still open": indexers, operators, `foreach` and `using` of a shell object,
calls through a shell delegate, a shell object passed straight on, a shell type that arrives through inference.
Pairs: `Fp0801_CoreCallsNonCoreCode`, `Fp0803_ShellTypeDerivesFromCore`, `Fp0801_ConcurrencyPrimitives`,
`Fp0802_TasksInCore`, `Fp0801_ShellValueWithoutTypeName`, and `Fp0801_TrustedLibrary` with a `demo/.globalconfig`
that lists one demo namespace under `fprog_pure_apis` (happy: core uses that namespace; unhappy: core uses a sibling
namespace that the list leaves out).

## Package 4: hidden inputs and culture

Catalog rows: FP0102 with its list, FP0106 with its table.

Build:

- `FP0102` from the list: clock, local time zone, randomness, current culture as a value, hash codes (exempt inside
  a `GetHashCode` override), static state including static properties with `set`.
- `FP0106` (new rule): each of the five forms in the table, with the culture-free types as `_NoReport` tests, and
  `string.Create(CultureInfo.InvariantCulture, $"…")`, `FormattableString.Invariant($"…")` and
  `value.ToString(CultureInfo.InvariantCulture)` as the silent forms.

Delete: the handling of `Random`, `Environment` and `TimeProvider` in the FP0102 analyzer and its tests; these are
Shell types and their tests move to FP0801 with the new ID and span.

Close from `demo/KnownGaps`: `Fp0102_ClockAndRandomnessNotInTheList.cs` (injected `TimeProvider`, `Stopwatch`,
`RandomNumberGenerator` as FP0801; `TimeZoneInfo.Local`, `ToLocalTime`, `CultureInfo.CurrentCulture`,
`string.GetHashCode()` as FP0102; `ToString("C")` as FP0106).
Pairs: `Fp0102_LocalTimeZone`, `Fp0102_HashCodeAsData`, `Fp0801_InjectedClock`, `Fp0106_FormattingByCulture`,
`Fp0106_SortingStringsByCulture`.

## Package 5: totality

Catalog rows: FP0301, FP0302 with its table and the guard rule, FP0303.

Build:

- `FP0301`: add calls to members annotated `[DoesNotReturn]`.
- `FP0302`: every row of the table, as a call and as a method group; the guard rule as one helper with tests for
  each guard shape (enclosing `if`, `while`, `for`, `?:`, `when`, `&&`, `||`, and a preceding `if` in the block).
- `FP0303`: (b) each `?` in the return type of a method or local function, including tuple elements and type
  arguments; (c) the `!` operator.

Close from `demo/KnownGaps`: `Fp0302_OtherPartialMembers.cs` (`ParseExact`, `Convert.ToInt32`, `Max()`,
`Average()`, `values[0]`, `a / b` as FP0302; `(T)value` from `object` as FP0601 in package 7),
`Fp0303_NullThroughALocal.cs` (null through a local, through a constant, inside a tuple).
Close from FINDINGS: change 1 (`KeyedCollection` by key), change 3 (instance throw helpers: `[DoesNotReturn]`
members here, `CancellationToken`, `ExceptionDispatchInfo`, `Debug` as Shell types in package 3).
Pairs: `Fp0302_ParseExactAndConvert`, `Fp0302_EmptySequence`, `Fp0302_ElementByPosition`, `Fp0302_Division`,
`Fp0302_Downcast`, `Fp0303_NullableReturnType`, `Fp0301_DoesNotReturn`.

## Package 6: mutation

Catalog rows: FP0104, FP0105, FP0107, FP0201, FP0203, section "Reached from a parameter, reached from a field,
mutating call".

Build:

- `Shared/Mutators.cs`: the table of mutating members, moved out of the FP0105 analyzer, extended by the
  `CollectionExtensions` methods, the `Span<T>` writers and the static writers with their destination argument.
- `Shared/LocalAliases.cs`, rebuilt to the "reached from" definition: `ref` locals, locals with several
  assignments, deconstruction variables, pattern variables at any depth, `foreach` variables, spans and memories
  made from something reached, elements handed out by a call (`First()`, `Find(…)`, `out var` of `TryGetValue`).
- `FP0104`: mutating calls on something reached from a parameter; `Span<T>` and `Memory<T>` parameters.
- `FP0105`: the same model with a field as root, extension-method mutators, `+=`/`-=` on events.
- `FP0201`: a write to a primary-constructor parameter through a `ref` local.
- `FP0203`: `Builder` types of immutable collections, `Memory<T>`, `ArraySegment<T>`.
- `FP0107` (new rule): pointer and function-pointer types, `&`, `fixed`, `unsafe` as modifier, block and expression.

Close from `demo/KnownGaps`: `Fp0203_ImmutableBuilderMember.cs`. Close from FINDINGS: "Not in a file"
(`items.Add(x)`, `Array.Sort(values)` on a parameter), the "Still open" lists of changes 9 and 10. Close the unsafe
findings of 2026-11-23: `*target = 42` and `target[0] = 42` through a pointer parameter, `fixed (int* first =
values) first[0] = 7`, `unsafe(*&value)`, a pointer declaration outside an `unsafe` block.
Pairs: `Fp0104_MutatingCallOnParameter`, `Fp0104_SpanOverParameter`, `Fp0104_ElementHandedOutByCall`,
`Fp0105_FieldBehindAlias`, `Fp0105_EventSubscription`, `Fp0203_BuilderInRecord`, `Fp0107_PointersInCore`.

## Package 7: signatures and runtime types

Catalog rows: FP0101, FP0601.

Build:

- `FP0101`: (b) delegate declarations, (c) written `void` delegate types, (d) lambdas, anonymous methods and method
  groups that become a `void` delegate whose type is unwritten.
- `FP0601`: operands of static type `object` and `dynamic` and every type parameter except union values; explicit
  downcasts and unboxing casts; the `dynamic` type itself; the exemption for `object` parameters of overriding and
  implementing members.

Close from `demo/KnownGaps`: `Fp0101_VoidDelegates.cs` (`Action<T>` taken, called, returned),
`Fp0601_CastThroughObject.cs` (`(string)(object)value`, a type test through an `object` local).
Pairs: `Fp0101_VoidDelegates`, `Fp0601_CastThroughObject`, `Fp0601_Dynamic`.

---

## Finish

1. **C# 15 pins.** One test class `CSharp15SyntaxTests` that runs every analyzer over core code using each new
   construct and asserts the exact diagnostics: an extension indexer (declared and used), collection expression
   arguments `[with(StringComparer.Ordinal), .. values]`, labeled `break outer` and `continue outer`, a `union`
   declaration with members, a nested closed hierarchy, and FP0802 on the same source with the parse feature
   `runtime-async=on` and without it.
2. **Docs.** Rewrite `docs/ROSLYN.source.md` as the map from the catalog to the Roslyn APIs you used. Rewrite the
   first line of `docs/SPEC.source.md` to point at `AGENTS.md` as the source of truth.
3. **Release tracking.** `ancplua-fprog/AnalyzerReleases.Unshipped.md` lists exactly the catalog IDs with their
   new titles.
4. **Demo.** `demo/KnownGaps/` holds only bugs that still pass, each with one row in `demo/FINDINGS.md` that gives
   the reason. `demo/FINDINGS.md` is reduced to that list. Delete `demo/patches/`. Regenerate the tables in
   `demo/README.md` from `python3 demo/count.py --json`.
5. **Gates.** `make check` exits 0. `python3 demo/count.py` ends with `RESULT: everything as expected`, with
   HappyPath at 0 errors, 0 warnings, 0 infos.
6. **Final commit:** `v2: catalog complete`.

## Report

Answer with:

- one table: package, rule IDs, tests added / rewritten / deleted, `KnownGaps` files closed, files deleted;
- the demo numbers per project (errors, warnings, infos) and the last line of `count.py`;
- the output line of `make check` and the total test count;
- every row of `AGENTS.md` you reworded, old meaning against new meaning, with the reason;
- everything that is still open, with the file in `demo/KnownGaps/` that shows it and the reason it stays.
