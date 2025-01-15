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
