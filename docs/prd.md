# bbt — A Bitbucket CLI for developers and AI agents

**Product Requirements Document — Draft v0.1**
**Author:** Mikael / mkdevforge
**Date:** February 2026

---

## Problem

GitHub developers have `gh` — a first-party CLI with 41k+ stars that handles PRs, issues, repos, Actions, and everything else from the terminal. It works brilliantly with AI coding agents like Claude Code and Cursor because they can shell out to `gh` and get deterministic, structured output.

Bitbucket developers have nothing equivalent. There is no official Bitbucket CLI. The few community attempts are either abandoned, enterprise-admin-focused, or too early-stage to rely on (the most `gh`-like option, `bkt`, has 3 GitHub stars and shipped v0.2.2 in late 2025).

This gap matters more now than ever because AI-assisted development workflows increasingly depend on CLI tooling to interact with source control platforms. At KPMG Finland, our development team has a concrete unmet need: getting AI-generated code review feedback posted as inline PR comments at the correct code lines, rather than dumped as a single wall-of-text comment. This is trivial on GitHub with `gh api` — it's a manual exercise on Bitbucket today.

Atlassian has shown no intent to close this gap. Their official MCP server covers Jira and Confluence but explicitly excludes Bitbucket. Their AI investment in Bitbucket (Rovo Dev) is a CI/CD agent, not a developer CLI. App passwords were deprecated in September 2025 and die completely in June 2026, yet no first-party CLI exists to ease the migration to API tokens.

## Vision

`bbt` is a command-line interface for Bitbucket that gives developers and AI agents the same fluency with Bitbucket that `gh` provides for GitHub. It is designed CLI-first, so it works everywhere — terminals, scripts, CI/CD pipelines, and AI agents — with a thin MCP wrapper available for clients that prefer that protocol.

## Target users

**Primary: Development teams on Bitbucket who use AI coding tools.** These are teams where developers use Claude Code, Cursor, Copilot, or similar tools and want those tools to interact with Bitbucket PRs, repos, and pipelines programmatically. The immediate use case is AI-powered code review with line-level inline PR comments.

**Secondary: Developers and DevOps engineers who want `gh`-like productivity on Bitbucket.** People who work across GitHub and Bitbucket and are frustrated by the tooling disparity. People who want to script Bitbucket operations without hand-rolling curl commands against the REST API.

**Tertiary: CI/CD pipeline authors.** Teams building Bitbucket Pipelines (or external CI) that need to interact with PRs, post status updates, or automate review workflows.

## Principles

**CLI-first, MCP-second.** The CLI is the product. An MCP server is a wrapper that calls the same CLI or shares the same core library. This ensures the widest possible audience and the best AI agent compatibility (agents are better at composing shell commands than navigating fixed MCP tool interfaces).

**Composable over comprehensive.** Follow the Unix philosophy. Each command does one thing, returns structured output, and can be piped. Cover the critical 80% of workflows well rather than wrapping every Bitbucket API endpoint poorly.

**`gh`-familiar ergonomics.** Developers who know `gh pr list`, `gh pr view`, `gh pr comment` should feel immediately at home with `bbt pr list`, `bbt pr view`, `bbt pr comment`. Don't invent new patterns where proven ones exist.

**Bitbucket Cloud first, Server/DC later.** Cloud is the primary target — it's where Atlassian is investing and where the auth migration is happening. Bitbucket Server / Data Center support is a future concern, not a launch blocker.

**Structured output as a first-class citizen.** Every command supports `--json` with field selection and `--jq` for filtering. This is what makes a CLI useful for AI agents and scripts, not just humans.

## Distribution

Distributed as a **.NET tool** published on NuGet under the **mkdevforge** organization.

```bash
# .NET 8/9
dotnet tool install --global MkDevForge.Bbt

# .NET 10+ (dnx)
dnx MkDevForge.Bbt
```

The `dnx` command in .NET 10 is particularly interesting — it runs dotnet tools without requiring a separate global install step, lowering the barrier further for one-off use or CI/CD environments.

**NuGet package:** `MkDevForge.Bbt` (published under the mkdevforge org on nuget.org)

