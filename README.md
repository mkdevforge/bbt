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

First run after install:

```bash
bbt auth login
```

`bbt auth login` is interactive by default (prompts for email and token).  
For non-interactive use, pass credentials explicitly:

```bash
bbt auth login --email <atlassian-email> --token <api-token>
```

## Requirements

- Bitbucket Cloud account
- Bitbucket API token
- Atlassian email address for HTTP Basic auth (`email:token`)
- Optional: `jq` if you want to use `--jq`

Minimum token scopes required by `bbt`:

- `read:repository:bitbucket`
- `read:workspace:bitbucket`
- `read:user:bitbucket`
- `read:pullrequest:bitbucket`
- `write:pullrequest:bitbucket`

## Quick start

1. From your Bitbucket repo folder, login:

```bash
bbt auth login
```

2. List open pull requests (workspace/repo inferred from git `origin`):

```bash
bbt pr list
```

3. View the PR for your current branch (or pass an explicit id):

```bash
bbt pr view
```

4. Get structured diff JSON:

```bash
bbt pr diff <id> --json
```

## Commands

### Auth

- `bbt auth login [--email <email>] [--token <token>] [--workspace <slug>] [--profile <name>]`
- `bbt auth switch <profile>`
- `bbt auth status [--check]`
- `bbt auth logout [--profile <name>]`

`auth login` validates credentials with Bitbucket before saving. If `--workspace` is provided, it also validates access and stores it as the profile default workspace.  
`auth status --check` performs a live API check.

If `--email`/`--token` are omitted, `auth login` prompts interactively (unless stdin is redirected). When prompting for a token, it prints the minimum required scopes above.

### Pull requests

- `bbt pr list [--state <STATE>] [--limit <n>]`
- `bbt pr view [<id>]`
- `bbt pr diff [<id>] [--include-raw]`
- `bbt pr comments [<id>] [--limit <n>] [--sort <expr>] [--page <n>] [--pagelen <n>] [--paginate] [--contains <text> | -q/--query <expr>]`
- `bbt pr threads [<id>] [--limit <n>] [--sort <expr>] [--pagelen <n>] [--contains <text> | -q/--query <expr>]`
- `bbt pr comment <id> (--body <text> | --body-file <path>) [--reply-to <comment-id>] [--file <path> --line <n> [--line-end <n>] [--side <to|from>]]`
- `bbt pr review <id> (--approve|--unapprove|--request-changes|--unrequest-changes) [--body <text>|--body-file <path>]`

Notes:

- For `pr view`, `pr diff`, `pr comments`, and `pr threads`, `<id>` is optional.
- If `<id>` is omitted, `bbt` tries to resolve the PR from your current git branch.
- `pr list` defaults to `--state OPEN`.
- `pr comment` and `pr review` always require explicit PR id.
- Inline comments default to `--side to` (new side).
- `pr comments` defaults to newest-first (`--sort -created_on`) and returns one page by default (50 comments). Use `--paginate`, a larger `--pagelen`, or `--limit` to fetch more.
- `pr threads` groups comments into discussion threads (root + replies, including nested replies). Threads are ordered by discovery sort (default: `--sort -created_on`); with `--contains/-q`, ordering is based on the newest matching comment. Defaults: `--limit 20`, `--pagelen 100`.
- `pr review --body/--body-file` posts a global comment first, then performs the review action.

#### PR comment threads (recommended)

Use this during code review to focus on discussions instead of a flat comment list.

```bash
# newest active threads for the PR of your current branch
bbt pr threads

# filter threads that mention a phrase (matches any comment in the thread)
bbt pr threads --contains "AI Code Review"

# JSON thread objects: root + replies + lastActivityOn
bbt pr threads --json

# root ids only (one per line)
bbt pr threads --quiet

# fetch more threads (may require more API calls)
bbt pr threads --limit 50
```

### Raw API access

- `bbt api <PATH> <METHOD> [--input <file>] [--paginate]`

`--paginate` follows Bitbucket `next` links and emits a merged `values` array.
`bbt api <METHOD> <PATH> ...` is also accepted.

Path placeholders:

- `{workspace}`
- `{repo}` / `{repo_slug}`

Example:

```bash
bbt api "/repositories/{workspace}/{repo}/pullrequests?state=OPEN&pagelen=10" GET --paginate --json
```

### LLM context

- `bbt llms`
- `bbt llms --json`

Use this to print a single, complete CLI capability reference for AI/automation tooling.

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
- `BBT_ALLOW_INSECURE_HTTP` (set to allow sending credentials over `http://` URLs; not recommended)
- `BBT_DISABLE_CRL_CHECK` (set to disable TLS certificate revocation checking)

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

## License

MIT. See [LICENSE](LICENSE).
