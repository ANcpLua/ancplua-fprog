# Demo for the ancplua-fprog analyzers

One C# file per case, named after the rule and the case it tests. Every case exists twice:

| Folder | Content | Must report |
| --- | --- | --- |
| `UnhappyPath/<Case>.cs` | code with a real bug | exactly the lines that end in `// expect: <ID>` |
| `HappyPath/<Case>.cs` | the same job done correctly | nothing |
| `KnownGaps/<Case>.cs` | real bugs that the analyzers do not report (open decisions, see `FINDINGS.md`) | nothing, on purpose |

All three projects compile with the analyzers of this repository (`../ancplua-fprog/ancplua-fprog.csproj` as analyzer
reference), with nullable enabled and C# preview.

## Expected numbers

| Project | Errors | Warnings | Infos |
| --- | --- | --- | --- |
| HappyPath | 0 | 0 | 0 |
| UnhappyPath | 0 | 40 | 0 |
| KnownGaps | 0 | 0 | 0 |

`dotnet build demo/Demo.slnx -c Release --no-incremental` ends with `40 Warning(s)` and `0 Error(s)`.
With the analyzers as they were at commit `8a17c77`, before the audit, UnhappyPath gives 6 warnings instead of 40.

| UnhappyPath file | Warnings | IDs | Before the audit |
| --- | --- | --- | --- |
| `Fp0102_GuidCreateVersion7.cs` | 3 | FP0102 x3 | 1 |
| `Fp0104_AliasOfParameter.cs` | 4 | FP0104 x4 | 0 |
| `Fp0104_ConditionalReceiver.cs` | 3 | FP0104 x3 | 0 |
| `Fp0104_OutArgument.cs` | 3 | FP0104 x3 | 0 |
| `Fp0105_StateHeldInAField.cs` | 3 | FP0105 x3 (new rule) | 0 |
| `Fp0201_PrimaryConstructorParameter.cs` | 2 | FP0201 x2 | 0 |
| `Fp0301_ThrowHelper.cs` | 4 | FP0301 x4 | 1 |
| `Fp0302_DictionaryIndexer.cs` | 4 | FP0302 x4 | 1 |
| `Fp0302_MethodGroup.cs` | 3 | FP0302 x3 | 0 |
| `Fp0801_ImplicitlyTypedShellObject.cs` | 3 | FP0801 x3 | 0 |
| `Fp0801_UsingStaticShellMember.cs` | 3 | FP0801 x3 | 0 |
| `Fps001_UnionThatIsNotClosed.cs` | 5 | CS8509 x2, FP0401 x1, FP0402 x2 | 3 (both CS8509 were suppressed) |

Every analyzer diagnostic is a warning, so errors and infos are 0 everywhere. In HappyPath two compiler warnings
CS8509 are suppressed by FPS001 (`Fp0301_ThrowHelper.cs`, `Fps001_UnionThatIsNotClosed.cs`): that is the suppressor
doing its job on closed unions, and a suppressed warning is not counted.

## Check on the command line

```sh
python3 demo/count.py             # builds, reads the SARIF logs, compares line by line with the "// expect:" comments
python3 demo/count.py --json      # the same as JSON (errors, warnings, infos per file)
python3 demo/count.py --expected  # only what the comments ask for, without building
```

`count.py` prints one row per file (`E`rrors, `W`arnings, `I`nfos, the IDs, and what a suppressor hid) and ends with
`RESULT: everything as expected` (exit code 0) or lists every line that differs (exit code 1). The SARIF logs are
written to `demo/artifacts/<Project>.sarif`.

## Check in the IDE

Open `demo/Demo.slnx` and build once, so that the analyzer project is built and loaded. Each warning in UnhappyPath
sits on a line that ends in `// expect: <ID>`; HappyPath and KnownGaps show none. Count compiler diagnostics (`CS...`)
and analyzer diagnostics (`FP...`) only: hints and suggestions of the IDE's own inspections are not part of the
numbers above.

## What belongs to this demo

- `FINDINGS.md`: what the audit found, what was changed and why, what is still open.
- `patches/`: the 12 changes as one patch per commit (`git am`), in the order they were made.
- The analyzer changes themselves are in `ancplua-fprog/` and `ancplua-fprog.Tests/` of this working tree. Without
  them UnhappyPath shows the "before" numbers.