This gives us cross-platform support (Windows, macOS, Linux) via a single distribution mechanism that .NET developers already have available. No separate installers, no brew taps, no manual PATH configuration. For local development and testing, the package can be installed from a local source:

```bash
dotnet tool install --global --add-source ./nupkg MkDevForge.Bbt
```

## Authentication

`bbt` uses **Bitbucket API tokens** (the new standard replacing app passwords, which are fully sunset June 9, 2026). Tokens are scoped, expirable, and use HTTP Basic Auth with the user's Atlassian account email.

```
bbt auth login
```

The login flow prompts for the user's Atlassian email and API token, validates them against the Bitbucket API, and stores the credentials securely using the OS credential store (Windows Credential Manager, macOS Keychain, or a platform-appropriate secret store on Linux).

OAuth 2.0 support is a future consideration for multi-user/app scenarios but is not needed for the primary CLI use case.

Multiple workspace contexts should be supported for developers who work across several Bitbucket workspaces:

```
bbt auth login --workspace my-company
bbt auth switch my-company
bbt auth status
```

## Core command surface

The command surface is organized around the resources developers interact with daily. The priority order reflects the primary use case (AI-powered code review on PRs) and expands outward from there.

### Priority 1 — Pull requests (launch)

This is the core. Every command that an AI agent needs to read a PR, understand the changes, post review feedback at specific lines, and approve or request changes.

```
bbt pr list          List open pull requests
bbt pr view          View PR details (description, reviewers, status, checks)
bbt pr diff          Get the diff for a PR
bbt pr comments      List existing comments on a PR
bbt pr comment       Post a comment (general or inline at a specific file + line)
bbt pr review        Approve, request changes, or unapprove a PR
bbt pr create        Create a new pull request
bbt pr merge         Merge a pull request
bbt pr checkout      Check out a PR branch locally
```

The critical commands for AI code review are `diff`, `comment` (with inline support), and `review`. An AI agent workflow looks like:

```bash
# 1. Get the diff
bbt pr diff 42 --json

# 2. Post inline comments at specific lines
bbt pr comment 42 --file src/Services/Handler.cs --line 23 \
  --body "This null check should use the pattern matching syntax"

bbt pr comment 42 --file src/Models/Order.cs --line 87 --line-end 94 \
  --body "Consider extracting this validation logic into a specification class"

# 3. Approve or request changes
bbt pr review 42 --request-changes \
  --body "Two issues to address before merging — see inline comments"
```

### Priority 2 — Repositories

```
bbt repo list        List repositories in a workspace
bbt repo view        View repo details
bbt repo clone       Clone a repository
bbt repo browse      Open in browser
```

### Priority 3 — Pipelines

```
bbt pipeline list    List recent pipeline runs
bbt pipeline view    View a pipeline run's status and steps
bbt pipeline run     Trigger a pipeline
bbt pipeline logs    View step logs
```

### Priority 4 — Raw API access

An escape hatch for anything the CLI doesn't wrap yet:

```
bbt api GET /repositories/{workspace}/{repo}/pullrequests
bbt api POST /repositories/{workspace}/{repo}/pullrequests/42/comments \
  --input comment.json
```

This is critical for extensibility — users and AI agents can access any Bitbucket API endpoint without waiting for `bbt` to add explicit support.

### Future considerations

- `bbt pr pipeline` — View pipeline status for a PR's source branch
- `bbt snippet` — Manage Bitbucket snippets
- `bbt workspace` — Workspace-level operations
- `bbt search` — Code search across repositories

## Output design

Every command supports three output modes:

**Human-readable (default):** Formatted text suitable for terminal viewing. Color-coded where useful. Respects terminal width.

**JSON (`--json`):** Structured output with optional field selection. This is the mode AI agents and scripts will use.

```bash
# Full JSON output
bbt pr view 42 --json

# Selected fields only
bbt pr view 42 --json --fields title,state,author,reviewers

# Filtered with jq syntax
bbt pr list --json --jq '.[] | select(.author == "mikael")'
```

