## Issues

Issue titles MUST be descriptive natural-language titles.

Do NOT use Conventional Commit syntax for issue titles.

Prefer:

```text
Map should preserve null values
Add pairwise function
Reduce allocations when mapping arrays
```

Avoid:

```text
fix: preserve null values when mapping arrays
feat: add pairwise function
perf: reduce allocations when mapping arrays
```

Every issue MUST have exactly one change-type label:

* `bug` for a defect
* `new-feature` for new functionality
* `enhancement` for an improvement or refactoring of existing functionality

The label is determined by the nature of the issue.

## Branches and worktrees

Every coding task MUST be performed in its own dedicated worktree and task branch.

For a new task:

1. Fetch the latest remote state.
2. Create the task branch from the latest `origin/main`.
3. Create or use a dedicated worktree for that branch.

Branch names MUST describe the nature of the change:

* `fix/<name>` for bug fixes and incorrect behavior
* `feat/<name>` for new functionality
* `refactor/<name>` for internal restructuring without intended behavior changes
* `perf/<name>` for performance improvements
* `docs/<name>` for documentation-only changes
* `test/<name>` for test-only changes
* `chore/<name>` for maintenance work that does not fit another category

When asked to fix a bug, defect, regression, or issue describing incorrect behavior, the branch MUST use the `fix/` prefix.

For example:

```text
fix/multiline-source-spans
feat/record-access-expression
refactor/parser-bindings
test/incomplete-pipeline-cases
```

Do NOT use tooling-specific prefixes such as:

```text
codex/
chatgpt/
```

The agent performing the task MUST NOT affect the branch name.

Branch names SHOULD be derived from the nature or title of the issue and SHOULD NOT contain the issue number.

## Conventional Commits

Commit messages and pull request titles MUST use the following form:

```text
<type>(<scope>): <description>
```

The scope is optional. When the description starts with a word, that word MUST start
with a lowercase letter. This is a repository convention in addition to the
Conventional Commits specification.

Use `ci` for CI configuration and scripts, including GitHub Actions workflows and
their dependencies. Use `build` for the build system and external project/build
dependencies, including NuGet, npm, and pip dependencies. These types both map to
the repository's `build` label, but they are not semantically interchangeable.

Prefer:

```text
ci(deps): bump actions/checkout from 6 to 7
feat(parser): add array accessor
fix(spans): preserve multiline token boundaries
test(grammar): cover incomplete record access
```

Avoid:

```text
ci(deps): Bump actions/checkout from 6 to 7
feat(parser): Add array accessor
```

## Dependencies

`Expressif.Syntax` is the canonical parser and syntax-tree implementation for the
Expressif language.

It MUST remain independent of consumers such as `Expressif` and
`Expressif.LanguageServer`.

`Expressif.Syntax` MUST NOT depend on:

* `Expressif.LanguageServer`
* an LSP framework such as OmniSharp
* editor-specific integrations
* semantic or binding libraries merely to support a downstream consumer

Protocol, editor, binding, and semantic concerns belong in their respective
consumer repositories unless they are genuinely part of the syntax model.

Before adding a dependency, confirm that it belongs at the parser or syntax-tree
layer rather than in a downstream consumer.

## Testing

Tests SHOULD be added at the lowest layer that owns the behavior.

When fixing a defect, add or update a test that demonstrates the failing behavior
whenever practical.

Grammar, parser, syntax-tree, token, diagnostic, source-position, and binding
behavior SHOULD be tested directly without relying solely on downstream
`Expressif` or language-server tests.

Tests involving source positions or spans SHOULD explicitly cover relevant
boundary-sensitive cases, such as:

* beginning and end of input
* multiline expressions
* incomplete or malformed syntax
* zero-length spans
* spans crossing lines

## Skills

Repository-specific workflows are defined under `.github/skills/`.

When a task matches an existing skill, read and follow that skill before making changes.

Skills define task-specific procedures. `AGENTS.md` defines repository-wide rules
and takes precedence if a skill contains conflicting Git, worktree, branch, issue,
commit, pull-request, testing, dependency, or architectural instructions.

## Pull requests

For every completed implementation:

1. Push the task branch.
2. Create a GitHub pull request targeting `main`.
3. Use a Conventional Commit-style PR title.
4. Include a concise description of the change.
5. Include the relevant tests or validation performed.
6. Link the pull request to the corresponding issue when one exists using wording
   that closes the issue.

Do NOT use `bug`, `new-feature`, or `enhancement` labels on the pull request unless explicitly requested.

Pull requests SHOULD remain focused on one coherent change.

Avoid unrelated cleanup or refactoring unless it is necessary to implement the
requested change.

## Completion criteria

A coding task is complete only when:

* implementation was performed in the task's dedicated worktree;
* for a new task, the branch was created from the latest `origin/main`;
* the branch name follows the repository branch naming rules;
* repository dependency boundaries are preserved;
* the solution builds successfully;
* the relevant tests have been run;
* regression coverage was added or updated for corrected behavior whenever practical;
* all intended changes are committed;
* commit messages follow Conventional Commits;
* the branch has been pushed;
* a pull request targeting `main` has been created;
* the PR title follows Conventional Commits;
* the corresponding issue has the appropriate `bug`, `new-feature`, or `enhancement` label;
* the pull request is linked to the issue when one exists;
* the worktree is clean.
