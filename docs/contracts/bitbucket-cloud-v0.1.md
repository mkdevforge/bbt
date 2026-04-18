# Bitbucket Cloud API contracts (bbt v0.1)

This document pins the API contracts that `bbt` v0.1 depends on. It is intentionally narrow in scope: only the endpoints and fields used by the v0.1 command surface.

Last reviewed: 2026-04-17

## 1) Base contract

### Base URL

All requests are made against:

```
https://api.bitbucket.org/2.0
```

### Authentication (API token)

`bbt` uses **HTTP Basic Authentication** with the Atlassian account email address as the username and a **Bitbucket API token** as the password:

```
Authorization: Basic base64("<email>:<api_token>")
```

`bbt` never prints tokens and never logs the Authorization header.

### Headers we send

- `Accept: application/json` (default; overridden for diff download)
- `User-Agent: bbt/<version>`

### Error response shape (how we surface it)

Bitbucket error responses are typically JSON objects shaped like:

```json
{
  "type": "error",
  "error": {
    "message": "Human readable message",
    "detail": "More detail (optional)",
    "fields": { }
  }
}
```

`bbt` surfaces:
- HTTP status code + reason
- `error.message` / `error.detail` when present
- raw response text when the body is not valid JSON

## 2) Pagination + filtering/sorting

### Paging envelope

Most list endpoints return an object with:

```json
{
  "pagelen": 30,
  "page": 1,
  "size": 123,
  "next": "https://api.bitbucket.org/2.0/…?page=2",
  "values": [ ... ]
}
```

`bbt` follows `next` until it is absent, or until `--limit` is reached.

### Query params used by `bbt`

- `page` (int, optional)
- `pagelen` (int, optional; we default to 50)
- `state` (string; for PR list: `OPEN|MERGED|DECLINED|SUPERSEDED`)

Note: Bitbucket Cloud also supports query filtering via `q` on many endpoints. `bbt` v0.1 **does not depend** on `q` for “PR by branch” resolution; it lists open PRs and filters client-side.

## 3) Endpoint contracts (v0.1)

In all paths below:
- `{workspace}` is the workspace slug
- `{repo_slug}` is the repository slug
- `{pull_request_id}` is the PR integer id

### 3.1 Auth validation

#### `GET /user`

Used by:
- `bbt auth login` (validate credentials)
- `bbt auth status --check`

Request:

```
GET https://api.bitbucket.org/2.0/user
Accept: application/json
Authorization: Basic …
```

Responses:
- `200 application/json`: current user object
- `401 application/json`: invalid/missing credentials

Minimal fields `bbt` relies on:
- `display_name` (string)
- `nickname` (string)
- `uuid` (string)

#### `GET /workspaces/{workspace}`

Used by:
- `bbt auth login --workspace <slug>` (validate workspace slug and access)

Responses:
- `200 application/json`: workspace object
- `401 application/json`: invalid/missing credentials
- `404 application/json`: workspace not found / no access

Minimal fields `bbt` relies on:
- `slug` (string)
- `name` (string)

### 3.2 Pull requests

#### `GET /repositories/{workspace}/{repo_slug}/pullrequests`

Used by:
- `bbt pr list`
- `bbt pr view` (when `<id>` is omitted; list+filter by `source.branch.name`)
- `bbt pr summary` (when `<id>` is omitted; list+filter by `source.branch.name`)
- `bbt pr diff` (when `<id>` is omitted)
- `bbt pr comments` (when `<id>` is omitted)

Query parameters:
- `state` (repeatable, optional; default behavior returns OPEN only)
- `page`, `pagelen` (paging)

Responses:
- `200 application/json`: paginated PR list
- `401` (may be empty body)
- `404 application/json`: repo not found/no access

Minimal fields `bbt` relies on:
- `id` (int)
- `title` (string)
- `state` (string)
- `author` (object; display name/nickname/uuid)
- `source.branch.name` (string)
- `destination.branch.name` (string)
- `links.html.href` (string)
- `created_on`, `updated_on` (ISO 8601)

