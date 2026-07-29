# Security Review

Read:
- _Project rules (context, architecture, Unity constraints) auto-load via CLAUDE.md / .cursor/rules._
- `$ARGUMENTS`

Review the **current git diff** for security issues in a Unity **client** game. This is a
focused security pass, complementary to `/review-task` (which covers architecture, lifecycle,
and leaks).

**Scope discipline:** if `/build-feature` dispatched this with an exact `git diff` command
(base ref + `-- <paths>`), use only that command. Do not derive scope yourself by comparing
branches (`git diff main`, `git diff main...HEAD`) — on a long-lived repo the target branch
can be thousands of commits behind, turning a small feature diff into the whole repo's
history. If no scope was given, review the current working-tree diff only.

Claude Code users may also run the built-in `/security-review`; this command defines the project-specific checklist both tools follow.

## Arguments

```text
/security-review fishing-flow-ab-test
```

Or a task path, or "current diff". If `$ARGUMENTS` is empty, review the current working diff.

## What to check (Unity client focus)

1. **Secrets & keys** — no hardcoded API keys, tokens, passwords, connection strings, or private endpoints in code, configs, or committed assets.
2. **Network** — no plaintext `http://` for sensitive calls; no disabled/blind TLS certificate validation; no secrets or PII in URLs/query strings.
3. **Client trust boundary** — reward / currency / progression / outcome decisions stay **server-authoritative** (e.g. Fishing Fortune's server-decided catch); the client must not grant value it can fabricate.
4. **Deserialization / untrusted input** — no unsafe deserialization of server, remote-config, or storage data into arbitrary types; validate and bound values; safe fallback (e.g. `Classic` flow) on malformed input.
5. **PII & analytics** — no personal or sensitive data sent to GameAnalytics / analytics beyond what is intended and consented; no user identifiers leaking into logs.
6. **Injection / dynamic code** — no `eval`-style dynamic code, reflection over untrusted names, or unsafe file-path construction from untrusted input.
7. **Logging** — no secrets, tokens, or PII written to `Debug.Log` / console / analytics breadcrumbs.
8. **Dependencies** — flag any new package or `manifest.json` change that pulls untrusted or known-vulnerable code.

## Severity & fix policy

- **Critical** — exploitable secret leak, broken client-trust boundary, or remote-code / unsafe-deserialization risk → **STOP**, must fix.
- **High** — insecure transport, PII leak, or unsafe input handling → fix before done.
- **Medium / Low** — hardening and robustness; record, fix if cheap.
- Fix **only Critical / High** here; do not expand scope.

## Output

List findings grouped Critical / High / Medium / Low, each with: issue, why it matters, minimal fix. End with **Approved / Not approved**. Optionally save to `AI/features/<feature-id>/reviews/SECURITY.md`.
