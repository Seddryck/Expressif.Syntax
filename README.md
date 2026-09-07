# Expressif.Syntax

![Expressif logo](misc/icon/expressif-icon-256.png)

`Expressif.Syntax` provides the [Tree-sitter](https://tree-sitter.github.io/tree-sitter/) parser for the [Expressif](https://github.com/Seddryck/Expressif) expression language, together with bindings for supported programming languages.

The parser defines the concrete syntax of Expressif independently from its runtime implementations. It is intended to provide a common syntax foundation for the C#, Python and TypeScript implementations of Expressif, as well as editor tooling and language-server support.

[About](#about) | [Repository structure](#repository-structure) | [Development](#development) | [Releases](#releases)

## About

**Project:** [![Expressif](https://img.shields.io/badge/Expressif-language-fe762d.svg)](https://github.com/Seddryck/Expressif)
[![Tree-sitter](https://img.shields.io/badge/parser-Tree--sitter-6a9f58.svg)](https://tree-sitter.github.io/tree-sitter/)

**Releases:** [![nuget](https://img.shields.io/nuget/v/Expressif.Syntax.svg)](https://www.nuget.org/packages/Expressif.Syntax/) [![GitHub Release](https://img.shields.io/github/v/release/Seddryck/Expressif.Syntax)](https://github.com/Seddryck/Expressif.Syntax/releases/latest)
[![GitHub Release Date](https://img.shields.io/github/release-date/Seddryck/Expressif.Syntax.svg)](https://github.com/Seddryck/Expressif.Syntax/releases/latest)
[![licence badge](https://img.shields.io/badge/License-Apache%202.0-yellow.svg)](https://github.com/Seddryck/Expressif.Syntax/blob/main/LICENSE)

**Dev. activity:** [![GitHub last commit](https://img.shields.io/github/last-commit/Seddryck/Expressif.Syntax.svg)](https://github.com/Seddryck/Expressif.Syntax/commits)
![Still maintained](https://img.shields.io/maintenance/yes/2026.svg)
![GitHub commit activity](https://img.shields.io/github/commit-activity/y/Seddryck/Expressif.Syntax)

**Continuous integration builds:** [![CI](https://github.com/Seddryck/Expressif.Syntax/actions/workflows/ci.yml/badge.svg)](https://github.com/Seddryck/Expressif.Syntax/actions/workflows/ci.yml)

**Status:** [![stars badge](https://img.shields.io/github/stars/Seddryck/Expressif.Syntax.svg)](https://github.com/Seddryck/Expressif.Syntax/stargazers)
[![Bugs badge](https://img.shields.io/github/issues/Seddryck/Expressif.Syntax/bug.svg?color=red\&label=Bugs)](https://github.com/Seddryck/Expressif.Syntax/issues?q=is%3Aissue+is%3Aopen+label%3Abug)
[![Features badge](https://img.shields.io/github/issues/Seddryck/Expressif.Syntax/new-feature.svg?color=purple\&label=Feature%20requests)](https://github.com/Seddryck/Expressif.Syntax/issues?q=is%3Aissue+is%3Aopen+label%3Anew-feature)

## Purpose

Expressif.Syntax separates the syntax of the Expressif language from its runtime semantics.

The Tree-sitter grammar parses source text into a syntax tree while preserving syntactic constructs such as shorthands. Language-specific bindings can then translate this tree into the semantic representation expected by an Expressif implementation.

For example:

```text
.foo
```

is represented syntactically as a field-access construct, while:

```text
field(foo)
```

is represented as a regular function call. Both can later be bound to the same semantic operation:

```text
field(foo)
```

This separation allows the same parser to support:

* Expressif for C#
* Expressif for Python
* Expressif for TypeScript
* language servers
* syntax highlighting and other editor tooling

### Structural access

Expressif distinguishes record fields from elements of ordered values:

```text
.name       named field of the current record
.0          positional field of the current record
^.name      named field of the current expression root
^.0         positional field of the current expression root
^^.name     named field of the enclosing expression root
$0          first element of the current tuple or array
$1          second element of the current tuple or array
$^0         last element of the current tuple or array
$^1         second-to-last element of the current tuple or array
^$1         second element of the current expression input
^^$1        second element of the enclosing expression input
^^^$1       second element two expression scopes outward
```

Record access always uses `.` for navigation. A leading `^` changes the root
from the current pipeline value to the current expression input; each additional
`^` moves outward by one enclosing expression. It does not change how fields are
selected. Access can be chained for nested records, for example
`.customer.address`, `^.customer.0`, or `^^.customer.0`. The former bracket forms
`[name]` and `[0]` are replaced by `^.name` and `^.0` respectively.

Element positions are zero-based. `$n` counts from the beginning and `$^n`
counts from the end. The parser represents both tuple and array access with the
same `tuple_projection` node; downstream binders decide whether the
runtime value supports positional access and how invalid or out-of-range access
is handled.

Leading carets on tuple projections follow the same root-depth convention as
record access: `^^^^$1` and `^^^^.name` both have root depth 4. Any positive
number of carets is supported. `TupleProjectionSyntax.Root` preserves the prefix
as an `ExpressionRootSyntax`, including its `$` delimiter, text, and source span;
`RootDepth` counts the carets (zero for unqualified projections). `Direction` and
`Index` remain independent properties. The root is included in `Children`.

`^$1` selects from an expression input; `$^1` counts from the end of the current
value. Combined forms such as `^^$^1`, negative indexes, and whitespace inside
the shorthand are invalid. Pipeline stages and grouping do not create scopes;
nested expression invocations do. Resolving those scopes belongs to downstream
evaluation, while the syntax tree preserves the authored reference directly.

### Current object and spread

`@_` denotes the current pipeline object as a single value. Spread syntax is
orthogonal: `...` spreads the current object implicitly, while `...expression`
spreads an explicit expression.

```text
@_                 current object as one value
...                spread the current object (shorthand for ...@_)
...@_              explicitly spread the current object
...@args           spread the variable @args
...args            spread the result of the zero-argument function args
```

The distinction applies consistently to function arguments, arrays, and named
record fields:

```text
array(@args)         variable as one positional argument
array(...@args)      variable spread into the function argument list
{1, @_, 3}         current object as one array element
{1, ..., 3}        current object spread into an array
{1, ...@args, 3}   variable @args spread into an array
{foo := @_}        current object as a normal field value
{foo := ...}       current object spread into the field
{foo := ...@args}  variable @args spread into the field
```

The syntax tree preserves whether a spread operand was implicit or explicitly
authored. Parsing records the intent to spread but does not expand or validate
the runtime value.

## Repository structure

```text
.
├── grammar.js
├── tree-sitter.json
├── package.json
├── src/
│   ├── parser.c
│   ├── grammar.json
│   └── node-types.json
├── bindings/
│   ├── csharp/
│   ├── python/
│   └── typescript/
├── queries/
│   └── highlights.scm
└── test/
```

`grammar.js` is the source definition of the Expressif grammar.

The files under `src/` are generated by Tree-sitter and are committed to source control so consumers do not need the Tree-sitter CLI to build the parser.

Language-specific integration is located under `bindings/`.

## Input binding pipeline stages

A binding must follow a pipe and a preceding expression in the same pipeline.
It binds the output of that preceding expression and preserves a dedicated body:

```expressif
Tuple(10, 20) | extend(30) | myTuple :> (@myTuple | $0 | subtract(@myTuple | $1) | multiply(@myTuple | $2))
record(name := "Alice", age := 30) | :> record(n := .name, a := .age)
Tuple(10, 20) | extend(30) | (current, minus, factor) :> (@current | subtract(@minus) | multiply(@factor))
```

Bindings also work inside an argument's own pipeline:

```expressif
apply(trim | input :> (@input | upper))
adjacent($1 | current :> (@current | multiply(2)))
apply(expression := trim | input :> @input | upper)
```

In the second example, the preceding `$1` supplies the value being bound.
Implicit input supplied by a function does not satisfy the syntactic requirement
for a preceding expression. These leading forms are **invalid**, including under
named arguments or extra grouping parentheses:

```expressif
apply(input :> @input)
apply(:> $0 | add($1))
adjacent((previous, current) :> @current | subtract(@previous))
map(input :> @input)
apply(expression := input :> @input)
apply((input :> @input))
```

Bare `name :> body`, `:> body`, and `(a, b) :> body` are also invalid without a
preceding pipeline expression. Ordinary expression arguments and callable
shorthands such as `apply(trim | upper)` and `adjacent(subtract)` remain valid.
No function names are special-cased by the placement rule.

### Body boundaries and spacing

Without parentheses, the body consumes the remaining pipeline through its
enclosing expression or argument boundary. Parentheses delimit the body explicitly;
a following pipe continues the outer pipeline:

```expressif
10 | input :> @input | add(1) | multiply(2)
10 | input :> (@input | add(1)) | multiply(2)
10 | :> (@_ | add(1)) | multiply(2)
10|:>(@_ | add(1))|multiply(2)
```

The last two examples have the same syntax structure: `| :>` and `|:>` are
equivalent. The `:>` token itself must remain contiguous; `: >` is invalid.
Whitespace and comments may surround the tokens. A body is required and may start
with a variable, literal, tuple/field selector, or ordinary function. Grouping
parentheses do not themselves introduce another binding or runtime scope.

### Syntax API and runtime integration

`InputBindingExpressionSyntax : ExpressionSyntax` is an ordered pipeline stage.
Its optional `BindingPatternSyntax Binding` is a `BindingNameSyntax` for a whole
input name, a `PositionalBindingPatternSyntax` for destructuring, or null for an
anonymous binding. `RootExpressionSyntax Body` preserves either an open or closed
root. A grouped body retains an `OpenExpressionSyntax` wrapper containing one
`ParenthesizedExpressionSyntax`; its `Expression` property preserves the actual
inner open/closed root, including a variable or literal source and its pipeline.
Consumers must handle both root kinds rather than requiring an open function-only
body. In particular, a grouped variable-led body is syntactically valid; rejection
of its closed root by a runtime binder must be corrected in that consumer.

These names replace `InputBoundExpressionSyntax` and the former declaration base
`InputBindingSyntax`. The CST uses `input_binding_expression` with `binding` and
`body` fields. Managed children are the binding (when present) followed by the body.
Punctuation remains in the CST and original text; every node preserves source spans.

Binding names use `[A-Za-z][A-Za-z0-9]*`. References use existing `@name` syntax;
bare names remain function calls. Positional patterns expose ordered
`IReadOnlyList<BindingNameSyntax> Names` with individual spans, distinct from tuple
or pair literals. Lists require at least two names; empty/single-name lists,
trailing commas, rest patterns, and nested patterns are rejected. Duplicate names
are retained for semantic diagnostics.

The Expressif runtime owns capture, lexical resolution, shadowing, scope restoration,
input types, component ordering, exact destructuring arity, and evaluation results.
Scoped tuple access (`^$1`, `^^$1`) stays distinct from from-end access (`$^1`).
This correction supports Expressif #980–#982 and its runtime integration PR #988.
The corrected package is published by the main-branch release workflow after merge.

## Development

Install the dependencies:

```sh
npm install
```

Generate the parser:

```sh
npx tree-sitter generate
```

Run the grammar tests:

```sh
npx tree-sitter test
```

The grammar should remain independent from the Expressif function catalogue. Parsing determines the syntactic structure of an expression; resolution of functions, predicates, accumulators and their accepted arguments belongs to the language-specific semantic binding layer.

## Releases

For every push to `main`, CI builds, tests, and collects the distributable artifacts. After every validation job succeeds, a patch-zero version is published from those same collected artifacts to the corresponding `vX.Y.0` GitHub release and NuGet.org. Other versions complete validation without publishing artifacts.

Each GitHub release contains the distributable artifacts collected by CI after package validation:

* the C# NuGet package
* the native parser source archive

Only the C# package is currently published to an external registry. NuGet publication uses GitHub OIDC trusted publishing to obtain a short-lived API key, so no long-lived NuGet API key is stored in the repository. Configure the trusted publishing policy on [NuGet.org](https://www.nuget.org/) with these values:

* Repository Owner: `Seddryck`
* Repository: `Expressif.Syntax`
* Workflow File: `ci.yml`
* Environment: leave blank

The policy's NuGet user must be `Seddryck`, matching the `NuGet/login` step in the workflow.

## Related projects

* [Expressif](https://github.com/Seddryck/Expressif) — C# implementation and reference project
* [Expressif documentation](https://seddryck.github.io/Expressif/) — language documentation
