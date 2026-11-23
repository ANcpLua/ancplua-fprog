# v2 catalog report

## Baseline

Commit `0305cb7` recorded the audit working tree on `fprog-v2`. Step 0 exited 0. The test run passed all
1,350 tests. The demo had 40 UnhappyPath warnings and zero diagnostics in HappyPath and KnownGaps.
The baseline demo ended with `RESULT: everything as expected`.

## Packages

The counts compare test method names and bodies in each package commit. Renamed methods appear as additions
and deletions. The executed suite totals count each data row separately.

| Package and commit | Catalog IDs | Tests added / rewritten / deleted | KnownGaps files closed | Files deleted |
| --- | --- | ---: | --- | --- |
| 1. Compiler-native unions (`08b0500`) | FP0401, FP0403, FP0404. Retired FP0402 and FPS001. | 29 / 14 / 292 | None | `Fp0402Tests.cs`, `Fps001Tests.cs`, `UnionSwitchCoverageTests.cs`, `Fp0401AbstractUnionAnalyzer.cs`, `Fp0402UnionConstructorAnalyzer.cs`, `Fp0403SealedNestedCaseAnalyzer.cs`, `Fps001UnionSwitchSuppressor.cs`, `UnionModel.Closed.cs`, `UnionModel.Coverage.cs`, `UnionSwitchCoverage.cs`, and both copies of `Fps001_UnionThatIsNotClosed.cs`. |
| 2. Matching on unions (`fc8cec4`) | FP0501, FP0502, FP0503 | 26 / 0 / 53 | `Fp0501_TupleUpcastAndNativeUnion.cs`, `Fp0502_SwitchStatementAndIfChain.cs` | Both KnownGaps files. |
| 3. The core boundary (`5aa73f1`) | FP0801, FP0802, FP0803 | 32 / 0 / 131 | `Fp0801_CoreCallsShell.cs`, `Fp0801_ConcurrencyInCore.cs`, `Fp0802_BlockingOnTask.cs`, `Fp0102_StaticMutableProperty.cs` | The four KnownGaps files, `Fp0801ImperativeShellAnalyzer.cs`, `Fp0801ShellUses.cs`, and `Fp0802TaskReturnAnalyzer.cs`. |
| 4. Hidden inputs and culture (`58b4e01`) | FP0102, FP0106, FP0801 | 18 / 0 / 56 | `Fp0102_ClockAndRandomnessNotInTheList.cs` | The KnownGaps file. |
| 5. Totality (`8828060`) | FP0301, FP0302, FP0303 | 45 / 38 / 8 | `Fp0302_OtherPartialMembers.cs`, `Fp0303_NullThroughALocal.cs` | Both KnownGaps files. |
| 6. Mutation (`8ed749b`) | FP0104, FP0105, FP0107, FP0201, FP0203 | 37 / 1 / 11 | `Fp0203_ImmutableBuilderMember.cs` | The KnownGaps file. |
| 7. Signatures and runtime types (`066159f`) | FP0101, FP0601 | 43 / 14 / 13 | `Fp0101_VoidDelegates.cs`, `Fp0601_CastThroughObject.cs` | Both KnownGaps files, `Fp0101VoidMethodAnalyzer.cs`, and `Fp0601TypeTestOnTypeParameterAnalyzer.cs`. |

Both gates passed at each package boundary. Finish added six C# 15 test methods with seven executions,
removed the 12 audit patches and the two duplicate work orders, and regenerated the demo tables from JSON.

## Final gates

`make check` exited 0 with 1,031 tests passed and zero InspectCode findings. Its last line was
`check passed`.

`python3 demo/count.py` exited 0.

```text
HappyPath: 0 errors, 0 warnings, 0 infos
UnhappyPath: 0 errors, 133 warnings, 0 infos
KnownGaps: 0 errors, 0 warnings, 0 infos

RESULT: everything as expected
```

The demo counter now fails on an unsuccessful build or build totals outside the checked demo files.

## Catalog wording

- **Environment.** The experimental API list named closed-type and union declarations. Adds `UnsafeExpressionSyntax` to document the same compiler diagnostic used by the new FP0107 implementation.
- **FP0101.** Named methods and local functions alongside delegates, with a general reference to unwritten target types. Names user-defined operators and their token. The unwritten-target rule uses the target type in core source as its boundary so the written type and conversion get one report.
- **FP0102 and hidden inputs.** Described hidden reads and introduced the list as members of Pure types. Names mutable static accesses explicitly, preserves method groups for ambient methods, and spells out the cross-type `GetHashCode` call. This covers state writes and calls whose binding is outside the Pure list.
- **FP0104.** Listed compound assignment and the other caller-memory writes. Includes event subscription in compound assignment because it writes the event on the reached object.
- **FP0105.** Listed field-rooted writes and event subscription in general. Field-rooted event receivers are handled through the shared model. Subscriptions to events on `this` and static events also affect the current type's state.
- **FP0201.** A primary-constructor parameter was reported for a write in a member body. Identifies direct writes and writes through a `ref` local because both target the captured parameter's storage.
- **FP0203.** Listed arrays and three collection namespaces. Includes `System.Array` explicitly and states that membership in those three namespaces is direct. Other collection namespaces follow the boundary rule.
- **FP0302.** Described partial members as calls or method groups. Describes partial operations and reserves that statement about calls and method groups for method entries. The list also includes reads, casts and operators.
- **FP0302 division and remainder.** Listed `/` and `%`. Adds `/=` and `%=` because compound division and remainder have the same partiality.
- **FP0302 guards.** Described guarding `when` conditions without a shared-label qualification. Requires a qualifying `when` for every label feeding a shared switch section so the guard applies on every entry.
- **FP0303.** Listed methods, local functions and lambdas for null results and methods and local functions for written nullable returns. Includes getters, operators, async results, yields, pointers and unconstrained generic results in the existing return check. Written nullable operator results expose the same `?` span as method results.
- **FP0501.** Described shared case types and nested pattern positions, with `or` as an example. Includes types the constrained input always converts to and names both sides of logical patterns. An explicit pattern for a sole case stays legal so a one-case union can be matched directly.
- **FP0601.** Listed hidden static types and an exemption for `object` parameters of external contracts. Names nullable type parameters, preserves union identity through upcasts, and specifies null checks, the bound `Object.GetType` member, dynamic casts and the actual contract parameter. This distinguishes runtime inspection from ordinary static type use.
- **Reached from a parameter or field.** Described reference and view reachability, fresh results, field roots and copy destinations. Distinguishes by-value slots from caller storage, follows writable `ref` results and tuple projections, recognizes source-backed properties and construction, and includes `TryCopyTo` destinations. Each case describes storage the shared model can identify.
- **Entry rule.** Described anchored declarations and an operator or implicit-conversion location. Requires an anchor in written type syntax, includes target-typed construction with a reported target type, and gives an explicit cast the opening parenthesis span. The rules report the first available source location for the entry.
- **Conventions.** Listed the original shared models. The current list names the shared entry, mutation-root
  and partial-guard models so readers can locate their implementations.

## Open audit findings

All 12 audit reproductions were closed. The KnownGaps project contains zero reproductions, and the findings
list is empty. [FINDINGS.md](../demo/FINDINGS.md)
