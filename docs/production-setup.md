# Production setup

The repository was the playbook and scaffold for a referee contest: agents implemented the
rules FP0101–FP0802 and the suppressor FPS001 against hidden tests and a hidden corpus. The
contest is over and all 20 rules are on `main`. This document lists what existed only for the
contest, decides for each item whether to keep, adapt, replace or remove it, and describes the
target setup: an analyzer package on nuget.org with a maintainer contract instead of a contest
playbook.

Written on 2026-11-22 on branch `setup/production`. Line numbers refer to commit `de70e24`
(`ci/check`, the base of this branch) unless a file is named as it is now.

## Inventory

| # | Item | Evidence | Decision | Reason | Status |
| --- | --- | --- | --- | --- | --- |
| 1 | `BASE`, `REFEREE`, `PROTECTED` variables | `Makefile:12-17` | Remove | Inputs of the referee only: base commit, hidden tests path, protected list | Done |
| 2 | `protected`, `score`, `tools-init` targets | `Makefile:60-78` | Remove | `protected` and `score` ran the hidden tests and corpus; `tools-init` created the tool manifest once, which is now committed (`dotnet-tools.json`) | Done |
| 3 | `PYTHON` variable | `Makefile:3` | Remove | Used only by `score` | Done |
| 4 | `eng/score.py` | 89 lines, `eng/score.py:1-5` ("Score a run per rule") | Remove | Scoring script of the referee | Done |
| 5 | mise `score` task and `python` tool | `mise.toml:3`, `mise.toml:17-19` | Remove | Ran `make score`; Python was needed only for `score.py` | Done |
| 6 | Contest text in `AGENTS.md` | `AGENTS.md:3` (observer note), `:11-15` Goal, `:81-88` Bonus rules "not scored", `:102-125` phase workflow, `:134-140` protected files, `:144-149` Scoring | Adapt | The catalog, scope and conventions stay binding; goal, phases, protected files and scoring go. See "Maintainer contract" | Done |
| 7 | Protected-file rule | `AGENTS.md:137-139`, `Makefile:15-17` | Remove | It guarded the referee's inputs. After the contest, `Makefile`, `mise.toml`, `global.json`, `AGENTS.md`, the test host and `Fixtures/` are ordinary files. Note that it listed `.config/dotnet-tools.json` while the manifest lives at the root | Done |
| 8 | Referee comments in the test host | `ancplua-fprog.Tests/FpAnalyzerTestHost.cs:18`, `:21` ("Protected file"), `:50` ("Hidden tests use this method") | Adapt | The host stays the single test entry point; only the comments were about the referee | Done |
| 9 | Referee comment in the test project | `ancplua-fprog.Tests/ancplua-fprog.Tests.csproj:12-13` ("Keep equal to FprogWorkspacesVersion in the referee's HiddenTests.csproj") | Adapt | The `Workspaces` reference stays (the testing library needs it); it is now tied to the analyzer's Roslyn version | Done |
| 10 | Demo tests `Example.cs`, `FixtureLifecycleTests.cs` | `ancplua-fprog.Tests/Example.cs:9`, `FixtureLifecycleTests.cs:13-98` | Remove | They demonstrate an MSTest fixture pattern (German test names, a temp file). No analyzer test uses them | Done |
| 11 | `ancplua-fprog.Tests/Fixtures/` | `FixtureTests.cs:29`, `InputFixture.cs:9` | Remove | Used only by item 10 (`grep -rn "Fixtures\|FixtureTests"` finds no other user) | Done |
| 12 | `docs/SPEC.source.md` | 105 lines; `docs/SPEC.source.md:1` "Unverified source. Written by Gemini" | Proposal: remove | Unverified, partly German, superseded by the catalog in `AGENTS.md` and by `README.md`. No code references it | Proposed |
| 13 | `docs/ROSLYN.source.md` | 180 lines; `docs/ROSLYN.source.md:1-12` "Partly verified", errata | Proposal: remove after the rule PRs | API map written for implementers who had no code to read; the code is now the better map. Rule agents may still read it today | Proposed |
| 14 | `tools/feed/RefTrace.0.9.0-preview.1.nupkg` and the `tools` source in `nuget.config:5-7` | `https://api.nuget.org/v3-flatcontainer/reftrace/index.json` returned 404 (`BlobNotFound`) on 2026-11-22 | Keep | `make verify-dll` needs RefTrace and it is not on nuget.org. Remove the feed and the `nuget.config` entry once RefTrace is published there | Kept |
| 15 | `.gitignore` entry `fprog-kit/` | `.gitignore:8` | Remove | The contest kit folder; nothing creates it any more | Done |
| 16 | SDK pin `11.0.100-rc.3.26522.148` | `global.json:3` | Replace | Released SDK `11.0.100`; content of `8a17c77` (branch `pins-11.0.100`), which was checked with `make check` on 11.0.100 | Done |
| 17 | `"allowPrerelease": true`, `"rollForward": "disable"` | `global.json` | Proposal: `allowPrerelease: false`, `rollForward: latestPatch` | No prerelease is pinned any more. `latestPatch` keeps the 11.0.1xx compiler (CS9057 safe) and accepts security patches | Proposed |
| 18 | `IsPackable=false` | `ancplua-fprog/ancplua-fprog.csproj:5` | Replace | The analyzer is the product. See "Package" | Done |
| 19 | Names: project and assembly `ancplua-fprog`, namespace `ancplua_fprog` | `ancplua-fprog/ancplua-fprog.csproj:12`, every `.cs` file | Proposal: rename to `ANcpLua.Fprog.Analyzers` | Matches the package id and the other ANcpLua repositories. It touches every file under `Rules/`, so it would conflict with all 20 `rule/*` branches. Do it after they merge, as one mechanical commit: project and folder names, `RootNamespace`, namespaces, `InternalsVisibleTo`, `ANALYZER_DLL` in the `Makefile`, the layout check in the workflow | Proposed |
| 20 | No license | — | Add MIT | Same license and holder as the sibling ANcpLua repositories; required for nuget.org | Done, owner confirms |
| 21 | No README | — | Add | Users need install steps, the annotations and the rule list; it is also the package readme | Done |

