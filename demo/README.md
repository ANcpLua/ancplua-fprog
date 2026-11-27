# Demo for the ancplua-fprog analyzers

One C# file per case, named after the rule and the case it tests. Every case exists twice:

| Folder | Content | Must report |
| --- | --- | --- |
| `UnhappyPath/<Case>.cs` | code with a real bug | exactly the lines that end in `// expect: <ID>` |
| `HappyPath/<Case>.cs` | the same job done correctly | nothing |
| `KnownGaps/<Case>.cs` | real bugs that the analyzers do not report (open decisions, see `FINDINGS.md`) | nothing, on purpose |

The three demo projects compile with the analyzers of this repository (`../ancplua-fprog/ancplua-fprog.csproj` as analyzer
reference), with nullable enabled and .NET 11's default language version, C# 15.
The warning-free ReferencedTypes library provides the closed cases for the FP0502 demo pair.

## Expected numbers

Generated from `python3 demo/count.py --json`.

| Project | Errors | Warnings | Infos |
| --- | --- | --- | --- |
| HappyPath | 0 | 0 | 0 |
| UnhappyPath | 0 | 149 | 0 |
| KnownGaps | 0 | 0 | 0 |

| UnhappyPath file | Warnings | IDs |
| --- | --- | --- |
| `Fp0101_VoidDelegates.cs` | 2 | FP0101 x2 |
| `Fp0102_GuidCreateVersion7.cs` | 3 | FP0102 x3 |
| `Fp0102_HashCodeAsData.cs` | 1 | FP0102 x1 |
| `Fp0102_LocalTimeZone.cs` | 3 | FP0102 x3 |
| `Fp0104_AliasOfParameter.cs` | 8 | FP0104 x4, FP0201 x3, FP0601 x1 |
| `Fp0104_ConditionalReceiver.cs` | 4 | FP0104 x3, FP0201 x1 |
| `Fp0104_ConvertedBranches.cs` | 1 | FP0104 x1 |
| `Fp0104_ElementHandedOutByCall.cs` | 2 | FP0104 x2 |
| `Fp0104_MutatingCallOnParameter.cs` | 3 | FP0104 x3 |
| `Fp0104_OutArgument.cs` | 5 | FP0104 x3, FP0201 x1, FP0801 x1 |
| `Fp0104_ReferenceInsideValueParameter.cs` | 1 | FP0104 x1 |
| `Fp0104_SpanOverParameter.cs` | 2 | FP0104 x2 |
| `Fp0105_EventSubscription.cs` | 2 | FP0105 x2 |
| `Fp0105_FieldBehindAlias.cs` | 3 | FP0105 x2, FP0202 x1 |
| `Fp0105_StateHeldInAField.cs` | 3 | FP0105 x3 |
| `Fp0106_FormattingByCulture.cs` | 2 | FP0106 x2 |
| `Fp0106_QueryOrdersText.cs` | 2 | FP0106 x2 |
| `Fp0106_SortingStringsByCulture.cs` | 1 | FP0106 x1 |
| `Fp0106_SpanStringComparer.cs` | 1 | FP0106 x1 |
| `Fp0107_PointersInCore.cs` | 9 | FP0107 x9 |
| `Fp0201_PrimaryConstructorParameter.cs` | 2 | FP0201 x2 |
| `Fp0203_BuilderInRecord.cs` | 5 | FP0203 x5 |
| `Fp0301_DoesNotReturn.cs` | 2 | FP0301 x2 |
| `Fp0301_ThrowHelper.cs` | 4 | FP0301 x4 |
| `Fp0302_DictionaryIndexer.cs` | 5 | FP0302 x5 |
| `Fp0302_Division.cs` | 2 | FP0302 x2 |
| `Fp0302_Downcast.cs` | 1 | FP0302 x1 |
| `Fp0302_ElementByPosition.cs` | 1 | FP0302 x1 |
| `Fp0302_EmptySequence.cs` | 2 | FP0302 x2 |
| `Fp0302_MethodGroup.cs` | 3 | FP0302 x3 |
| `Fp0302_ParseExactAndConvert.cs` | 2 | FP0302 x2 |
| `Fp0302_ScalarConvertOverloads.cs` | 2 | FP0302 x2 |
| `Fp0303_NullableReturnType.cs` | 4 | FP0303 x4 |
| `Fp0401_OpenClassInCore.cs` | 2 | CS8509 x1, FP0401 x1 |
| `Fp0403_CaseLeftOpen.cs` | 1 | FP0403 x1 |
| `Fp0404_ComputedUnionValue.cs` | 4 | FP0404 x4 |
| `Fp0404_EmptyUnion.cs` | 3 | FP0404 x3 |
| `Fp0501_CatchAllBehindCast.cs` | 1 | FP0501 x1 |
| `Fp0501_CatchAllInTuple.cs` | 1 | FP0501 x1 |
| `Fp0501_IncompleteClosedHierarchy.cs` | 1 | FP0501 x1 |
| `Fp0501_NativeUnionCatchAll.cs` | 1 | FP0501 x1 |
| `Fp0501_NegatedCase.cs` | 1 | FP0501 x1 |
| `Fp0502_CastHidesCases.cs` | 1 | FP0502 x1 |
| `Fp0502_SwitchStatementMissesCase.cs` | 1 | FP0502 x1 |
| `Fp0503_IsChain.cs` | 2 | FP0503 x2 |
| `Fp0601_CastThroughObject.cs` | 3 | FP0601 x3 |
| `Fp0601_Dynamic.cs` | 1 | FP0601 x1 |
| `Fp0701_ExtensionReceiver.cs` | 1 | FP0701 x1 |
| `Fp0801_ConcurrencyPrimitives.cs` | 5 | FP0801 x5 |
| `Fp0801_CoreCallsNonCoreCode.cs` | 2 | FP0801 x2 |
| `Fp0801_ImplicitlyTypedShellObject.cs` | 4 | FP0801 x3, FP0802 x1 |
| `Fp0801_InjectedClock.cs` | 3 | FP0801 x3 |
| `Fp0801_QueryOverShellExtension.cs` | 1 | FP0801 x1 |
| `Fp0801_ShellValueWithoutTypeName.cs` | 6 | FP0801 x6 |
| `Fp0801_TrustedLibrary.cs` | 1 | FP0801 x1 |
| `Fp0801_UnnamedShellExtension.cs` | 1 | FP0801 x1 |
| `Fp0801_UsingStaticShellMember.cs` | 3 | FP0801 x3 |
| `Fp0802_TasksInCore.cs` | 5 | FP0802 x5 |
| `Fp0803_ShellTypeDerivesFromCore.cs` | 1 | FP0803 x1 |

## Check on the command line

```sh
python3 demo/count.py             # builds, reads the SARIF logs, compares line by line with the "// expect:" comments
python3 demo/count.py --json      # the same as JSON (errors, warnings, infos per file)
python3 demo/count.py --expected  # only what the comments ask for, without building
```

`count.py` prints one row per file (`E`rrors, `W`arnings, `I`nfos, and the IDs) and ends with
`RESULT: everything as expected` (exit code 0) or lists every line that differs (exit code 1). The SARIF logs are
written to `demo/artifacts/<Project>.sarif`.

## Check in the IDE

Open `demo/Demo.slnx` and build once, so that the analyzer project is built and loaded. Each warning in UnhappyPath
sits on a line that ends in `// expect: <ID>`; HappyPath and KnownGaps show none. Count compiler diagnostics (`CS...`)
and analyzer diagnostics (`FP...`) only: hints and suggestions of the IDE's own inspections are not part of the
numbers above.

Known coverage gaps and their reasons are listed in [FINDINGS.md](FINDINGS.md).