#### `GET /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}`

Used by:
- `bbt pr view <id>`
- `bbt pr summary <id>`

Responses:
- `200 application/json`: PR object
- `401` (may be empty body)
- `404 application/json`: not found/no access

Minimal fields `bbt` relies on (in addition to list fields):
- `description` (string)
- `comment_count` (int; total PR comments count)
- `reviewers[]` (array of accounts)
- `participants[]` (array; used for “approved/changes_requested” state)

#### `GET /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/activity`

Used by:
- `bbt pr summary <id>` (only when the PR state is `MERGED`, to derive `mergedAt`)

Responses:
- `200 application/json`: paginated activity log
- `401` (may be empty body)
- `404 application/json`: not found/no access

Minimal fields `bbt` relies on:
- `values[].update.state` (string; `MERGED` entries indicate the merge state transition)
- `values[].update.date` (ISO 8601; exact timestamp used for `mergedAt`)

Notes:
- `bbt` does not use `updated_on` as a merged timestamp fallback.
- If no activity entry with `update.state == MERGED` is returned, `mergedAt` is emitted as `null`.

#### `GET /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/diff`

Used by:
- `bbt pr diff`

Contract notes:
- The endpoint responds with **`302` redirect** to a diff resource. The client must follow the redirect and download the diff text.
- The redirected resource is typically `text/plain` unified diff.

Responses:
- `302` (no body) with `Location: …`

#### `GET /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/comments`

Used by:
- `bbt pr comments`

Responses:
- `200 application/json`: paginated comment list
- `403 application/json`: insufficient permission
- `404 application/json`: PR not found/no access

Minimal fields `bbt` relies on:
- `id` (int)
- `content.raw` / `content.html` (string)
- `inline` (object; see below)
- `user` (account)
- `created_on` (ISO 8601)
- `links.html.href` (string)

#### `POST /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/comments`

Used by:
- `bbt pr comment`
- `bbt pr review` (when `--body*` is provided; posts a global comment first)

Request body:

```json
{
  "content": { "raw": "text", "markup": "markdown" },
  "inline": {
    "path": "src/Foo.cs",
    "to": 42,
    "start_to": 40
  }
}
```

Rules:
- `content.raw` is required.
- `content.markup` is always `"markdown"` in v0.1.
- Inline anchor is optional.
- `inline.path` is required when anchoring.
- New-side (default): use `to` (single line) or `start_to` + `to` (range).
- Old-side (for deletions): use `from` (single line) or `start_from` + `from` (range).

Responses:
- `201 application/json`: created comment object
- `403 application/json`: insufficient permission
- `404 application/json`: PR not found/no access

### 3.3 Review actions

#### `POST /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/approve`

Used by:
- `bbt pr review --approve`

Responses:
- `200 application/json`: participant object
- `401 application/json`: invalid/missing credentials
- `404 application/json`: not found/no access

#### `DELETE /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/approve`

Used by:
- `bbt pr review --unapprove`

Responses:
- `204` (no body)
- `400 application/json`: invalid state (e.g., unapprove when not approved)
- `401 application/json`
- `404 application/json`

#### `POST /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/request-changes`

Used by:
- `bbt pr review --request-changes`

Responses:
- `200 application/json`: participant object
- `400 application/json`: invalid state
- `401 application/json`
- `404 application/json`

#### `DELETE /repositories/{workspace}/{repo_slug}/pullrequests/{pull_request_id}/request-changes`

Used by:
- `bbt pr review --unrequest-changes`

Responses:
- `204` (no body)
- `400 application/json`
- `401 application/json`
- `404 application/json`

## 4) CLI → API mapping table (v0.1)