Kept as they are, with reasons: `make check` and its steps (the quality gate, unchanged),
`make manifest` (useful for bug reports), the `Microsoft.CodeAnalysis.CSharp` 5.9.0 pin
(CS9057), `LangVersion preview` in the analyzer project (the pinned compiler supports it),
`System.Security.Cryptography.Pkcs` override in the test project (vulnerable transitive version),
`TestAssembly.cs` (method-level parallelism).

## Package

- **Id:** `ANcpLua.Fprog.Analyzers` (free on nuget.org: the flat-container index returned 404 on
  2026-11-22). The assembly keeps the name `ancplua-fprog.dll` until item 19.
- **Layout** (checked with `unzip -l` on `ANcpLua.Fprog.Analyzers.0.1.0.nupkg`):

  ```text
  analyzers/dotnet/cs/ancplua-fprog.dll
  contentFiles/cs/any/Fprog/FprogAttributes.cs   (buildAction="Compile")
  README.md
  LICENSE
  ```

  No `lib/` folder (`IncludeBuildOutput=false`), no dependencies
  (`SuppressDependenciesWhenPacking=true`; Roslyn is provided by the compiler), and
  `developmentDependency=true`, so the reference does not flow to the user's own dependents.
- **Attributes.** The analyzers match `Fprog.FunctionalCoreAttribute` and `Fprog.UnionAttribute`
  by name in any assembly (`ancplua-fprog/Shared/FprogAttributes.cs:6-10`), so the package does
  not need a runtime library. It ships `ancplua-fprog/contentFiles/FprogAttributes.cs` as a
  content file that compiles into the user's project as `internal` types, with the same
  `AttributeUsage` as `FpAnalyzerTestHost.AnnotationsSource`. Defining `FPROG_NO_ATTRIBUTES`
  removes it for users who declare their own copies. The file is excluded from the analyzer's
  own compilation. Alternatives considered: a separate `ANcpLua.Fprog.Annotations` library
  (one more package and a runtime dependency for two empty attributes), or a
  `RegisterPostInitializationOutput` generator (needs a source generator in the analyzer
  assembly and changes the analyzer surface). Known limit: two projects that both receive the
  internal copy and share internals through `InternalsVisibleTo` get CS0436; they can set
  `FPROG_NO_ATTRIBUTES` in one of them.
