# Raze

### Raze Lang

<!-- ![Raze-Logo](https://theaustincommon.com/wp-content/uploads/2015/11/Your-Logo-Here.png) -->

> A Modern C-Like Programming Language

## About

Raze is a Compiled, Object Oriented, Statically & Strictly typed toy programming language that combines all the intuitive features from languages such as C, C#, and Python into a neat syntax, while also offering access to lower level programming.

## Syntax

Raze's syntax resembles other C-Family languages such as C and C#.

Example:

```py 

# Hello-World in Raze

```

```js

function Main()
{
    Print("Hello, World!");
}

```

See all the examples [here](Raze-Driver/Examples)

<!-- ## How to Install and Run



![](https://miro.medium.com/max/1400/1*zGZSsGmCMrAF3PEkrvUgKg.gif) -->

## TODO
### Types
- [ ] Type Nullability 
- [ ] Generics & Arrays (`function Main(Array<string> args)`)
- [ ] Fix string types
- [ ] Interfaces
- [ ] Multiple inheritance?
### CodeGen and Backend
- [ ] RIL (Raze Intermediate Language)
- [ ] CodeGen optimizations
- [ ] Constant propagation
- [x] Fix register allocation (refactor, liveness analysis, graph coloring)
- [ ] Managed heap & GC
- [ ] Support for more platforms (Windows, Mac)
- [ ] Support for more backends/architectures (LLVM, RISC-V, ARM, etc.)
### Exprs
- [ ] Structs, `stackalloc`, and structure alignment
- [ ] `break` and `continue` keywords
- [ ] Full constexpr (compile time evaluation) support
- [ ] `static` and `const` variables
- [ ] First class and higher order functions
- [ ] Enums
- [ ] `switch` statements
- [ ] Anonynmous functions, classes, and enums
- [ ] Decorators
- [ ] Snytax cleanup
### Compiler Messages
- [ ] Add Compiler warnings and messages
- [ ] Improve error line number reporting
- [ ] Stack traces
### Other
- [ ] `constexpr` system (also to replace Primitives.cs)
- [ ] Cleanups
- [ ] Speed tests
- [ ] Unit testing
- [ ] LSP Server
- [ ] Compiler rewrite in C/C++?
- [ ] Implement a Roslyn analyzer to ensure marshalled structs have pack 1

## Releases

`Raze Compiler ALPHA V0.0.0`  - MM/DD/YY