| CLI command | HTTP calls | Notes |
| --- | --- | --- |
| `bbt auth login` | `GET /user` (+ `GET /workspaces/{workspace}` when `--workspace`) | Validates before storing token. |
| `bbt auth status --check` | `GET /user` | `--check` is the only mode that hits the network. |
| `bbt pr list` | `GET /repositories/{ws}/{repo}/pullrequests?state=…` (paged) | Defaults to `state=OPEN`. |
| `bbt pr view <id>` | `GET /repositories/{ws}/{repo}/pullrequests/{id}` | If `<id>` omitted: list open PRs and filter by branch name. |
| `bbt pr summary <id>` | `GET /repositories/{ws}/{repo}/pullrequests/{id}` + `GET /repositories/{ws}/{repo}/pullrequests/{id}/diff` + `GET /repositories/{ws}/{repo}/pullrequests/{id}/activity` (MERGED only) | Uses `comment_count` from the PR object; `mergedAt` comes from the latest activity `update.state == MERGED`, else `null`. |
| `bbt pr diff <id>` | `GET /repositories/{ws}/{repo}/pullrequests/{id}/diff` (follow 302) | Human mode prints raw diff; JSON mode parses diff. |
| `bbt pr comments <id>` | `GET /repositories/{ws}/{repo}/pullrequests/{id}/comments` (paged) | If `<id>` omitted: resolve PR by branch first. |
| `bbt pr comment <id>` | `POST /repositories/{ws}/{repo}/pullrequests/{id}/comments` | Inline anchor uses `inline.*` fields. |
| `bbt pr review …` | (optional) `POST …/comments` then `POST`/`DELETE` approve/request-changes | Body is posted as a global comment. |
| `bbt api …` | Any method/path | Optional `--paginate` merges `values` from `next` pages. |

## 5) Contract verification checklist (manual)

The commands below use env vars so you can copy/paste without committing secrets:

```bash
export BBT_EMAIL='user@example.com'
export BBT_TOKEN='bitbucket_api_token'
export BBT_WORKSPACE='my-workspace'
export BBT_REPO='my-repo'
```

### Validate auth

```bash
curl -u \"$BBT_EMAIL:$BBT_TOKEN\" \\
  -H 'Accept: application/json' \\
  'https://api.bitbucket.org/2.0/user'
```

### Validate workspace

```bash
curl -u \"$BBT_EMAIL:$BBT_TOKEN\" \\
  -H 'Accept: application/json' \\
  \"https://api.bitbucket.org/2.0/workspaces/$BBT_WORKSPACE\"
```

### List PRs (open)

```bash
curl -u \"$BBT_EMAIL:$BBT_TOKEN\" \\
  -H 'Accept: application/json' \\
  \"https://api.bitbucket.org/2.0/repositories/$BBT_WORKSPACE/$BBT_REPO/pullrequests?state=OPEN&pagelen=50\"
```

### Fetch diff (follow redirect)

```bash
curl -u \"$BBT_EMAIL:$BBT_TOKEN\" -v \\
  \"https://api.bitbucket.org/2.0/repositories/$BBT_WORKSPACE/$BBT_REPO/pullrequests/<PR_ID>/diff\"
```

### Post a global PR comment

```bash
curl -u \"$BBT_EMAIL:$BBT_TOKEN\" \\
  -H 'Content-Type: application/json' \\
  -d '{\"content\":{\"raw\":\"hello from curl\",\"markup\":\"markdown\"}}' \\
  \"https://api.bitbucket.org/2.0/repositories/$BBT_WORKSPACE/$BBT_REPO/pullrequests/<PR_ID>/comments\"
```

### Post an inline PR comment

```bash
curl -u \"$BBT_EMAIL:$BBT_TOKEN\" \\
  -H 'Content-Type: application/json' \\
  -d '{\"content\":{\"raw\":\"inline comment\",\"markup\":\"markdown\"},\"inline\":{\"path\":\"src/Foo.cs\",\"to\":42}}' \\
  \"https://api.bitbucket.org/2.0/repositories/$BBT_WORKSPACE/$BBT_REPO/pullrequests/<PR_ID>/comments\"
```
