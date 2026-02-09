# bbt

`bbt` is a .NET global tool for Bitbucket Cloud pull request workflows.

## Install

```bash
dotnet tool install --global MkDevForge.Bbt
```

## Key commands

```bash
bbt auth login --workspace <workspace>
bbt pr list --workspace <workspace> --repo <repo>
bbt pr view <id> --workspace <workspace> --repo <repo>
bbt pr diff <id> --workspace <workspace> --repo <repo> --json
bbt pr comment <id> --workspace <workspace> --repo <repo> --body "LGTM"
```

## Documentation

- API contracts used by v0.1 live in `docs/contracts/bitbucket-cloud-v0.1.md`.
- Release process and semver rules live in `docs/release.md`.

## Versioning

- Package/tool version is generated with `MinVer` from git tags.
- Release tags must use `v<major>.<minor>.<patch>` (example: `v0.2.0`).
- Commits after a release tag produce prerelease versions until the next tag.
