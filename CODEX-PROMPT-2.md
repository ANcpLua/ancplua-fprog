# Work order 2: Roslyn 5.12.0, caller-owned memory, anchoring

You are in the repository `ancplua-fprog`, branch `fprog-v2`, at the commit `v2: catalog complete`. The working
tree holds two uncommitted files: an updated `AGENTS.md` and this work order.

## The situation

- `AGENTS.md` is the target, as before. It changed in six places since your last commit: the Environment section
  (Roslyn 5.12.0, experimental APIs, language version), the facts table (union case types, the generic closed
  switch, `IsExhaustive`, pointers, driver behaviour), the definition of a union's cases in Scope, the first bullet
  of "Reached from a parameter" (memory the caller owns), the anchor paragraph of the entry rule, and the test
  conventions. `git diff AGENTS.md` shows each change.
- Roslyn 5.12.0 is on NuGet and is the compiler inside SDK 11.0.100. Until now the analyzers compiled against
  an earlier Roslyn release and the test host used that compiler, while real builds ran inside 5.12.0. This work order
  puts all three on the same version.
- An independent check of your last result found the catalog implemented faithfully on about a hundred probed
  forms, and two places where the rules and the intent differ. Packages B and C close them.

Measured on a copy of this branch, so you know what to expect:

| Step | Result |
| --- | --- |
| The three package pins moved to 5.12.0, nothing else changed | `make check` exits 0 with 1,031 tests; the demo ends with `RESULT: everything as expected` |
| The project-wide `RSEXPERIMENTAL006` suppression removed | exactly two errors, both in `Rules/Fp0107UnsafeCodeAnalyzer.cs` (lines 29 and 60: `SyntaxKind.UnsafeExpression`, `UnsafeExpressionSyntax`) |
| A syntax node action over a setter and a `throw` inside a union body | The earlier compiler delivers each node three times (member, union type, namespace). 5.12.0 delivers it once. |
| A syntax node action on `Parameter` over a partial constructor | both versions deliver the implementing part only |
| `ITypeSymbol.UnionCaseTypes` | `[Cat, Dog, Bird]` for `union Pet`, `[int, string]` for `Generic<int>` of `union Generic<T>(T, string)` and for a hand-written `[Union]` class, empty for closed classes and for `Pet?` |
| `s switch { … }` over `T s where T : Closed` with every case named | no CS8509 from the SDK compiler |

## Your mandate

The same as before. Make code, tests, demo and docs match `AGENTS.md`. Replace a model as a whole and delete what
the new one makes unnecessary in the same commit. Git history is the archive. Where the implementation shows that
a row needs sharper wording, rewrite the row.

## Step 0: baseline

```sh
git rm -q CODEX-PROMPT.md                    # the first work order; it is finished
git branch -D codex/fprog-v2-package6        # leftover branch of the first run
git add -A
git commit -m "baseline: playbook 2.1 and work order 2"
make tools && make check          # expected: exit 0, 1,031 tests
python3 demo/count.py             # expected: RESULT: everything as expected
```

Put the numbers you saw into the final report. Every commit lands on `fprog-v2`, locally. The owner pushes.

Work in `ancplua-fprog/`, `ancplua-fprog.Tests/`, `demo/`, `docs/`, `AGENTS.md` and the build files. `fprog-kit/`
is a frozen copy of the earlier kit with its own old `AGENTS.md`; it belongs to the owner, as do the Makefile
targets `protected` and `score`. This file, `CODEX-PROMPT-2.md`, is the only work order.

## The loop, once per package

Tests first at the exact spans, then the model, then the rule, then delete what the model replaces, then
`make check`, then the demo pair and `python3 demo/count.py`, then commit as `v2.1(<letter>): <package title>`.
Run package A first; B, C and D are independent of each other and can run as subagents in their own worktrees,
merged in order with both gates after every merge.

---

## Package A: one Roslyn version, 5.12.0

1. **Pins.** `Microsoft.CodeAnalysis.CSharp` and `Microsoft.CodeAnalysis.Analyzers` 5.12.0 in
   `ancplua-fprog.csproj`; `Microsoft.CodeAnalysis.CSharp.Workspaces` 5.12.0 in `ancplua-fprog.Tests.csproj`.
2. **Suppression.** Delete `<NoWarn>$(NoWarn);RSEXPERIMENTAL006</NoWarn>` from both projects. Put one
   `#pragma warning disable RSEXPERIMENTAL006` with its reason at the top of `Fp0107UnsafeCodeAnalyzer.cs` and of
   any test file that names an API from the experimental list in `AGENTS.md`.
3. **Union cases.** `Shared/UnionModel.cs` takes the cases of a union from `ITypeSymbol.UnionCaseTypes`. Delete the
   constructor scan. Add tests for a generic union, a hand-written `[Union]` class and `Nullable<T>` of a union.
4. **Driver filters.** `Fp0202SetAccessorAnalyzer` and `Fp0301ThrowAnalyzer` return early when
   `context.ContainingSymbol is INamedTypeSymbol or INamespaceSymbol`, to drop the earlier compiler's repeated passes. Delete
   that filter and its comment in both, and in every other analyzer that carries the same pattern. Pin the new
   behaviour with tests: a setter and a `throw` inside a union body give exactly one diagnostic each.
