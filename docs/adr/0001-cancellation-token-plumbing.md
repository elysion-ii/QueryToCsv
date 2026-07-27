---
status: accepted
---

# Plumb CancellationToken Before Any Caller Supplies a Real Token

## Background

While no caller supplies a real token yet — the GUI has no cancel button, the console app has not wired Ctrl+C — it is tempting to omit `CancellationToken` parameters ("nobody passes a real token anyway") or to pass `CancellationToken.None` at call sites.

That shortcut has a deferred cost: retrofitting a token into a helper later is a cross-layer change — the helper signature plus every call site must change in one commit. Every helper that skips the token today becomes the same multi-file retrofit tomorrow, and the omission spreads by copy-paste in the meantime.

## Decision

All async methods that perform potentially long I/O (DB, file, network, external process) accept and propagate `CancellationToken`, regardless of whether any caller supplies a real token yet — including static helpers and feature-internal classes, not just service entry points.

The token parameter is cheap to carry (a defaulted parameter, one argument per await), while retrofitting it later costs a cross-layer signature change per helper. Plumbing it up front means connecting a real cancellation source becomes an entry-point-only change: a GUI adds a cancel button backed by a `CancellationTokenSource`; a console app hooks `Console.CancelKeyPress` (or uses the Generic Host stopping token) so Ctrl+C becomes graceful cooperative cancellation instead of an abrupt process kill. Either way, the token just flows into calls that are already tokened.

Operations that must run to completion are deliberately excluded and must NOT take a token: logging, transaction rollback, best-effort cleanup, and post-commit finalization. Cancelling these would lose diagnostic records, mask original exceptions, or leave files inconsistent with committed data. The exact list lives in `docs/rules/dotnet.md`, CANCEL.

## Alternatives Considered

- **Add tokens only when a cancel UI ships** — rejected: each retrofit is a multi-file, cross-layer change, and the omission spreads by copy-paste in the meantime.
- **Plumb tokens everywhere including cleanup/logging paths** — rejected: cancelling completion-critical work creates worse failure modes than the wait it saves.

## Consequences

- Every async I/O method signature carries the `CancellationToken ct = default` boilerplate — one extra parameter, one extra argument per await, accepted as the standing cost.
- In exchange, connecting a real cancellation source (a cancel button, Ctrl+C) is an entry-point-only change; the multi-layer signature retrofit never happens.

## Applicability

Applies to all async I/O in QueryToCsv. Fully synchronous code paths are out of scope until async I/O is introduced.
