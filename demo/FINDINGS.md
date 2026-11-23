# Audit of the analyzers: bugs that passed, what was changed, what is still open

Baseline: commit `8a17c77` (branch `pins-11.0.100`), 1236 visible tests, `make check` green.
Result: 12 changes, 1350 visible tests, `make check` green (build, tests, InspectCode 0 findings, RefTrace).

## How the audit worked

1. Code with a real bug was written for four kinds of failure: a plain logic error, an effect at the edge to the
   outside world, order and concurrency, and a wrong assumption about the platform. Each file was run through all
   analyzers, one compilation per file.
2. Three independent reviewers tried to refute every claim. They re-ran the analyzers, showed the bug at run time
   (exception, different result on the second call, corrupted dictionary under 8 threads) and attacked each proposed
   fix with correct code that it would wrongly report.
3. Only what survived was fixed, by three agents (Claude Opus 5.5), tests first, one commit per change. The fixes
   were then run against the reviewers' attack files and against correct functional code.

No analyzer crash (AD0001) appeared in any run, before or after.

**The main result is not a list of slips.** Every hole below was already known to the analyzers' author: an existing
visible test encoded it as "not reported", because the catalog row in `AGENTS.md` says so literally or names no span
for it. The changes below replace those decisions. They are therefore decisions for you, and each one is a separate
commit so that you can drop it.

## Changed (each is one patch in `patches/`, in this order)

| # | Rule | Bug that passed before | Reported now, at | Verified at run time |
| --- | --- | --- | --- | --- |
| 1 | FP0302 | `map[key]` on `ImmutableDictionary`, `FrozenDictionary`, `ImmutableSortedDictionary`, `ReadOnlyDictionary`, `SortedDictionary`, `ConcurrentDictionary`, `SortedList`, builders: the dictionaries FP0203 tells you to use | the element access, for every dictionary declared in `System.Collections` or below | all throw `KeyNotFoundException` |
| 2 | FP0302 | `texts.Select(int.Parse)`, `Func<string,int> f = int.Parse`, `&int.Parse` | the method name in the method group | `FormatException` per element |
| 3 | FP0301 | `ArgumentNullException.ThrowIfNull(x)` and the other static `Throw*` helpers on exception types; the CA1510 to CA1513 code fixes rewrite `if (...) throw` into them | the method name | throws; the .NET 11 framework has exactly 13 such methods |
| 4 | FP0102 | `Guid.CreateVersion7()` (clock plus random bits), also the overload with a timestamp | the method name, like `NewGuid` | two calls, two values |
| 5 | FP0201 | `class Counter(int count) { int Next() => ++count; }`: a primary constructor parameter is a field that cannot be readonly | the parameter name in the primary constructor, once | `Next()` gives 6, 7 |
| 7 | FP0104 | `int.TryParse(text, out state.Count)`, `ref values[0]` | the argument expression (decided by the parameter being `ref`/`out`) | caller's object changed |
| 8 | FP0104 | `(first ? a : b).Value = 1`, `(a ?? b).Value = 1`, a switch-expression receiver | the assignment target | caller's object changed |
| 9 | FP0104 | `var same = order; same.Total = 0;`, a local holding a member, a `foreach` variable, a pattern variable | the assignment target | caller's object changed |
| 10 | **FP0105 (new rule)** | a `static readonly Dictionary` cache, a `readonly List` field with `Add`, `_totals[0]++`, `AsyncLocal.Value = ...`: readonly fields whose content changes | the target, or the name of the mutating method | `Price("a", 10)` then `Price("a", 20)`: 12.0 twice; 8 threads corrupt the dictionary; `Add` gives 1 then 2 |
| 11 | FP0801 | `using static System.Console; WriteLine(x);`, `using static System.IO.File; ReadAllText(p)` | the member name | console output and file access in core |
| 12 | FP0801 | `var reader = Shell.Open(path); reader.ReadLine()`, `Shell.Open(path).ReadToEnd()`, `repo.Connection.Open()`, a target-typed `new(...)` for a parameter outside core | the member name (or `new`), where the shell type enters the chain | file, network and database access in core |

