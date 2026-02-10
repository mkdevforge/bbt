# bbt

`bbt` is a .NET CLI for Bitbucket Cloud pull request workflows.

It supports profile-based authentication, PR read/write operations, structured JSON output for scripting, and a raw API command for endpoints that are not wrapped yet.

## Install

```bash
dotnet tool install --global MkDevForge.Bbt
```

Verify:

```bash
bbt --version
```

## Requirements

- Bitbucket Cloud account
- Bitbucket API token
- Atlassian email address for HTTP Basic auth (`email:token`)
- Optional: `jq` if you want to use `--jq`

## Quick start

1. Login and create/update a profile:

```bash
bbt auth login --workspace <workspace>
```

2. List open pull requests:

```bash
bbt pr list --workspace <workspace> --repo <repo>
```

3. View a PR:

```bash
bbt pr view <id> --workspace <workspace> --repo <repo>
```

4. Get structured diff JSON:

```bash
bbt pr diff <id> --workspace <workspace> --repo <repo> --json
```

## Commands

### Auth

- `bbt auth login [--workspace <slug>] [--profile <name>] [--email <email>] [--token <token>]`
- `bbt auth switch <profile>`
- `bbt auth status [--check]`
- `bbt auth logout [--profile <name>]`

`auth login` validates credentials with Bitbucket before saving.  
`auth status --check` performs a live API check.

### Pull requests

- `bbt pr list [--state <STATE>] [--limit <n>]`
- `bbt pr view [<id>]`
- `bbt pr diff [<id>] [--include-raw]`
- `bbt pr comments [<id>] [--limit <n>]`
- `bbt pr comment <id> (--body <text> | --body-file <path>) [--file <path> --line <n> [--line-end <n>] [--side <to|from>]]`
- `bbt pr review <id> (--approve|--unapprove|--request-changes|--unrequest-changes) [--body <text>|--body-file <path>]`

Notes:

- For `pr view`, `pr diff`, and `pr comments`, `<id>` is optional.
- If `<id>` is omitted, `bbt` tries to resolve the PR from your current git branch.
- `pr comment` and `pr review` always require explicit PR id.
- `pr review --body/--body-file` posts a global comment first, then performs the review action.

### Raw API access

- `bbt api <METHOD> <PATH> [--input <file>] [--paginate]`

`--paginate` follows Bitbucket `next` links and emits a merged `values` array.

Path placeholders:

- `{workspace}`
- `{repo}` / `{repo_slug}`

Example:

```bash
bbt api GET "/repositories/{workspace}/{repo}/pullrequests?state=OPEN&pagelen=10" --paginate --json
```

## Output and scripting flags

Most commands support:

- `--json` for structured output
- `--fields <csv>` to keep top-level fields from JSON output
- `--jq <expr>` to pipe JSON through external `jq`
- `--quiet` for minimal output
- `--verbose` for request diagnostics to stderr
- `--no-retry` to disable transient retry/backoff

Rules:

- `--fields` and `--jq` require `--json`
- `--json` and `--quiet` are mutually exclusive
- `jq` is optional unless `--jq` is used

## Workspace/repo resolution

When a command needs workspace/repo, resolution order is:

1. CLI flags (`--workspace`, `--repo`)
2. Environment (`BBT_WORKSPACE`, `BBT_REPO`)
3. Current profile defaults
4. Git `origin` URL (`https://bitbucket.org/<workspace>/<repo>.git` or `git@bitbucket.org:<workspace>/<repo>.git`)

Workspace and repo can come from different sources in the same invocation.

## Environment variables

- `BBT_EMAIL`
- `BBT_TOKEN`
- `BBT_WORKSPACE`
- `BBT_REPO`
- `BBT_BASE_URL` (default: `https://api.bitbucket.org/2.0`)

Environment values override profile values.

## Credential and config storage

- Non-secret config is stored in `config.json` under OS-specific app config directories.
- Tokens are stored in:
  - macOS: Keychain
  - Windows: Credential Manager
  - Linux: `secret-tool` when available
  - Linux fallback: token file with restrictive permissions (`0600`)

## Current scope

- Bitbucket Cloud only
- API token auth only (no OAuth flow yet)
- No dedicated `pr comment edit` command yet (you can use `bbt api PUT .../comments/{id}`)

## Documentation

- API contract used by v0.1: `docs/contracts/bitbucket-cloud-v0.1.md`
- Release and semantic versioning: `docs/release.md`

## License

MIT (`LICENSE`)
