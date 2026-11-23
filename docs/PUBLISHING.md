# Publishing

A release is the package **ANcpLua.Fprog.Analyzers** on nuget.org, the tag `v<version>` and a GitHub release with
the changelog section of that version. Pushing the tag starts it: `.github/workflows/check.yml` in
[`ANcpLua/ancplua-fprog`](https://github.com/ANcpLua/ancplua-fprog) runs `check`, then `pack`, then `publish`.

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

1. **Version.** `VersionPrefix` in `ancplua-fprog/ancplua-fprog.csproj` is the version to release, and
   `CHANGELOG.md` has its section. Each release needs a new version.
2. **Rule tracking.** Move the rows of `ancplua-fprog/AnalyzerReleases.Unshipped.md` into a `## Release <version>`
   section of `ancplua-fprog/AnalyzerReleases.Shipped.md`, and leave the header lines of the unshipped file.
3. **Gates.** `make check`, `python3 demo/count.py` and `make pack` succeed. Commit on `main` and push; the `check`
   workflow of that commit is green.
4. **Try the product.** Reference `artifacts/package/ANcpLua.Fprog.Analyzers.<version>.nupkg` from a new `net11.0`
   project through a local package source, build the examples of the README with it and see the findings the README
   shows. A green CI run does not replace this check.
5. **Publish.** Tag that commit and push the tag. The `publish` job rejects a tag that differs from `VersionPrefix`.

   ```sh
   git tag v<version> && git push origin v<version>
   gh run watch "$(gh run list --workflow check.yml --branch v<version> --limit 1 --json databaseId --jq '.[0].databaseId')" --exit-status
   ```
6. **Confirm.** The version appears in `https://api.nuget.org/v3-flatcontainer/ancplua.fprog.analyzers/index.json`.
   NuGet validates and indexes a new package first, which can take several minutes.
7. **GitHub release.** Publish the changelog section of the version as the release notes:

   ```sh
   gh release create v<version> --verify-tag --title "ANcpLua.Fprog.Analyzers <version>" --notes-file <file with the changelog section>
   ```
8. **Next version.** Raise `VersionPrefix` to the next version, so that packages built from `main` sort after the
   release.

A new or wider rule can break a build that treats warnings as errors: it is a minor version below 1.0 and a major
version from 1.0 on.
