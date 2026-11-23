# Changelog

What each release changes for a project that references the package. The exact definition of every rule is the
rule catalog in [AGENTS.md](AGENTS.md).

## 0.1.1

* Faster analysis of functional-core boundaries, mutation through aliases and union cases.
* FP0501, FP0502 and FP0503 leave a constructed `closed` class or record to the compiler when it cannot list all
  cases. FP0601 treats a type parameter constrained to one like other type parameters.
* FP0404 allows default construction of hand-written union structs whose `Value` property is computed, and
  construction with an object initializer that assigns `Value`.
* FP0104 and FP0105 follow a value through a target-typed `?:` or `switch` expression, as in
  `Shape shape = pick ? square : triangle;`. A write through such a local is now reported.

## 0.1.0

First release.

* 24 rules for C# 15 on .NET 11, all with category `Functional` and severity `Warning`. Every message says what to
  write instead.
* Inside types marked `[Fprog.FunctionalCore]`, and inside every type of an assembly marked
  `[assembly: Fprog.FunctionalCore]`:
  * purity: FP0101 (no `void` results), FP0102 (no hidden inputs or mutable static state), FP0103 (no `ref` or `out`
    parameters), FP0104 (no writes into memory the caller owns), FP0105 (no state change after construction), FP0106
    (no dependence on the current culture), FP0107 (no pointers or `unsafe`);
  * immutability: FP0201 (fields), FP0202 (set accessors), FP0203 (mutable collections in records and readonly
    structs);
  * totality: FP0301 (no `throw`), FP0302 (no partial operations), FP0303 (no `null`, `default` or `!` for a missing
    result);
  * shape: FP0401 (classes and records are `sealed`, `static` or `closed`), FP0601 (no runtime type tests, no
    `dynamic`), FP0701 (closures capture variables that are assigned once);
  * effects: FP0801 (shell types and `lock` stay outside), FP0802 (asynchronous types and `async` stay outside).
* On every `union` type and `closed` class of the compilation: FP0403 (cases are `sealed` or `closed`), FP0404 (no
  empty union value), FP0501 (no catch-all), FP0502 (a `switch` statement handles every case), FP0503 (a case is
  picked in a `switch`).
* Outside the core: FP0803 (core classes and interfaces are implemented inside the core).
* The package compiles `Fprog.FunctionalCoreAttribute` into the referencing project as an internal type.
  `FPROG_NO_ATTRIBUTES` leaves it out.
* `fprog_pure_apis` in `.editorconfig` or `.globalconfig` names libraries you trust to be pure.
* Requires the .NET 11 SDK 11.0.100 or later; the analyzers are compiled against Roslyn 5.12.0.