- **Smoke test.** A `net11.0` library referencing the local package built with FP0101 reported
  on a `void` method in a `[Fprog.FunctionalCore]` class and no CS8509 on an exhaustive union
  switch (FPS001), with the attributes coming from the content file.
- **Readme and license** are packed from the repository root (`PackageReadmeFile`,
  `PackageLicenseExpression=MIT`).
- **Release tracking.** `AnalyzerReleases.Unshipped.md` lists all 20 diagnostics. At the first
  release the rows move to `## Release 0.1.0` in `AnalyzerReleases.Shipped.md`. That edit is
  left to the release commit because the `rule/*` branches own the Unshipped file today.
- **Versioning.** `VersionPrefix` 0.1.0 in the project; CI packs `0.1.0-ci.<run>` on every push
  and the tag version on a `v*` tag. Semantic versioning as written in `AGENTS.md`: a new or
  wider rule can break builds with warnings as errors, so it is a minor bump below 1.0 and a
  major bump after.

## CI

`.github/workflows/check.yml` (from `ci/check`) keeps the `check` job: tools, build, test,
InspectCode, RefTrace on every push and on pull requests to `main`. This branch adds:

- **`pack`**, after `check`: `dotnet pack` with warnings as errors, a layout check (the DLL
  under `analyzers/dotnet/cs`, the attribute source, README, LICENSE, no `lib/`), and upload of
  the `.nupkg` as the `nupkg` artifact.
- **`publish`**, after `pack`, only for refs `refs/tags/v*`: `dotnet nuget push --skip-duplicate`
  with the secret `NUGET_API_KEY` from the GitHub environment `nuget`. It fails with a clear
  message when the secret is missing. No tag and no secret were created on this branch.

## Maintainer contract

`AGENTS.md` changes from the contest playbook to the maintainer contract.

Stays binding: environment, commands, scope, the rule catalog with its exact spans (now a
release surface instead of a test target), diagnostic conventions, the ban on silencing
findings, no network access, copying with a source URL.

Goes: the goal and scoring, the phase 0/phase 1 workflow, the protected-file list, the
referee's hidden tests, the observer's cleanup note, "bonus rules not scored" (kept as
candidate rules that need a definition first).

New: how to change a rule (catalog row first, test first, a `_NoReport` test for every fixed
false positive), the release flow for `AnalyzerReleases.*.md`, versioning, the rule that a
higher Roslyn version raises the minimum SDK.

`CLAUDE.md` stays `@AGENTS.md`.

## Merge order

1. **`ci/check`** (the workflow). This branch is based on it and contains its commit.
2. **`setup/production`** (this branch). It does not touch `ancplua-fprog/Rules/`,
   `ancplua-fprog.Tests/Rules/` or `AnalyzerReleases.*.md`, so it does not conflict with the
   rule branches. It removes 4 test files outside `Rules/`; the test count drops from 1236 to
   1218 (the 18 demo tests; the full suite passes on this branch).
3. **`rule/FPxxxx`** PRs, in any order, each green on CI. They may also go before step 2; there
   is no file overlap either way.
4. After all rule PRs: the release commit (Unshipped to Shipped, release notes), then item 19
   (rename) and items 12–13 (delete the source documents) as separate PRs, then the first tag.

## Observer steps

These need the repository owner and were not done:

1. Confirm the MIT license and the package id `ANcpLua.Fprog.Analyzers`.
2. Create the GitHub environment `nuget` with the secret `NUGET_API_KEY` (a nuget.org API key
   scoped to push `ANcpLua.Fprog.Analyzers`), optionally with a required reviewer.
3. After the merges in "Merge order", push the tag `v0.1.0` from `main`. CI packs version
   0.1.0 and publishes it.
4. Optionally publish RefTrace to nuget.org, then drop `tools/feed` and its `nuget.config` entry.