5. **Language version.** `FpAnalyzerTestHost` and `Shared/TestCompilation.cs` parse with
   `LanguageVersion.CSharp15`. Add one explicit way for a test to ask for `Preview` together with the feature
   `updated-memory-safety-rules`. FP0107 gets tests in both modes: C# 15 with classic `unsafe` code, and the
   preview with a pointer declaration and `unsafe(expr)` outside a block.
6. **Generic closed switch.** Tests for FP0501 and FP0502 over `T s where T : Closed`: a switch that names every
   case is clean, a `_` arm is reported, a switch statement that misses a case is reported. One test pins that the
   compiler gives no CS8509 for the complete switch expression.
7. **Demo.** The demo projects build with the default language version; remove `LangVersion` from
   `demo/Directory.Build.props`. `Fp0107_PointersInCore` shows classic unsafe code.
8. **Text.** Rewrite `docs/ROSLYN.source.md` for 5.12.0. Re-verify every code comment that names the earlier Roslyn pin
   on 5.12.0 and state what is true now. `grep -rn "5\.9\.0"` over the tracked files ends empty.

## Package B: memory the caller owns (FP0104, FP0105)

Today a write is reported only when the parameter itself is a reference, a span or a memory. A reference that
sits inside a value-type parameter passes: all four of these compile in core with 0 diagnostics.

```csharp
static int A(ImmutableArray<Order> orders) { orders[0].Lines.Add(1); return 1; }
static int B(ImmutableArray<Order> orders) { orders[0].Raw[0] = 1; return 1; }
static int C((Order a, Order b) pair)      { pair.a.Lines.Add(1); return 1; }
static int D(Pair pair)                    { pair.First.Raw[0] = 1; return 1; }   // readonly record struct Pair(Order First, Order Second)
```

Build the definition from `AGENTS.md`: the write is the caller's when the path from the parameter to the written
location passes through a reference, at the parameter or at any field, property or element read on the way. Use
the same path walk for FP0105 with a field as the root, so both rules share one piece of code.

Tests, `_Reports`: the four forms above; a pattern variable taken from a `Nullable<Pair>` parameter; an element
handed out by a call on an `ImmutableArray<Order>` (`orders.First().Lines.Add(1)` through a local); an extension
indexer whose setter writes the receiver's array (`extension(Grid grid) { public int this[int i] { set =>
grid.Cells[i] = value; } }`) and its use `g[1] = 7` on a parameter `g`. Tests, `_NoReport`: `pair.First = other`
on a by-value tuple or struct; a write into a local struct copy's own field; every existing `_NoReport` form.

Demo pair: `Fp0104_ReferenceInsideValueParameter`.

## Package C: one report per shell value (FP0801, FP0802)

Today a `var` copy of a value that is already reported gets reported again at every use.

```csharp
static string A(string p) { var r = new StreamReader(p); return r.ReadLine() + r.ReadLine(); }   // 3 diagnostics, 1 wanted: StreamReader
static string B(StreamReader r) { var c = r; return c.ReadLine() + c.ReadLine(); }              // 3 diagnostics, 1 wanted: StreamReader
static async Task<int> C(Task<int> t) { var x = t; return await x + await x; }                  // 4 diagnostics, 2 wanted: both written Task names
```

Build the anchor rule from `AGENTS.md`: a local is anchored when each of its assignments is an anchored expression.
Follow chains of such locals to any length; a local with one unanchored assignment is unanchored.

Tests, `_NoReport` beyond the written type name: the three forms above; a chain `var a = r; var b = a; b.ReadLine()`;
a local assigned twice from anchored expressions; a `foreach` variable over an anchored collection; a pattern
variable taken from an anchored value. Tests, `_Reports`: a `var` local whose value comes from a pure member that
returns a shell type, at its first member use.

Update the expected counts of the FP0801 and FP0802 demo files; state old and new count per file in the report.

## Package D: types from a referenced assembly

Add to the test host one helper that compiles a second source into a metadata reference, then these tests. The
rules are expected to hold as they are; where a test shows a defect, fix the rule and name it in the report.

- A `union` and a `closed` hierarchy declared in the referenced assembly: FP0501, FP0502, FP0503 and FP0404 on
  values of those types in the consuming compilation.
- A subtype in the consuming compilation that derives from a case of the referenced closed class: FP0403 follows
  the catalog row.
- A core type and a core interface declared in the referenced assembly: used from core code it counts as Core for
  FP0801; a type outside core in the consuming compilation that implements that interface gets FP0803.

---

## Finish

1. `ancplua-fprog/AnalyzerReleases.Unshipped.md` still lists exactly the catalog IDs.
2. Regenerate the tables in `demo/README.md` from `python3 demo/count.py --json`.
3. `make check` exits 0 and `python3 demo/count.py` ends with `RESULT: everything as expected`.
4. Final commit: `v2.1: Roslyn 5.12.0`.

## Report

Answer with:

- one table: package, rule IDs touched, tests added / rewritten / deleted, files deleted;
- the Step 0 numbers and the final numbers: test count, demo counts per project, last line of `count.py`;
- for package A: every statement about the earlier Roslyn pin you re-verified, and what it says now;
- for package C: old and new diagnostic count per demo file;
- for package D: each test that exposed a defect, and the fix;
- every row of `AGENTS.md` you reworded, with the reason;
- everything still open, with the reason.
