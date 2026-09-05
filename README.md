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
```

Record access always uses `.` for navigation. A leading `^` changes the root
from the current pipeline value to the current expression input; each additional
`^` moves outward by one enclosing expression. It does not change how fields are
selected. Access can be chained for nested records, for example
`.customer.address`, `^.customer.0`, or `^^.customer.0`. The former bracket forms
`[name]` and `[0]` are replaced by `^.name` and `^.0` respectively.

Element positions are zero-based. `$n` counts from the beginning and `$^n`
counts from the end. The parser represents both tuple and array access with the
same `positional_element_access` node; downstream binders decide whether the
runtime value supports positional access and how invalid or out-of-range access
is handled.

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
