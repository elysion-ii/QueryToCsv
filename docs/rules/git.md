# Git

Rules for Git write operations and pull request operations, distributed into each
repository at `docs/rules/git.md`. Read this file before implementing any change, as
`standard.md` How to use these rules directs, and consult it again before every commit,
branch, push, and pull request.

## Confirming Scope

How far a task goes is the user's decision, not something to infer from the work being
complete. Agree on the destination before the first Git write operation.

Ask how far to go:

1. Commit only
2. Commit and push
3. Through PR creation
4. All the way — PR, merge, return to the default branch, delete the branch

Stop at the agreed point and report. Stopping early is normal: a branch holding unpushed
or unmerged commits is a valid resting state, not unfinished work.

Never go beyond the agreed point. Do not push, open a PR, or merge because the work looks
ready.

## Operation Language

Git text is typed back into shell commands — branch names into `git checkout`, messages
into `-m`, tags into `git push`. Non-ASCII text there is a standing source of mistakes:
mistyped names, pathspecs that match nothing, garbled output. English is also the common
language of the tooling and of everyone who reads the history.

- Write all Git-related text in English by default: commit messages, branch names, PR titles and descriptions, tags, and the like

## Commit Format

- Use clear, concise descriptions of what was changed
- Follow the conventional commit format: `feat:`, `fix:`, `refactor:`, `docs:`, `build:`, `chore:`, etc.

## Branches

- Treat the default branch as protected, whether or not the repository actually enforces it: never commit on it. Create a branch first, from the default branch's current state. Work on the default branch only when the user explicitly asks for it
- Branch names are kebab-case and describe the change; a type prefix mirroring the commit type (`feat/`, `fix/`, `docs/`, `chore/`, ...) is recommended, not required
- A branch carries as many commits as the work needs — one PR does not mean one commit
- Delete branches per Branch Cleanup below once their PR is merged

## Pull Requests

- Landing a branch on the default branch always goes through a PR; never merge a branch locally
- Give every PR a clear title and a description of what changed and why
- Before relying on a PR's diff, composing a merge message, or merging, confirm the PR still reflects the intended change — re-check with `gh pr view` if commits were pushed after opening

## Merge Strategy

- Merge PRs with `--squash` by default; use `--merge` / `--rebase` only when explicitly instructed

## Squash Commit Message Composition

When squash-merging, always specify the final commit message explicitly. Never rely on
the tool's defaults, the PR title, the PR body, or auto-generated co-author text.

- **Subject**: a concise statement of what the change does, in the Operation Language
- **Body**: summarize what changed and why at a level useful when reading the history during a future review. Not a copy of the PR description — distill it
- **Nothing useful beyond the subject**: set an explicitly empty body instead of letting the tool generate one

## Pre-merge Verification

- Check the PR state with `gh pr view` before merging: approvals, CI checks, merge conflicts
- If checks are failing, report and stop — do not merge over a red CI without the user's explicit instruction

## Merge Procedure

1. Determine the strategy per Merge Strategy above (squash unless overridden)
2. Compose the subject and body from the PR's actual diff and commits, following Squash Commit Message Composition. Pass them explicitly with `--subject` and `--body` (or `--body-file`); use `--body ""` when nothing useful exists beyond the subject
3. Merge, naming the strategy explicitly: `gh pr merge <number> --squash --subject "..." --body "..."` (substitute the chosen strategy's flag when Merge Strategy calls for `--merge` or `--rebase`)

## Post-merge Verification

- Verify the PR state is `MERGED` (`gh pr view`) and report the final commit message

## Branch Cleanup

- After a PR is merged, delete the source branch on both the remote and the local clone (`gh pr merge --delete-branch` does both in one step)
- Under squash merging, `git branch -d` does not recognize the branch as merged — the default branch received a different commit SHA. After confirming the PR is merged, delete with `git branch -D <branch>`
- Prune stale remote-tracking refs with `git fetch --prune`