**Quiet (`--quiet`):** Minimal output — typically just IDs or success/failure. Useful in scripts where you only need the result.

```bash
PR_ID=$(bbt pr create --title "Fix handler" --source feature/fix --quiet)
bbt pr comment $PR_ID --file src/Handler.cs --line 42 --body "Added null check"
```

## Defaults and context awareness

`bbt` should be smart about defaults to minimize required arguments:

- **Workspace and repo** are inferred from the current git remote when inside a repository directory, like `gh` does.
- **Partial overrides are allowed:** `--workspace` and `--repo` override only that value; the other value is still resolved normally (env/profile/git), so the final context may combine sources.
- **The current branch** is used as the default source branch for `bbt pr create` and as a filter for `bbt pr view` (show the PR for the current branch).
- **Pagination** is handled automatically — commands like `bbt pr list` return all results by default, with `--limit` to cap output.

## MCP wrapper

The MCP server is a thin layer that exposes `bbt` commands as MCP tools. It can either shell out to the CLI binary or link directly against the same core library.

The MCP server is a separate concern and is not required for launch. Its value is for MCP-native clients (Claude Desktop, some IDE plugins) that prefer tool-based interaction over bash. The CLI remains the primary and recommended integration point.

## Rate limit awareness

Bitbucket Cloud allows 1,000 authenticated requests per hour (scaling to 10,000 for large workspaces with 100+ seats), compared to GitHub's 5,000. This is unlikely to be a practical problem for typical use — a full AI code review session (fetch diff + post 15 inline comments + approve) is roughly 20 requests — but it should inform design decisions throughout the tool:

- **Request what you need, not everything available.** Commands should use field filtering and partial responses where the API supports it, rather than fetching full objects and discarding data client-side.
- **Avoid redundant calls.** When used via the MCP wrapper (which maintains a long-running session), PR metadata fetched by one tool call shouldn't need re-fetching for the next. The MCP layer can cache frequently accessed data within a session with a short TTL. The CLI itself is stateless — each invocation is its own process — so this only applies to the MCP server.
- **Batch where possible.** If a future version supports posting multiple inline comments in one operation, prefer that over N individual calls — even though Bitbucket's API currently requires one call per comment.
- **Surface limits, don't hide them.** Include rate limit headers in `--verbose` output so users and agent developers can see where they stand. Automatic retry on 429s with backoff by default, opt-out with `--no-retry`.

This is a design consideration to keep in mind across all commands, not a problem to over-engineer upfront.

- **Not an AI code reviewer.** `bbt` posts comments — it doesn't generate them. The intelligence comes from the AI agent (Claude Code, Cursor, etc.) that decides *what* to say. `bbt` is the delivery mechanism.
- **Not a Bitbucket admin tool.** No user management, workspace settings, or project administration. There are commercial tools (Appfire CLI) for that.
- **Not a git replacement.** `bbt` complements git, not replaces it. Git operations (commit, push, pull, branch) stay with git. `bbt` handles the Bitbucket-specific layer on top.

## Success criteria

- A developer can install `bbt` with a single command and authenticate within 60 seconds.
- An AI agent (Claude Code / Cursor) can read a PR diff and post inline review comments at specific file + line locations in a Bitbucket PR using `bbt` commands, without any custom scripting.
- The command names and flags feel immediately familiar to anyone who has used `gh`.
- The tool works reliably within Bitbucket Cloud's 1,000 requests/hour rate limit for a typical code review session (reading one PR diff + posting 10–20 inline comments + approving).

## Open questions

1. **Naming:** Is `bbt` the right name? Alternatives: `bb`, `bitbucket`, `buck`. `bbt` avoids collision with existing tools and is short to type. We should verify no naming conflicts exist.
2. **Bitbucket Server/DC scope:** Should the initial release support Server/Data Center at all, or should it be Cloud-only with Server support as a documented future milestone?
3. **Comment threading:** Bitbucket supports reply threads on PR comments via `parent.id`. Should `bbt pr comment` support `--reply-to <comment-id>` from launch?
4. **MCP packaging:** Should the MCP wrapper ship as part of the same dotnet tool or as a separate package?
