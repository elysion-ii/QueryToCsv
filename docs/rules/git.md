# Git

Rules for Git write operations and pull request operations, distributed into each
repository at `docs/rules/git.md`. Read this file together with `standard.md` whenever a
task commits, creates or updates a branch, or creates, updates, or merges a pull
request.

## Operation Language

- Write all Git-related text in English by default: commit messages, branch names, PR titles and descriptions, tags, and the like
- If the application rules file specifies another language (e.g., Japanese), follow it instead
- When no language is specified, English is the default

## Commit Format

- Use clear, concise descriptions of what was changed
- Follow the conventional commit format: `feat:`, `fix:`, `refactor:`, `docs:`, `build:`, `chore:`, etc.

## Branches

- Create a new branch from the default branch's current state for a set of related changes; do not commit directly to the default branch when a PR-based review is expected
- Branch names are kebab-case and describe the change; a type prefix mirroring the commit type (`feat/`, `fix/`, `docs/`, `chore/`, ...) is recommended, not required
- Delete branches per Branch Cleanup below once their PR is merged

## Pull Requests

- Give every PR a clear title and a description of what changed and why
- Before relying on a PR's diff, composing a merge message, or merging, confirm the PR still reflects the intended change — re-check with `gh pr view` if commits were pushed after opening

## Merge Strategy

- Merge PRs with `--squash` by default; use `--merge` / `--rebase` only when explicitly instructed or specified by the application rules file

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