### Visible tests whose expectation changed

These tests said "not reported" (or "suppressed") on purpose. If your hidden tests contain the same expectation,
that rule loses its point in `make score`; run it with your referee folder before you keep a change.

| Patch | Old test name | New test name |
| --- | --- | --- |
| 1 | `FP0302_IndexersNotDeclaredOnTheDictionaryTypes_NoReport` | `FP0302_IndexersOfOtherSystemCollectionsDictionaries_Reports` |
| 2 | `FP0302_MethodGroupIsAReferenceNotACall_NoReport` | `FP0302_MethodGroupOfPartialMember_Reports` + `FP0302_NameOfPartialMember_NoReport` |
| 2 | `FP0302_ImmutableArrayOrDefaultAndOtherOperators_NoReport` (one line) | `FP0302_MethodGroupOfImmutableArrayOperator_Reports` |
| 2 | `FP0302_InvokingDelegateMadeFromMethodGroup_NoReport` | `FP0302_DelegateMadeFromMethodGroupReportedAtTheGroupNotAtItsInvocation_Reports` + `FP0302_InvokingDelegateParameter_NoReport` |
| 5 | `FP0201_MutatedPrimaryConstructorParameterHasNoDeclarator_NoReport` | `FP0201_PrimaryConstructorParameterWrittenByAMember_Reports` |
| 7 | `FP0104_RefAndOutArgumentsAreNotAssignments_NoReport` | `FP0104_RefAndOutArgumentsIntoParameterObject_Reports` |
| 11 | `FP0801_UsingStaticMethodAndEnumConstant_NoReport` | `FP0801_UsingStaticMethodAndEnumConstant_Reports` |
| 11 | `FP0801_UsingStaticMemberHasNoTypeNameToken_NoReport` | `FP0801_UsingStaticMemberOfForbiddenTypeWithoutTypeToken_Reports` + `FP0801_UsingStaticMemberOfAllowedType_NoReport` |
| 11 | `FP0801_UsingStaticMethodGroupNameofAndDynamicCall_NoReport` | `FP0801_UsingStaticMethodGroupAndDynamicCall_Reports` + `FP0801_UsingStaticMethodNameof_NoReport` |
| 11 | `FP0801_UsingStaticPropertyFieldEventAndNameof_NoReport` | `FP0801_UsingStaticPropertyFieldAndEvent_Reports` + `FP0801_UsingStaticPropertyNameof_NoReport` |
| 11 | `FP0801_VarWithForbiddenInferredTypeNoTypeToken_NoReport` | `FP0801_VarWithForbiddenInferredTypeFromShellReportsMember_Reports` |

Patches 3, 4, 8, 9 and 10 changed no existing test. 114 tests were added.

### What each change deliberately does not report

- **1:** an indexer of a user type that implements a dictionary interface (it may be total), `JsonObject`, `ILookup`,
  `KeyedCollection`, non-generic dictionaries.
- **2:** `nameof(int.Parse)`; invoking the delegate later. A group passed to a wrapper that catches the exception is
  reported, as the lambda form already was.
- **3:** instance helpers (`CancellationToken.ThrowIfCancellationRequested`), `ExceptionDispatchInfo.Throw`, `Debug.Fail`,
  helpers on types that are not exceptions. A user method `MyException.ThrowX` that never throws is reported.
- **5:** a parameter written only in an initializer or in the base-constructor arguments (plain reassignment during
  construction, nothing is captured); records; a `ref` local bound to the parameter.
