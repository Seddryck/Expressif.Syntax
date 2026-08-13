---
name: implement-syntax
description: Implement a syntax feature from a GitHub issue across the Tree-sitter grammar, generated parser artifacts, typed C# syntax tree, binder, and tests. Use for `/implement-syntax`, `$implement-syntax`, or any issue-driven addition or change to Expressif syntax.
---

# Implement Syntax

Implement syntax features end to end while preserving the authored source in the concrete syntax tree (CST).

## 1. Investigate

1. Read the repository `AGENTS.md` and follow its issue, worktree, Git, and pull-request requirements.
2. Resolve `<issue>` as an issue number or URL, then read the complete issue, including relevant comments and linked context.
3. Inspect `grammar.js` around every affected construct.
4. Trace the corresponding generated Tree-sitter nodes, typed C# syntax nodes, binder cases, and tests to learn the current conventions.
5. Keep the change syntactic. Leave semantic or runtime validation downstream unless the issue explicitly requires it.

## 2. Design the CST before editing

Form explicit alternatives for the intended CST shape. For each viable choice, present:

- Tree-sitter node name;
- typed hierarchy, such as `ExpressionSyntax` or `ValueSyntax`;
- named fields;
- child nodes and their source order;
- whether punctuation or other authored tokens appear in `Children`;
- how `Text` and source ranges behave;
- whether the source form remains distinct or is lowered into another construct.

Recommend one choice and explain the material tradeoffs. Prefer a lossless CST: preserve shorthand and specialized authored syntax instead of prematurely lowering it to function calls or another semantic representation.

Pause and ask the user to confirm the CST design. Do not modify code, generated artifacts, or tests until the user confirms a choice. If the user changes the design, revise the proposal and confirm it again.

## 3. Implement the confirmed design

After confirmation:

1. Modify `grammar.js`.
2. Regenerate Tree-sitter artifacts with the repository's generation command. Never hand-edit generated files.
3. Update the typed C# model:
   - add the `SyntaxKind` member;
   - add or update the appropriate `*Syntax` node;
   - use the confirmed inheritance;
   - expose clear public semantic properties.
4. Update the C# binder for every new Tree-sitter node. Do not leave raw new node kinds unbound.
5. Treat the feature as incomplete until the typed C# syntax tree exposes it cleanly.

## 4. Test the complete syntax surface

Add focused tests for every applicable behavior:

- standalone syntax;
- composition as an argument, pipeline element, or compound construct;
- exact node type and `SyntaxKind`;
- public properties;
- `Children`, including order;
- `Text` and preservation of authored syntax;
- source spans or ranges;
- malformed or incomplete input and recovery behavior.

Use the confirmed CST design as the test contract. Avoid asserting semantic or runtime rules unless the issue explicitly includes them.

## 5. Validate and deliver

1. Run Tree-sitter generation validation and the Tree-sitter test suite using the repository commands.
2. Run the relevant .NET tests, expanding to the full .NET test suite when practical.
3. Review generated diffs to verify they result only from regeneration.
4. Confirm every new Tree-sitter node has a typed binder path and every public typed property is tested.
5. Complete the repository's required commit, push, issue-label, pull-request, and clean-worktree workflow.
6. Report the CST decision, changed layers, validation commands and results, and pull request.
