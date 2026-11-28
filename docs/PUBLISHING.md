# Publishing

A release consists of `ANcpLua.Fprog.Analyzers` on nuget.org, the immutable `v<version>` source tag and the
GitHub release. [`check.yml`](../.github/workflows/check.yml) publishes deliberately marked commits on `main`.
The [Playground](../ANcpLua.Fprog.Analyzers.Playground) tests the exact NuGet archive as a customer’s .NET 11
project would consume it, with controls and expected diagnostics for every shipped rule.

## Set up once

1. In GitHub, create an environment named `nuget`.
2. Add its `NUGET_USER` secret: `ANcpLua`, the NuGet profile name. It is neither an email address nor an API key.
3. Sign in at [NuGet Trusted Publishing](https://www.nuget.org/account/trustedpublishing) and add a policy with the
   values below. Choose the NuGet account or organization that will own the package. Set the package pattern to
   exactly `ANcpLua.Fprog.Analyzers`, keep the permission to push new packages and versions, and turn the
   permission to unlist or relist versions off.

| Field | Value |
| --- | --- |
| Repository owner | `ANcpLua` |
| Repository | `ancplua-fprog` |
| Workflow file | `check.yml` |
| Environment | `nuget` |

Step 3 needs the owner's NuGet sign-in. Steps 1 and 2 can be done with `gh`:

```sh
gh api -X PUT repos/ANcpLua/ancplua-fprog/environments/nuget
gh secret set NUGET_USER --env nuget --repo ANcpLua/ancplua-fprog --body ANcpLua
```

The `publish` job uses `NuGet/login` with GitHub's `id-token: write` permission to obtain a short-lived publishing
key, so the repository stores no long-lived NuGet API key.

## Release a version

1. Set the stable `VersionPrefix` in `ancplua-fprog/ancplua-fprog.csproj`, give `CHANGELOG.md` a released
   `## <version>` section, and update the README installation example. Move new rule rows from
   `AnalyzerReleases.Unshipped.md` to the named release in `AnalyzerReleases.Shipped.md`. A new or wider rule can
   break warnings-as-errors builds, so use a minor version below 1.0 and a major version from 1.0 on.
2. While editing, run `make check`, `python3 demo/count.py` and `make playground`. The Playground checks the packed
   DLL, marker source, package version and compiler diagnostics; it removes its temporary projects, packages, and caches.
3. Commit the intended release on local `main`. Use the exact stable version as a Git trailer, keeping a blank line
   before it. From a clean checkout run `make release`. This tests the package for that committed SHA and pushes it to
   the canonical `main` only if the consumer gate passes. For example, for the next patch after 0.7.0:

   ```sh
   git commit -m "Release 0.7.1" -m "NuGet-Release: 0.7.1"
   make release
   ```

4. Watch the `check` workflow for the pushed SHA. It runs repository checks, rebuilds the stable package on a clean runner,
   and runs the Playground on **the same file it submits**. It creates the immutable tag without overwriting an existing
   ref, uses the `nuget` environment and OIDC to submit the package, then records the changelog and a source/payload receipt
   in the GitHub release. The green `publish` job confirms acceptance or a byte-verified existing publication.
   Old-style `v<version>` tag pushes remain eligible if the tag matches the project version and points to a commit on main.
5. Raise the development `VersionPrefix` when appropriate. Leave the release trailer out of development and housekeeping
   commits. They run ordinary checks and the CI-prerelease Playground.

## Check the indexed package

NuGet controls validation and indexing after submission. The read-only [Verify published package workflow](../.github/workflows/verify-published.yml)
checks that the upstream run’s `publish` job succeeded, waits up to 20 minutes for index visibility and resolves the tag to
its exact SHA. It runs the SDK and Playground from that release and checks the signed nuget.org payload against the accepted
candidate digest. No copy of the local candidate is installed in the user’s global NuGet cache. CI and local consumer runs
discard the private feed, restored candidate, build files and test project, including when a diagnostic assertion fails.

To recheck a published version without a publish or tag operation:

```sh
gh workflow run verify-published.yml --ref main -f version=0.7.0
```

Manual checks validate the release tag and expected source commit. For historical tags that predate the Playground, such
as `v0.7.0`, they use the compatible current-main Playground. To test an exact indexed version directly on your machine:

```sh
python3 -B ANcpLua.Fprog.Analyzers.Playground/verify.py --nuget 0.7.0 --expected-commit "$(git rev-parse 'v0.7.0^{commit}')"
```

## Recover a release

- If a pre-publication gate fails, fix the failing source or fixture. An untagged superseded commit cannot reserve an old
  version on top of newer `main` work. Never move a tag that identifies published or potentially submitted bytes.
- If the submission outcome was uncertain, rerun the original failed jobs. A saved GitHub release receipt recognizes a
  previous acceptance before indexing finishes. For an already indexed version the Playground verifies the official package
  commit and payload before the release is accepted. A version collision with different bytes fails; prepare a new version.
- If only the indexed check times out or its network access fails, rerun **Verify published package**. It has read-only
  permissions. Check validation notices in the NuGet account if the package does not appear. Trusted Publishing supplies the
  credentials; the index is confirmed by the clean public-feed build.