- **7:** a `ref` argument for a `ref readonly` parameter, `in` arguments, `out var`, the method's own `out` parameter.
- **9:** a local whose value comes from a method call (`values.ToArray()`, `source.ToList()`), `new`, `with`; a local
  that is assigned again; a struct copy; a boxed copy. Still open: `ref` locals, spans over a parameter's array,
  locals assigned after their declaration, deconstructed locals, nested pattern variables, `out var` results.
- **10:** everything rooted in a local or a parameter; a constructor or initializer filling its own fields; assigning
  the field itself (FP0201, FP0102); reads; immutable collections. A memo such as `Memo.GetOrAdd(...)` on a static
  `ConcurrentDictionary` IS reported: decide whether you want that. Still open: a field reached through a local alias,
  properties with `set`, extension methods, event subscriptions.
## Not changed: real bugs that still pass (`KnownGaps/`)

Each needs a decision about the catalog, or cannot be decided by a rule of this kind.

| File | What passes | Why it was left |
| --- | --- | --- |
| `Fp0303_NullThroughALocal.cs` | `string? result = null; ...; return result;` | Needs data flow; a constant holding null is encoded as not reported. |
| `Fp0302_OtherPartialMembers.cs` | `ParseExact`, `Convert.ToInt32`, `Max()`/`Average()` on an empty sequence, `xs[0]`, division, a hard cast | Outside the row's list; `ParseExact` and `BigInteger.Parse` are encoded as not reported. |
| `Fp0203_ImmutableBuilderMember.cs` | a record holding `ImmutableArray<T>.Builder` (a `with` copy shares it) | The row allows the whole namespace, and the test is named "Decided". |
| `Fp0101_VoidDelegates.cs` | `Action<T>` taken, called and returned | The row names methods and local functions. |
| `Fp0601_CastThroughObject.cs` | `(string)(object)value`, a type test through an `object` local | Outside the row's list. |

Not in a file, same kind: a mutating call on a parameter's collection (`items.Add(x)`, `Array.Sort(values)`), which
FP0104 does not treat as an assignment and FP0105 leaves alone, because accumulator helpers such as
`PrintMembers(StringBuilder)` are correct code.

## Claims of the audit that the reviewers corrected

- "FP0302 reports `IImmutableDictionary` inconsistently": wrong. It declares no indexer of its own and binds to
  `IReadOnlyDictionary`, which is consistent.
- "an indexer that implements the interface indexer" as the fix: wrong. `FrozenDictionary` has a public
  `ref readonly` indexer that implements neither; the fix uses the namespace and the key parameter instead.
- "`Aggregate` on an empty `ImmutableArray` throws": wrong, it returns 0. Removed from the examples.
- "CS8509 cannot be removed for `Circle { Radius: > 0 }` + `Circle { Radius: <= 0 }` without a catch-all": wrong.
  `Circle { Radius: > 0 } => 1, Circle => 2` is suppressed. No dead end exists.
- The first design for FP0801 (report only where the member has no FP0801 yet) was shown to add second diagnostics,
  to depend on unrelated members and to be non-monotonic. It was replaced before any code was written.
- A record member of type `...Builder` was planned as a fix and dropped: the row allows it literally.

## Keeping, splitting or dropping the changes

The changes are in the working tree, not committed. `patches/` holds the same changes as 12 commits.

```sh
# see everything
git status && git diff

# turn the working tree into the 12 commits (first put the tree back, then apply the patches)
git checkout -- ancplua-fprog ancplua-fprog.Tests
rm ancplua-fprog/Rules/Fp0105FieldStateChangeAnalyzer.cs ancplua-fprog/Rules/Fp0801ShellUses.cs \
   ancplua-fprog/Shared/LocalAliases.cs ancplua-fprog/Shared/UnionModel.Closed.cs \
   ancplua-fprog.Tests/Rules/Fp0105Tests.cs
git am demo/patches/*.patch

# drop one change: leave its patch out of `git am` (7, 8, 9 and 10 build on each other in that order; 12 builds on 11)

# drop everything: the first two commands above, then delete demo/
```
