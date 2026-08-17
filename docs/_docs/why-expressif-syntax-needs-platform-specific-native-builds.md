# Why Expressif.Syntax Needs Platform-Specific Native Builds

For the C# version of `Expressif.Syntax`, the important distinction is between the **C# bindings** and the **Tree-sitter C parser** underneath.

## C# bindings

The C# part is compiled to .NET IL:

```text
C# source
   ↓
.NET IL
   ↓
.NET runtime / JIT
   ↓
x64 or ARM64 machine code
```

That means the same managed assembly can normally run on different CPU architectures.

So `Expressif.Syntax.dll` does **not** need a separate build just because the machine is:

```text
Windows x64
Windows ARM64
Linux x64
Linux ARM64
macOS x64
macOS ARM64
```

The .NET runtime handles the CPU-specific execution.

## Tree-sitter C parser

The Tree-sitter C code is different.

C is compiled directly into machine code for a specific target:

```text
Tree-sitter C
      ↓
   compiler
      ↓
x64 machine code
```

or:

```text
Tree-sitter C
      ↓
   compiler
      ↓
ARM64 machine code
```

An x64 binary cannot simply run as ARM64.

The operating system also matters because the resulting native library format and runtime environment are different between Windows, Linux, and macOS.

So the native parser needs distinct builds for each supported combination.

A reasonable build matrix is therefore:

```text
Windows
    x64
    ARM64

Linux
    x64
    ARM64

macOS
    x64
    ARM64
```

Or in RID form:

```text
win-x64
win-arm64

linux-x64
linux-arm64

osx-x64
osx-arm64
```

## What the pipeline is really building

The platform matrix should not be understood as:

> Build the C# bindings six times.

It is really:

> Build the Tree-sitter C parser for each supported OS/CPU combination, and package those binaries so that the same C# binding can load the correct one at runtime.

Conceptually:

```text
                    Expressif.Syntax.dll
                         C# / .NET
                             │
                             ▼
                    Tree-sitter binding
                             │
          ┌──────────────────┼──────────────────┐
          ▼                  ▼                  ▼
       Windows             Linux              macOS
       C binary            C binary           C binary
       x64/ARM64           x64/ARM64          x64/ARM64
```

## Rule for Expressif.Syntax

For the C# implementation:

> **The C# bindings are architecture-independent. The Tree-sitter C parser is not.**

Therefore the OS/architecture matrix exists because of the **C parser**, not because of the C# code.

The pipeline therefore produces and validates:

```text
win-x64
win-arm64
linux-x64
linux-arm64
osx-x64
osx-arm64
```

This gives complete x64/ARM64 coverage across Windows, Linux, and macOS.
